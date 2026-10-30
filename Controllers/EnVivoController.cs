using System.Text.Json;
using AppFronton.Data;
using AppFronton.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace AppFronton.Controllers;

/// <summary>
/// NUEVO (07): partidos EN VIVO. Los jugadores del partido (o un admin) van anotando
/// el marcador de cada set; amigos y seguidores lo ven actualizarse.
/// El resultado oficial se sigue publicando y confirmando como siempre.
/// </summary>
[ApiController]
[Route("api/EnVivo")]
[Authorize]
public class EnVivoController(AppDbContext db) : ControllerBase
{
    // GET api/EnVivo  ->  partidos en vivo de mis amigos, seguidos y míos (admin: todos)
    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        return Ok(await SpHelper.QueryAsync(conn, "sp_vivo_listar", new() { ["p_id_usuario"] = idUsuario }));
    }

    // GET api/EnVivo/para-iniciar  ->  partidos confirmados de hoy que aún no están en vivo
    [HttpGet("para-iniciar")]
    public async Task<IActionResult> ParaIniciar()
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        return Ok(await SpHelper.QueryAsync(conn, "sp_vivo_partidos_para_iniciar", new() { ["p_id_usuario"] = idUsuario }));
    }

    // GET api/EnVivo/{idPartido}
    [HttpGet("{idPartido:int}")]
    public async Task<IActionResult> Detalle(int idPartido)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var rows = await SpHelper.QueryAsync(conn, "sp_vivo_detalle",
            new() { ["p_id_partido"] = idPartido, ["p_id_usuario"] = idUsuario });
        var fila = rows.FirstOrDefault();
        if (fila == null) return NotFound(new { mensaje = "Partido no encontrado." });

        // Los JSON de MySQL llegan como texto: se devuelven como listas
        foreach (var clave in new[] { "jugadores_json", "sets_json", "eventos_json", "delegados_json" })
        {
            var texto = Convert.ToString(fila.GetValueOrDefault(clave));
            fila[clave.Replace("_json", "")] = string.IsNullOrWhiteSpace(texto)
                ? (object)Array.Empty<object>()
                : JsonDocument.Parse(texto).RootElement.Clone();
            fila.Remove(clave);
        }
        return Ok(fila);
    }

    // POST api/EnVivo/{idPartido}/iniciar
    [HttpPost("{idPartido:int}/iniciar")]
    public async Task<IActionResult> Iniciar(int idPartido)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_vivo_iniciar",
            inParams: new() { ["p_id_partido"] = idPartido, ["p_id_usuario"] = idUsuario },
            outParams: new() { ["p_exito"] = MySqlDbType.Byte, ["p_mensaje"] = MySqlDbType.VarChar });
        return Respuesta(result);
    }

    // POST api/EnVivo/{idPartido}/marcar   body: { numero_set: 2, puntos_lado1: 10, puntos_lado2: 8 }
    [HttpPost("{idPartido:int}/marcar")]
    public async Task<IActionResult> Marcar(int idPartido, [FromBody] Dictionary<string, object?> body)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_vivo_marcar",
            inParams: new()
            {
                ["p_id_partido"] = idPartido,
                ["p_id_usuario"] = idUsuario,
                ["p_numero_set"] = body.GetValueOrDefault("numero_set"),
                ["p_puntos1"]    = body.GetValueOrDefault("puntos_lado1"),
                ["p_puntos2"]    = body.GetValueOrDefault("puntos_lado2")
            },
            outParams: new() { ["p_exito"] = MySqlDbType.Byte, ["p_mensaje"] = MySqlDbType.VarChar });
        return Respuesta(result);
    }

    // POST api/EnVivo/{idPartido}/sumar   body: { lado: 1|2, delta: 1|-1 }   (botones + / − del set en juego)
    [HttpPost("{idPartido:int}/sumar")]
    public async Task<IActionResult> Sumar(int idPartido, [FromBody] Dictionary<string, object?> body)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_vivo_sumar",
            inParams: new()
            {
                ["p_id_partido"] = idPartido,
                ["p_id_usuario"] = idUsuario,
                ["p_lado"]       = body.GetValueOrDefault("lado"),
                ["p_delta"]      = body.GetValueOrDefault("delta")
            },
            outParams: new() { ["p_exito"] = MySqlDbType.Byte, ["p_mensaje"] = MySqlDbType.VarChar });
        return Respuesta(result);
    }

    // NUEVO (08) ─ POST api/EnVivo/{idPartido}/delegar   body: { id_amigo }   (un amigo puede anotar el marcador)
    [HttpPost("{idPartido:int}/delegar")]
    public async Task<IActionResult> Delegar(int idPartido, [FromBody] Dictionary<string, object?> body)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_vivo_delegar",
            inParams: new() { ["p_id_partido"] = idPartido, ["p_id_usuario"] = idUsuario, ["p_id_amigo"] = body.GetValueOrDefault("id_amigo") },
            outParams: new() { ["p_exito"] = MySqlDbType.Byte, ["p_mensaje"] = MySqlDbType.VarChar });
        return Respuesta(result);
    }

    // NUEVO (08) ─ POST api/EnVivo/{idPartido}/delegar/quitar   body: { id_amigo }
    [HttpPost("{idPartido:int}/delegar/quitar")]
    public async Task<IActionResult> QuitarDelegado(int idPartido, [FromBody] Dictionary<string, object?> body)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_vivo_quitar_delegado",
            inParams: new() { ["p_id_partido"] = idPartido, ["p_id_usuario"] = idUsuario, ["p_id_amigo"] = body.GetValueOrDefault("id_amigo") },
            outParams: new() { ["p_exito"] = MySqlDbType.Byte, ["p_mensaje"] = MySqlDbType.VarChar });
        return Respuesta(result);
    }

    // POST api/EnVivo/{idPartido}/finalizar
    [HttpPost("{idPartido:int}/finalizar")]
    public async Task<IActionResult> Finalizar(int idPartido)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_vivo_finalizar",
            inParams: new() { ["p_id_partido"] = idPartido, ["p_id_usuario"] = idUsuario },
            outParams: new() { ["p_exito"] = MySqlDbType.Byte, ["p_mensaje"] = MySqlDbType.VarChar });
        return Respuesta(result);
    }

    private IActionResult Respuesta(Dictionary<string, object?> result) =>
        Convert.ToInt32(result["p_exito"]) == 0
            ? BadRequest(new { mensaje = result["p_mensaje"] })
            : Ok(new { exito = true, mensaje = result["p_mensaje"] });
}
