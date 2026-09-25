using AppFronton.Data;
using AppFronton.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace AppFronton.Controllers;

/// <summary>
/// Ranking por ligas. Solo lectura de ligas/auspiciadores: su administración
/// se hace directamente en la base de datos (ver ADMIN_LIGAS.md).
/// </summary>
[ApiController]
[Route("api/Liga")]
[Authorize]
public class LigaController(AppDbContext db) : ControllerBase
{
    // GET api/Liga?id_deporte=17&id_tipo_juego=45
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] int id_deporte, [FromQuery] int? id_tipo_juego)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var rows = await SpHelper.QueryAsync(conn, "sp_liga_listar", new()
        {
            ["p_id_usuario"]    = idUsuario,
            ["p_id_deporte"]    = id_deporte,
            ["p_id_tipo_juego"] = id_tipo_juego
        });
        return Ok(rows);
    }

    // GET api/Liga/{idLiga}
    [HttpGet("{idLiga:int}")]
    public async Task<IActionResult> Detalle(int idLiga)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var rows = await SpHelper.QueryAsync(conn, "sp_liga_obtener_detalle",
            new() { ["p_id_liga"] = idLiga, ["p_id_usuario"] = idUsuario });

        var liga = rows.FirstOrDefault();
        if (liga == null) return NotFound(new { mensaje = "Liga no encontrada." });
        return Ok(liga);
    }

    // GET api/Liga/{idLiga}/tabla  → posiciones + motivo_bloqueo por fila
    [HttpGet("{idLiga:int}/tabla")]
    public async Task<IActionResult> Tabla(int idLiga)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var rows = await SpHelper.QueryAsync(conn, "sp_liga_tabla",
            new() { ["p_id_liga"] = idLiga, ["p_id_usuario"] = idUsuario });
        return Ok(rows);
    }

    // GET api/Liga/{idLiga}/partidos
    [HttpGet("{idLiga:int}/partidos")]
    public async Task<IActionResult> Partidos(int idLiga)
    {
        using var conn = db.CreateConnection();
        var rows = await SpHelper.QueryAsync(conn, "sp_liga_listar_partidos",
            new() { ["p_id_liga"] = idLiga });
        return Ok(rows);
    }

    // POST api/Liga/{idLiga}/inscribirse
    [HttpPost("{idLiga:int}/inscribirse")]
    public async Task<IActionResult> Inscribirse(int idLiga)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_liga_inscribirse",
            inParams: new() { ["p_id_liga"] = idLiga, ["p_id_usuario"] = idUsuario },
            outParams: new()
            {
                ["p_exito"]              = MySqlDbType.Byte,
                ["p_mensaje"]            = MySqlDbType.VarChar,
                ["p_estado_inscripcion"] = MySqlDbType.VarChar
            });

        if (Convert.ToInt32(result["p_exito"]) == 0)
            return BadRequest(new { mensaje = result["p_mensaje"], estadoInscripcion = result["p_estado_inscripcion"] });

        return Ok(new { exito = true, mensaje = result["p_mensaje"], estadoInscripcion = result["p_estado_inscripcion"] });
    }

    // POST api/Liga/{idLiga}/retar   body: { id_rival, id_cancha, fecha, hora }
    [HttpPost("{idLiga:int}/retar")]
    public async Task<IActionResult> Retar(int idLiga, [FromBody] Dictionary<string, object?> body)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_liga_crear_reto",
            inParams: new()
            {
                ["p_id_liga"]    = idLiga,
                ["p_id_usuario"] = idUsuario,
                ["p_id_rival"]   = body.GetValueOrDefault("id_rival"),
                ["p_id_cancha"]  = body.GetValueOrDefault("id_cancha"),
                ["p_fecha"]      = body.GetValueOrDefault("fecha"),
                ["p_hora"]       = body.GetValueOrDefault("hora")
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
}