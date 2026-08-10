using AppFronton.Data;
using AppFronton.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace AppFronton.Controllers;

[ApiController]
[Route("api/Solicitud")]
[Authorize]
public class SolicitudController(AppDbContext db) : ControllerBase
{
    // GET api/Solicitud/mis-solicitudes
    [HttpGet("mis-solicitudes")]
    public async Task<IActionResult> MisSolicitudes()
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var rows = await SpHelper.QueryAsync(conn, "sp_solicitud_listar_mis_solicitudes",
            new() { ["p_id_usuario"] = idUsuario });
        return Ok(rows);
    }

    // POST api/Solicitud/{idSolicitud}/aceptar/{idRetador}
    [HttpPost("{idSolicitud:int}/aceptar/{idRetador:int}")]
    public async Task<IActionResult> AceptarRetador(int idSolicitud, int idRetador)
    {
        var idCreador = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_solicitud_aceptar_retador",
            inParams: new()
            {
                ["p_id_partido"]  = idSolicitud,
                ["p_id_retador"]  = idRetador,
                ["p_id_creador"]  = idCreador
            },
            outParams: new()
            {
                ["p_exito"]          = MySqlDbType.Byte,
                ["p_mensaje"]        = MySqlDbType.VarChar,
                ["p_nombre_retador"] = MySqlDbType.VarChar
            });

        if (Convert.ToInt32(result["p_exito"]) == 0)
            return BadRequest(new { mensaje = result["p_mensaje"] });

        return Ok(new { exito = true, mensaje = result["p_mensaje"], nombreRetador = result["p_nombre_retador"] });
    }

    // POST api/Solicitud/{idSolicitud}/rechazar/{idRetador}
    [HttpPost("{idSolicitud:int}/rechazar/{idRetador:int}")]
    public async Task<IActionResult> RechazarRetador(int idSolicitud, int idRetador)
    {
        var idCreador = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_solicitud_rechazar_retador",
            inParams: new()
            {
                ["p_id_partido"] = idSolicitud,
                ["p_id_retador"] = idRetador,
                ["p_id_creador"] = idCreador
            },
            outParams: new()
            {
                ["p_exito"]   = MySqlDbType.Byte,
                ["p_mensaje"] = MySqlDbType.VarChar
            });

        if (Convert.ToInt32(result["p_exito"]) == 0)
            return BadRequest(new { mensaje = result["p_mensaje"] });

        return Ok(new { exito = true, mensaje = result["p_mensaje"] });
    }
}
