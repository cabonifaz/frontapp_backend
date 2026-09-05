using AppFronton.Data;
using AppFronton.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace AppFronton.Controllers;

[ApiController]
[Route("api/Clase")]
[Authorize]
public class ClaseController(AppDbContext db) : ControllerBase
{
    // GET api/Clase/profesores-disponibles?id_cancha=1&fecha=2026-02-15&hora=15:00&id_deporte=1
    [HttpGet("profesores-disponibles")]
    public async Task<IActionResult> ProfesoresDisponibles(
        [FromQuery] int id_deporte,
        [FromQuery] int id_cancha,
        [FromQuery] string fecha,
        [FromQuery] string hora)
    {
        var idAlumno = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var rows = await SpHelper.QueryAsync(conn, "sp_clase_buscar_profesores", new()
        {
            ["p_id_alumno"]  = idAlumno,
            ["p_id_deporte"] = id_deporte,
            ["p_id_cancha"]  = id_cancha,
            ["p_fecha"]      = fecha,
            ["p_hora"]       = hora
        });
        return Ok(rows);
    }

    // POST api/Clase/solicitar
    [HttpPost("solicitar")]
    public async Task<IActionResult> Solicitar([FromBody] Dictionary<string, object?> body)
    {
        var idAlumno = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_clase_solicitar",
            inParams: new()
            {
                ["p_id_alumno"]         = idAlumno,
                ["p_id_profesor"]       = body.GetValueOrDefault("id_profesor"),
                ["p_id_deporte"]        = body.GetValueOrDefault("id_deporte"),
                ["p_id_cancha"]         = body.GetValueOrDefault("id_cancha"),
                ["p_fecha"]             = body.GetValueOrDefault("fecha"),
                ["p_hora"]              = body.GetValueOrDefault("hora"),
                ["p_duracion_minutos"]  = body.GetValueOrDefault("duracion_minutos")
            },
            outParams: new()
            {
                ["p_id_clase_generada"] = MySqlDbType.Int32,
                ["p_exito"]             = MySqlDbType.Byte,
                ["p_mensaje"]           = MySqlDbType.VarChar
            });

        if (Convert.ToInt32(result["p_exito"]) == 0)
            return BadRequest(new { mensaje = result["p_mensaje"] });

        return Ok(new { idClaseGenerada = result["p_id_clase_generada"], exito = true, mensaje = result["p_mensaje"] });
    }

    // POST api/Clase/{id}/aceptar
    [HttpPost("{id:int}/aceptar")]
    public async Task<IActionResult> Aceptar(int id)
    {
        var idProfesor = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_clase_aceptar",
            inParams: new() { ["p_id_clase"] = id, ["p_id_profesor"] = idProfesor },
            outParams: new() { ["p_exito"] = MySqlDbType.Byte, ["p_mensaje"] = MySqlDbType.VarChar });

        if (Convert.ToInt32(result["p_exito"]) == 0)
            return BadRequest(new { mensaje = result["p_mensaje"] });

        return Ok(new { exito = true, mensaje = result["p_mensaje"] });
    }

    // POST api/Clase/{id}/completar
[HttpPost("{id:int}/completar")]
public async Task<IActionResult> Completar(int id)
{
    var idProfesor = JwtHelper.GetUserId(HttpContext);
    using var conn = db.CreateConnection();
    var result = await SpHelper.ExecuteAsync(conn, "sp_clase_completar",
        inParams: new() { ["p_id_clase"] = id, ["p_id_profesor"] = idProfesor },
        outParams: new()
        {
            ["p_exito"]          = MySqlDbType.Byte,
            ["p_mensaje"]        = MySqlDbType.VarChar,
            ["p_puntos_ganados"] = MySqlDbType.Decimal,
            ["p_nuevo_puntaje"]  = MySqlDbType.Decimal
        });

    if (Convert.ToInt32(result["p_exito"]) == 0)
        return BadRequest(new { mensaje = result["p_mensaje"] });

    return Ok(new
    {
        exito             = true,
        mensaje           = result["p_mensaje"],
        puntosGanados     = result["p_puntos_ganados"],
        nuevoPuntajeTotal = result["p_nuevo_puntaje"]
    });
}

// POST api/Clase/{id}/rechazar
    [HttpPost("{id:int}/rechazar")]
    public async Task<IActionResult> Rechazar(int id)
    {
        var idProfesor = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_clase_rechazar",
            inParams: new() { ["p_id_clase"] = id, ["p_id_profesor"] = idProfesor },
            outParams: new() { ["p_exito"] = MySqlDbType.Byte, ["p_mensaje"] = MySqlDbType.VarChar });

        if (Convert.ToInt32(result["p_exito"]) == 0)
            return BadRequest(new { mensaje = result["p_mensaje"] });

        return Ok(new { exito = true, mensaje = result["p_mensaje"] });
    }

    // POST api/Clase/{id}/cancelar
    [HttpPost("{id:int}/cancelar")]
    public async Task<IActionResult> Cancelar(int id)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();

        var result = await SpHelper.ExecuteAsync(conn, "sp_clase_cancelar",
            inParams: new()
            {
                ["p_id_clase"]   = id,
                ["p_id_usuario"] = idUsuario
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

    // POST api/Clase/{id}/feedback
    [HttpPost("{id:int}/feedback")]
    public async Task<IActionResult> DejarFeedback(int id, [FromBody] Dictionary<string, object?> body)
    {
        var idAlumno = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_clase_dejar_feedback",
            inParams: new()
            {
                ["p_id_clase"]      = id,
                ["p_id_alumno"]     = idAlumno,
                ["p_calificacion"]  = body.GetValueOrDefault("calificacion"),
                ["p_comentario"]    = body.GetValueOrDefault("comentario")
            },
            outParams: new() { ["p_exito"] = MySqlDbType.Byte, ["p_mensaje"] = MySqlDbType.VarChar });

        if (Convert.ToInt32(result["p_exito"]) == 0)
            return BadRequest(new { mensaje = result["p_mensaje"] });

        return Ok(new { exito = true, mensaje = result["p_mensaje"] });
    }

    // GET api/Clase/solicitudes-profesor?id_deporte=1
    [HttpGet("solicitudes-profesor")]
    public async Task<IActionResult> SolicitudesProfesor([FromQuery] int id_deporte)
    {
        var idProfesor = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var rows = await SpHelper.QueryAsync(conn, "sp_clase_solicitudes_profesor",
            new() { ["p_id_profesor"] = idProfesor, ["p_id_deporte"] = id_deporte });
        return Ok(rows);
    }

    // GET api/Clase/mis-clases?id_deporte=1
    [HttpGet("mis-clases")]
    public async Task<IActionResult> MisClases([FromQuery] int id_deporte)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var rows = await SpHelper.QueryAsync(conn, "sp_clase_listar_mis_clases",
            new() { ["p_id_usuario"] = idUsuario, ["p_id_deporte"] = id_deporte });
        return Ok(rows);
    }

    // GET api/Clase/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detalle(int id)
    {
        using var conn = db.CreateConnection();
        var rows = await SpHelper.QueryAsync(conn, "sp_clase_obtener_detalle",
            new() { ["p_id_clase"] = id });
        return Ok(rows.FirstOrDefault());
    }
}
