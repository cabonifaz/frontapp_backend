using AppFronton.Data;
using AppFronton.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace AppFronton.Controllers;

[ApiController]
[Route("api/Maestro")]
public class MaestroController(AppDbContext db) : ControllerBase
{
    // GET api/Maestro/categoria?codigo=CAT_DEPORTE
    [HttpGet("categoria")]
    public async Task<IActionResult> ObtenerPorCategoria([FromQuery] string codigo)
    {
        using var conn = db.CreateConnection();
        var rows = await SpHelper.QueryAsync(conn, "sp_maestro_obtener_por_categoria",
            new() { ["p_codigo_categoria"] = codigo });
        return Ok(rows);
    }

    // GET api/Maestro/deportes
    [HttpGet("deportes")]
    public async Task<IActionResult> Deportes()
    {
        using var conn = db.CreateConnection();
        var rows = await SpHelper.QueryAsync(conn, "sp_maestro_obtener_por_categoria",
            new() { ["p_codigo_categoria"] = "CAT_DEPORTE" });
        return Ok(rows);
    }

    // GET api/Maestro/canchas?busqueda=terrazas
    [HttpGet("canchas")]
    public async Task<IActionResult> Canchas([FromQuery] string? busqueda)
    {
        using var conn = db.CreateConnection();
        var rows = await SpHelper.QueryAsync(conn, "sp_maestro_obtener_por_categoria",
            new() { ["p_codigo_categoria"] = "CAT_CANCHA" });

        if (!string.IsNullOrEmpty(busqueda))
            rows = rows.Where(r => r["nombre"]?.ToString()
                       ?.Contains(busqueda, StringComparison.OrdinalIgnoreCase) == true).ToList();

        return Ok(rows);
    }

    // POST api/Maestro/canchas
    [Authorize]
    [HttpPost("canchas")]
    public async Task<IActionResult> InsertarCancha([FromBody] Dictionary<string, object?> body)
    {
        using var conn = db.CreateConnection();
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        var result = await SpHelper.ExecuteAsync(conn, "sp_maestro_insertar_cancha",
            inParams: new()
            {
                ["p_nombre_cancha"]      = body.GetValueOrDefault("nombre"),
                ["p_id_usuario_creador"] = idUsuario
            },
            outParams: new()
            {
                ["p_id_maestro_nuevo"] = MySqlDbType.Int32,
                ["p_exito"]            = MySqlDbType.Byte,
                ["p_mensaje"]          = MySqlDbType.VarChar
            });

        if (Convert.ToInt32(result["p_exito"]) == 0)
            return BadRequest(new { mensaje = result["p_mensaje"] });

        return Ok(new { idMaestroNuevo = result["p_id_maestro_nuevo"], exito = true });
    }

    // GET api/Maestro/configuraciones
    [HttpGet("configuraciones")]
    public async Task<IActionResult> ListarConfiguraciones()
    {
        using var conn = db.CreateConnection();
        var rows = await SpHelper.QueryAsync(conn, "sp_maestro_listar_configuraciones");
        return Ok(rows);
    }

    // PUT api/Maestro/configuraciones
    // SP: sp_maestro_actualizar_configuracion(p_codigo, p_valor, p_valor_numerico, p_id_usuario → OUT p_exito, p_mensaje)
    [Authorize]
    [HttpPut("configuraciones")]
    public async Task<IActionResult> ActualizarConfiguracion([FromBody] Dictionary<string, object?> body)
    {
        using var conn = db.CreateConnection();
        var idUsuario = JwtHelper.GetUserId(HttpContext); // ← faltaba; el SP lo pide como IN p_id_usuario
        var result = await SpHelper.ExecuteAsync(conn, "sp_maestro_actualizar_configuracion",
            inParams: new()
            {
                ["p_codigo"]         = body.GetValueOrDefault("codigo"),
                ["p_valor"]          = body.GetValueOrDefault("valor"),
                ["p_valor_numerico"] = body.GetValueOrDefault("valor_numerico"),
                ["p_id_usuario"]     = idUsuario   // ← parámetro que faltaba
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