using AppFronton.Data;
using AppFronton.Helpers;
using MySqlConnector;

namespace AppFronton.Services;

/// <summary>
/// NUEVO (08): cada 10 minutos confirma los resultados que el rival no confirmó ni corrigió
/// dentro del plazo (CFG_HORAS_LIMITE_REVISION = 24 h). Toda la lógica está en
/// sp_resultado_auto_aceptar_vencidos (suma los puntos, finaliza el partido y avisa).
///
/// Igual que el envío de push, solo corre si PUSH_WORKER=true (en el servidor, no en las PCs),
/// para que no lo ejecuten varias máquinas a la vez.
/// </summary>
public class ResultadosVencidosWorker(AppDbContext db, ILogger<ResultadosVencidosWorker> log) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var habilitado = string.Equals(Environment.GetEnvironmentVariable("PUSH_WORKER"), "true",
                                       StringComparison.OrdinalIgnoreCase);
        if (!habilitado)
        {
            log.LogInformation("ResultadosVencidosWorker desactivado (PUSH_WORKER != true).");
            return;
        }

        log.LogInformation("ResultadosVencidosWorker iniciado.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var conn = db.CreateConnection();
                var r = await SpHelper.ExecuteAsync(conn, "sp_resultado_auto_aceptar_vencidos",
                    outParams: new() { ["p_registros_procesados"] = MySqlDbType.Int32 });
                var total = Convert.ToInt32(r["p_registros_procesados"] ?? 0);
                if (total > 0) log.LogInformation("Resultados confirmados por plazo vencido: {Total}", total);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Error confirmando resultados vencidos.");
            }

            try { await Task.Delay(Intervalo, stoppingToken); }
            catch (TaskCanceledException) { }
        }
    }
}
