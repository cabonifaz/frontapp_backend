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

        if (rows.Count == 0)
            return Ok(new { partidos = new List<object>(), solicitudes = new List<object>() });

        // 1. FILTRAR ESTRICTAMENTE: Descartar cualquier fila cuyo estado sea 0 (cancelado)
        var filasActivas = rows.Where(r => 
        {
            if (r.TryGetValue("estado", out var est) && est != null)
            {
                return Convert.ToInt32(est) != 0;
            }
            return true;
        }).ToList();

        if (filasActivas.Count == 0)
            return Ok(new { partidos = new List<object>(), solicitudes = new List<object>() });

        // 2. Agrupar solo las filas activas por id_partido
        var partidosAgrupados = filasActivas
            .GroupBy(r => r["id_partido"])
            .Where(g => g.Key != null)
            .Select(g => new
            {
                id_partido      = g.Key,
                fecha           = g.First()["fecha"],
                hora            = g.First()["hora"],
                nombre_cancha   = g.First()["nombre_cancha"],
                foto_cancha_url = g.First().ContainsKey("foto_cancha_url") ? g.First()["foto_cancha_url"] : null,
                estado          = g.First().ContainsKey("estado") ? g.First()["estado"] : 1
            })
            .ToList<object>();

        // 3. Mapear solicitudes únicamente de los partidos activos
        var solicitudes = filasActivas
            .Where(r => r["id_solicitud"] != null)
            .Select(r => new
            {
                id_solicitud    = r["id_solicitud"],
                id_partido      = r["id_partido"],
                id_usuario      = r["id_usuario"],
                nombre          = r["nombre"],
                apellidos       = r["apellidos"],
                foto_perfil_url = r["foto_perfil_url"],
                fecha           = r["fecha"],
                hora            = r["hora"],
                nombre_cancha   = r["nombre_cancha"],
            })
            .ToList<object>();

        return Ok(new { partidos = partidosAgrupados, solicitudes });
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