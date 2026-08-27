using AppFronton.Data;
using AppFronton.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace AppFronton.Controllers;

[ApiController]
[Authorize]
public class PartidoController(AppDbContext db) : ControllerBase
{
    // ── Buscar ───────────────────────────────────────────────────────────────

    [HttpGet("api/PartidoRankeado/buscar")]
    public async Task<IActionResult> BuscarRankeado(
        [FromQuery] int id_deporte,
        [FromQuery] int? id_cancha,
        [FromQuery] string? fecha,
        [FromQuery] string? hora,
        [FromQuery] int? id_tipo_juego)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var rows = await SpHelper.QueryAsync(conn, "sp_partido_buscar_rankeado", new()
        {
            ["p_id_usuario"]    = idUsuario,
            ["p_id_deporte"]    = id_deporte,
            ["p_filtro_cancha"] = id_cancha,
            ["p_filtro_fecha"]  = fecha,
            ["p_filtro_hora"]   = hora,
            ["p_id_tipo_juego"] = id_tipo_juego   // ✅ añadido
        });
        return Ok(rows);
    }

    [HttpGet("api/PartidoAmistoso/buscar")]
    public async Task<IActionResult> BuscarAmistoso(
        [FromQuery] int id_deporte,
        [FromQuery] int? id_cancha,
        [FromQuery] string? fecha,
        [FromQuery] string? hora,
        [FromQuery] int? id_tipo_juego)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var rows = await SpHelper.QueryAsync(conn, "sp_partido_buscar_amistoso", new()
        {
            ["p_id_usuario"]    = idUsuario,
            ["p_id_deporte"]    = id_deporte,
            ["p_filtro_cancha"] = id_cancha,
            ["p_filtro_fecha"]  = fecha,
            ["p_filtro_hora"]   = hora,
            ["p_id_tipo_juego"] = id_tipo_juego   // ✅ añadido
        });
        return Ok(rows);
    }

    // ── Crear ────────────────────────────────────────────────────────────────

    [HttpPost("api/PartidoRankeado/crear")]
    public async Task<IActionResult> CrearRankeado([FromBody] Dictionary<string, object?> body)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_partido_crear_rankeado",
            inParams: new()
            {
                ["p_id_usuario"]    = idUsuario,
                ["p_id_deporte"]    = body.GetValueOrDefault("id_deporte"),
                ["p_id_cancha"]     = body.GetValueOrDefault("id_cancha"),
                ["p_fecha"]         = body.GetValueOrDefault("fecha"),
                ["p_hora"]          = body.GetValueOrDefault("hora"),
                ["p_id_tipo_juego"] = body.GetValueOrDefault("id_tipo_juego")
            },
            outParams: new()
            {
                ["p_id_partido_creado"] = MySqlDbType.Int32,
                ["p_exito"]             = MySqlDbType.Byte,
                ["p_mensaje"]           = MySqlDbType.VarChar
            });

        if (Convert.ToInt32(result["p_exito"]) == 0)
            return BadRequest(new { mensaje = result["p_mensaje"] });

        return Ok(new { idPartidoCreado = result["p_id_partido_creado"], exito = true, mensaje = result["p_mensaje"] });
    }

    [HttpPost("api/PartidoAmistoso/crear")]
    public async Task<IActionResult> CrearAmistoso([FromBody] Dictionary<string, object?> body)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_partido_crear_amistoso",
            inParams: new()
            {
                ["p_id_usuario"]    = idUsuario,
                ["p_id_deporte"]    = body.GetValueOrDefault("id_deporte"),
                ["p_id_cancha"]     = body.GetValueOrDefault("id_cancha"),
                ["p_fecha"]         = body.GetValueOrDefault("fecha"),
                ["p_hora"]          = body.GetValueOrDefault("hora"),
                ["p_id_tipo_juego"] = body.GetValueOrDefault("id_tipo_juego")
            },
            outParams: new()
            {
                ["p_id_partido_creado"] = MySqlDbType.Int32,
                ["p_exito"]             = MySqlDbType.Byte,
                ["p_mensaje"]           = MySqlDbType.VarChar
            });

        if (Convert.ToInt32(result["p_exito"]) == 0)
            return BadRequest(new { mensaje = result["p_mensaje"] });

        return Ok(new { idPartidoCreado = result["p_id_partido_creado"], exito = true, mensaje = result["p_mensaje"] });
    }

    // ── Postular / Cancelar ──────────────────────────────────────────────────

    [HttpPost("api/Partido/postular/{idPartido:int}")]
    public async Task<IActionResult> Postular(int idPartido)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_partido_postular",
            inParams: new() { ["p_id_partido"] = idPartido, ["p_id_usuario"] = idUsuario },
            outParams: new() { ["p_exito"] = MySqlDbType.Byte, ["p_mensaje"] = MySqlDbType.VarChar });

        if (Convert.ToInt32(result["p_exito"]) == 0)
            return BadRequest(new { mensaje = result["p_mensaje"] });

        return Ok(new { exito = true, mensaje = result["p_mensaje"] });
    }

    [HttpPost("api/Partido/{id:int}/cancelar")]
    public async Task<IActionResult> Cancelar(int id)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_partido_cancelar",
            inParams: new() { ["p_id_partido"] = id, ["p_id_usuario"] = idUsuario },
            outParams: new() { ["p_exito"] = MySqlDbType.Byte, ["p_mensaje"] = MySqlDbType.VarChar });

        if (Convert.ToInt32(result["p_exito"]) == 0)
            return BadRequest(new { mensaje = result["p_mensaje"] });

        return Ok(new { exito = true, mensaje = result["p_mensaje"] });
    }

    // ── Listado / Detalle ─────────────────────────────────────────────────────

    [HttpGet("api/GestionPartido")]
    public async Task<IActionResult> ListarMisPartidos([FromQuery] int id_deporte)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var rows = await SpHelper.QueryAsync(conn, "sp_gestion_listar_encuentros",
            new() { ["p_id_usuario"] = idUsuario, ["p_id_deporte"] = id_deporte });
        return Ok(rows);
    }

[HttpGet("api/GestionPartido/{id:int}")]
public async Task<IActionResult> DetallePartido(int id)
{
    var idUsuario = JwtHelper.GetUserId(HttpContext);
    using var conn = db.CreateConnection();
    var rows = await SpHelper.QueryAsync(conn, "sp_solicitud_obtener_detalle",
        new() { ["p_id_partido"] = id, ["p_id_usuario"] = idUsuario });

    var detalle = rows.FirstOrDefault();

    if (detalle == null)
        return NotFound(new { mensaje = "Partido no encontrado." });

    // ✅ Corregido: Se lee 'estado_partido' (ID numérico) o se valida contra el texto
    if (detalle.ContainsKey("estado_partido") && Convert.ToInt32(detalle["estado_partido"]) == 0)
        return StatusCode(403, new { mensaje = "Este partido ha sido cancelado y ya no está disponible." });

    return Ok(detalle);
}

    [HttpPost("api/GestionPartido/{id:int}/marcar-leido")]
    public async Task<IActionResult> MarcarLeido(int id)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_gestion_marcar_mensajes_leidos",
            inParams: new() { ["p_id_partido"] = id, ["p_id_usuario"] = idUsuario },
            outParams: new() { ["p_exito"] = MySqlDbType.Byte });
        return Ok(new { exito = Convert.ToInt32(result["p_exito"]) == 1 });
    }
}