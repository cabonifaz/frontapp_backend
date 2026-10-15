using AppFronton.Data;
using AppFronton.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace AppFronton.Controllers;

// NUEVO (APP-AVO-041 / 042): bandeja de notificaciones y registro del celular para push.
[ApiController]
[Route("api/Notificacion")]
[Authorize]
public class NotificacionController(AppDbContext db) : ControllerBase
{
    // GET api/Notificacion?limite=50
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] int limite = 50)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var rows = await SpHelper.QueryAsync(conn, "sp_notificacion_listar",
            new() { ["p_id_usuario"] = idUsuario, ["p_limite"] = Math.Clamp(limite, 1, 100) });
        return Ok(rows);
    }

    // GET api/Notificacion/no-leidas  ->  { total }
    [HttpGet("no-leidas")]
    public async Task<IActionResult> NoLeidas()
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_notificacion_contar_no_leidas",
            inParams: new() { ["p_id_usuario"] = idUsuario },
            outParams: new() { ["p_total"] = MySqlDbType.Int32 });
        return Ok(new { total = Convert.ToInt32(result["p_total"] ?? 0) });
    }

    // POST api/Notificacion/leidas   body: { "id_notificacion": 12 }  (sin id = marcar todas)
    [HttpPost("leidas")]
    public async Task<IActionResult> MarcarLeidas([FromBody] Dictionary<string, object?>? body)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_notificacion_marcar_leidas",
            inParams: new()
            {
                ["p_id_usuario"]      = idUsuario,
                ["p_id_notificacion"] = body?.GetValueOrDefault("id_notificacion")
            },
            outParams: new() { ["p_actualizadas"] = MySqlDbType.Int32 });
        return Ok(new { actualizadas = Convert.ToInt32(result["p_actualizadas"] ?? 0) });
    }

    // POST api/Notificacion/push-token   body: { "token": "ExponentPushToken[...]", "plataforma": "android", "idioma": "es" }
    [HttpPost("push-token")]
    public async Task<IActionResult> RegistrarToken([FromBody] Dictionary<string, object?> body)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_push_registrar_token",
            inParams: new()
            {
                ["p_id_usuario"] = idUsuario,
                ["p_token"]      = body.GetValueOrDefault("token"),
                ["p_plataforma"] = body.GetValueOrDefault("plataforma"),
                ["p_idioma"]     = body.GetValueOrDefault("idioma")
            },
            outParams: new() { ["p_exito"] = MySqlDbType.Byte });

        if (Convert.ToInt32(result["p_exito"]) == 0)
            return BadRequest(new { mensaje = "Token inválido." });

        return Ok(new { exito = true });
    }

    // POST api/Notificacion/push-token/desactivar   body: { "token": "..." }   (al cerrar sesión)
    [HttpPost("push-token/desactivar")]
    public async Task<IActionResult> DesactivarToken([FromBody] Dictionary<string, object?> body)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        await SpHelper.ExecuteAsync(conn, "sp_push_desactivar_token",
            inParams: new() { ["p_token"] = body.GetValueOrDefault("token"), ["p_id_usuario"] = idUsuario });
        return Ok(new { exito = true });
    }
}
