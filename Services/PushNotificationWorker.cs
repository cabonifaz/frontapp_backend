using System.Net.Http.Json;
using System.Text.Json;
using AppFronton.Data;
using AppFronton.Helpers;

namespace AppFronton.Services;

/// <summary>
/// NUEVO (APP-AVO-041 / 042): envía por push las notificaciones que crean los SP.
/// Cada 30 segundos toma un lote de la tabla `notificacion` (sp_push_tomar_pendientes)
/// y lo envía con el servicio de Expo (https://exp.host). No necesita llaves en el backend:
/// las credenciales de Firebase/Apple se configuran una sola vez en EAS (expo.dev).
///
/// Solo corre si en el .env está PUSH_WORKER=true. Déjalo en false en las PCs de desarrollo
/// y en true únicamente en el servidor, para no mandar avisos desde varias máquinas.
/// </summary>
public class PushNotificationWorker(AppDbContext db, IHttpClientFactory httpFactory, ILogger<PushNotificationWorker> log)
    : BackgroundService
{
    private const string ExpoUrl = "https://exp.host/--/api/v2/push/send";
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var habilitado = string.Equals(Environment.GetEnvironmentVariable("PUSH_WORKER"), "true",
                                       StringComparison.OrdinalIgnoreCase);
        if (!habilitado)
        {
            log.LogInformation("PushNotificationWorker desactivado (PUSH_WORKER != true).");
            return;
        }

        log.LogInformation("PushNotificationWorker iniciado.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcesarLote(stoppingToken);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Error enviando notificaciones push.");
            }

            try { await Task.Delay(Intervalo, stoppingToken); }
            catch (TaskCanceledException) { }
        }
    }

    private async Task ProcesarLote(CancellationToken ct)
    {
        var lote = Guid.NewGuid().ToString();
        List<Dictionary<string, object?>> filas;
        using (var conn = db.CreateConnection())
        {
            filas = await SpHelper.QueryAsync(conn, "sp_push_tomar_pendientes",
                new() { ["p_lote"] = lote, ["p_limite"] = 100 });
        }
        if (filas.Count == 0) return;

        var mensajes = filas.Select(f =>
        {
            var idioma = Convert.ToString(f["idioma"]) == "en" ? "en" : "es";
            var (titulo, cuerpo) = Textos(Convert.ToString(f["tipo"]) ?? "", Convert.ToString(f["actor_nombre"]), idioma);
            return new
            {
                to       = Convert.ToString(f["token"]),
                title    = titulo,
                body     = cuerpo,
                sound    = "default",
                channelId = "default",
                badge    = Convert.ToInt32(f["no_leidas"] ?? 0),
                data     = new
                {
                    id_notificacion = f["id_notificacion"],
                    tipo            = f["tipo"],
                    id_partido      = f["id_partido"],
                    id_resultado    = f["id_resultado"]
                }
            };
        }).ToList();

        // Resultado por notificación: 1 si llegó a al menos un dispositivo, 3 si falló en todos
        var estado = new Dictionary<long, int>();
        var tokensDadosDeBaja = new List<string>();

        var http = httpFactory.CreateClient();
        foreach (var bloque in mensajes.Select((m, i) => (m, i)).Chunk(100))
        {
            var resp = await http.PostAsJsonAsync(ExpoUrl, bloque.Select(x => x.m).ToList(), ct);
            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            var tickets = json.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Array
                ? d.EnumerateArray().ToList()
                : new List<JsonElement>();

            for (var k = 0; k < bloque.Length; k++)
            {
                var (m, i) = bloque[k];
                var id = Convert.ToInt64(filas[i]["id_notificacion"]);
                var ok = k < tickets.Count
                         && tickets[k].TryGetProperty("status", out var st) && st.GetString() == "ok";

                if (ok) estado[id] = 1;
                else if (!estado.ContainsKey(id)) estado[id] = 3;

                if (!ok && k < tickets.Count
                    && tickets[k].TryGetProperty("details", out var det)
                    && det.TryGetProperty("error", out var err)
                    && err.GetString() == "DeviceNotRegistered")
                {
                    tokensDadosDeBaja.Add(m.to!);
                }
            }
        }

        using var conn2 = db.CreateConnection();
        foreach (var (id, est) in estado)
            await SpHelper.ExecuteAsync(conn2, "sp_push_marcar",
                inParams: new() { ["p_id_notificacion"] = id, ["p_estado"] = est });
        foreach (var token in tokensDadosDeBaja.Distinct())
            await SpHelper.ExecuteAsync(conn2, "sp_push_desactivar_token",
                inParams: new() { ["p_token"] = token, ["p_id_usuario"] = null });

        log.LogInformation("Push: {Total} avisos procesados.", estado.Count);
    }

    // Textos del aviso según el tipo (mismo significado que en la bandeja de la app)
    private static (string Titulo, string Cuerpo) Textos(string tipo, string? actor, string idioma)
    {
        var a = string.IsNullOrWhiteSpace(actor) ? null : actor;
        if (idioma == "en")
        {
            return tipo switch
            {
                "RESULTADO_POR_CONFIRMAR" => ("Result to confirm", $"{a ?? "Your opponent"} posted your match result. Please confirm it."),
                "RESULTADO_CONFIRMADO"    => ("Result confirmed", "Your match result is now official."),
                "RESULTADO_AMIGO"         => ("New result", $"{a ?? "A player you follow"} has a new result. Check it out!"),
                "NUEVO_SEGUIDOR"          => ("New follower", $"{a ?? "Someone"} started following you."),
                _                         => ("Ranked", "You have a new notification."),
            };
        }
        return tipo switch
        {
            "RESULTADO_POR_CONFIRMAR" => ("Resultado por confirmar", $"{a ?? "Tu rival"} registró el resultado de su partido. Confírmalo."),
            "RESULTADO_CONFIRMADO"    => ("Resultado confirmado", "El resultado de tu partido ya es oficial."),
            "RESULTADO_AMIGO"         => ("Nuevo resultado", $"{a ?? "Un jugador que sigues"} tiene un nuevo resultado. ¡Míralo!"),
            "NUEVO_SEGUIDOR"          => ("Nuevo seguidor", $"{a ?? "Alguien"} empezó a seguirte."),
            _                         => ("Ranked", "Tienes una nueva notificación."),
        };
    }
}
