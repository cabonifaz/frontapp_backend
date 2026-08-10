using AppFronton.Data;
using AppFronton.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace AppFronton.Controllers;

[ApiController]
[Route("api/Resultado")]
[Authorize]
public class ResultadoController(AppDbContext db) : ControllerBase
{
    // GET api/Resultado?filtro_fecha=2026-02-12&busqueda_nombre=Renato
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string? filtro_fecha,
        [FromQuery] string? busqueda_nombre)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var rows = await SpHelper.QueryAsync(conn, "sp_resultado_listar_historial", new()
        {
            ["p_id_usuario"]      = idUsuario,
            ["p_filtro_fecha"]    = filtro_fecha,
            ["p_busqueda_nombre"] = busqueda_nombre
        });
        return Ok(rows);
    }

    // GET api/Resultado/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detalle(int id)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var rows = await SpHelper.QueryAsync(conn, "sp_resultado_obtener_detalle",
            new() { ["p_id_partido"] = id, ["p_id_usuario"] = idUsuario });
        return Ok(rows.FirstOrDefault());
    }
}

// ─────────────────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/GestionResultado")]
[Authorize]
public class GestionResultadoController(AppDbContext db) : ControllerBase
{
    // POST api/GestionResultado/{idPartido}/publicar
    [HttpPost("{idPartido:int}/publicar")]
    public async Task<IActionResult> Publicar(int idPartido, [FromBody] Dictionary<string, object?> body)
    {
        var idJugadorLocal = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_resultado_publicar",
            inParams: new()
            {
                ["p_id_partido"]        = idPartido,
                ["p_id_jugador_local"]  = idJugadorLocal,
                ["p_id_rival"]          = body.GetValueOrDefault("id_rival"),
                ["p_calificacion_rival"]= body.GetValueOrDefault("calificacion_rival"),
                ["p_comentario"]        = body.GetValueOrDefault("comentario"),
                ["p_sets"]              = body.GetValueOrDefault("sets")?.ToString() // JSON string
            },
            outParams: new()
            {
                ["p_id_resultado"] = MySqlDbType.Int32,
                ["p_exito"]        = MySqlDbType.Byte,
                ["p_mensaje"]      = MySqlDbType.VarChar,
                ["p_estado"]       = MySqlDbType.VarChar
            });

        if (Convert.ToInt32(result["p_exito"]) == 0)
            return BadRequest(new { mensaje = result["p_mensaje"] });

        return Ok(new
        {
            idResultado = result["p_id_resultado"],
            exito       = true,
            mensaje     = result["p_mensaje"],
            estado      = result["p_estado"]
        });
    }

    // POST api/GestionResultado/{idPartido}/confirmar
    [HttpPost("{idPartido:int}/confirmar")]
    public async Task<IActionResult> Confirmar(int idPartido, [FromBody] Dictionary<string, object?> body)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_resultado_confirmar",
            inParams: new()
            {
                ["p_id_partido"]      = idPartido,
                ["p_id_usuario"]      = idUsuario,
                ["p_esta_de_acuerdo"] = body.GetValueOrDefault("esta_de_acuerdo"),
                ["p_id_rival"]        = body.GetValueOrDefault("id_rival"),
                ["p_sets"]            = body.GetValueOrDefault("sets")?.ToString()
            },
            outParams: new()
            {
                ["p_exito"]             = MySqlDbType.Byte,
                ["p_estado"]            = MySqlDbType.VarChar,
                ["p_puntos_transferidos"]= MySqlDbType.Decimal
            });

        if (Convert.ToInt32(result["p_exito"]) == 0)
            return BadRequest(new { mensaje = "Error al confirmar resultado." });

        return Ok(new
        {
            exito             = true,
            estado            = result["p_estado"],
            puntosTransferidos= result["p_puntos_transferidos"]
        });
    }
}
