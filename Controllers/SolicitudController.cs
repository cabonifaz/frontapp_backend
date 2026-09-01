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

        // El SP devuelve p.id_estado AS "estado". Solo mostramos Buscando(28) y Pendiente(29).
        var partidosAgrupados = rows
            .GroupBy(r => r["id_partido"])
            .Where(g => g.Key != null)
            .Select(g =>
            {
                var first = g.First();
                // El SP alias "estado" contiene el id_estado real del partido
                var idEstado = first.TryGetValue("estado", out var est) && est != null
                    ? Convert.ToInt32(est) : 0;
                return new { first, idEstado };
            })
            .Where(x => x.idEstado == 28 || x.idEstado == 29) // Solo Buscando Oponente o Pendiente
            .Select(x => (object)new
            {
                id_partido      = x.first["id_partido"],
                fecha           = x.first["fecha"],
                hora            = x.first["hora"],
                nombre_cancha   = x.first["nombre_cancha"],
                foto_cancha_url = x.first.ContainsKey("foto_cancha_url") ? x.first["foto_cancha_url"] : null,
            })
            .ToList();

        var solicitudes = rows
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

    // GET api/Solicitud/pendientes-recibidas
    [HttpGet("pendientes-recibidas")]
    public async Task<IActionResult> PendientesRecibidas()
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var rows = await SpHelper.QueryAsync(conn, "sp_solicitud_listar_mis_solicitudes",
            new() { ["p_id_usuario"] = idUsuario });

        var pendientes = rows
            .Where(r => r["id_solicitud"] != null)
            .Where(r =>
            {
                if (r.TryGetValue("estado", out var est) && est != null)
                    return Convert.ToInt32(est) == 1;
                return true;
            })
            .Select(r => new
            {
                id_solicitud = r["id_solicitud"],
                id_partido   = r["id_partido"],
                nombre       = r["nombre"],
            })
            .DistinctBy(r => r.id_solicitud)
            .ToList<object>();

        return Ok(pendientes);
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

    // POST api/Solicitud/{idSolicitud}/rechazar/{idRetadore}
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