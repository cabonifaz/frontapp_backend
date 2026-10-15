using AppFronton.Data;
using AppFronton.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace AppFronton.Controllers;

[ApiController]
[Route("api/Usuario")]
[Authorize]
public class UsuarioController(AppDbContext db) : ControllerBase
{
    // GET api/Usuario/menu-principal?id_deporte=1
    [HttpGet("menu-principal")]
    public async Task<IActionResult> MenuPrincipal([FromQuery] int id_deporte)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var rows = await SpHelper.QueryAsync(conn, "sp_usuario_obtener_menu_principal",
            new() { ["p_id_usuario"] = idUsuario, ["p_id_deporte"] = id_deporte });
        return Ok(rows.FirstOrDefault());
    }

    // POST api/Usuario/cuestionario
    [HttpPost("cuestionario")]
    public async Task<IActionResult> Cuestionario([FromBody] Dictionary<string, object?> body)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_usuario_completar_cuestionario",
            inParams: new()
            {
                ["p_id_usuario"]         = idUsuario,
                ["p_id_deporte"]         = body.GetValueOrDefault("id_deporte"),
                ["p_nivel_fisico"]       = body.GetValueOrDefault("nivel_fisico"),
                ["p_nivel_juego"]        = body.GetValueOrDefault("nivel_juego"),
                ["p_partidos_semanales"] = body.GetValueOrDefault("partidos_semanales"),
                ["p_lecciones_semanales"]= body.GetValueOrDefault("lecciones_semanales"),
                ["p_edad"]               = body.GetValueOrDefault("edad"),
                ["p_id_genero"]          = body.GetValueOrDefault("id_genero")
            },
            outParams: new()
            {
                ["p_nivel_calculado"]  = MySqlDbType.Int32,
                ["p_puntaje_inicial"]  = MySqlDbType.Decimal,
                ["p_exito"]            = MySqlDbType.Byte,
                ["p_mensaje"]          = MySqlDbType.VarChar
            });

        if (Convert.ToInt32(result["p_exito"]) == 0)
            return BadRequest(new { mensaje = result["p_mensaje"] });

        return Ok(new
        {
            nivelCalculado = result["p_nivel_calculado"],
            puntajeInicial = result["p_puntaje_inicial"],
            exito = true,
            mensaje = result["p_mensaje"]
        });
    }

    // PUT api/Usuario/foto-perfil
    [HttpPut("foto-perfil")]
    public async Task<IActionResult> ActualizarFoto([FromBody] Dictionary<string, object?> body)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_usuario_actualizar_foto",
            inParams: new()
            {
                ["p_id_usuario"]     = idUsuario,
                ["p_foto_perfil_url"]= body.GetValueOrDefault("foto_perfil_url")
            },
            outParams: new() { ["p_exito"] = MySqlDbType.Byte });

        return Ok(new { exito = Convert.ToInt32(result["p_exito"]) == 1 });
    }

    // GET api/Usuario/perfil?id_deporte=1
    [HttpGet("perfil")]
    public async Task<IActionResult> ObtenerPerfil([FromQuery] int id_deporte)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var rows = await SpHelper.QueryAsync(conn, "sp_usuario_obtener_perfil_publico",
            new() { ["p_id_usuario"] = idUsuario, ["p_id_deporte"] = id_deporte });
        return Ok(rows.FirstOrDefault());
    }

    // GET api/Usuario/deportes
    [HttpGet("deportes")]
    public async Task<IActionResult> ListarDeportes()
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var rows = await SpHelper.QueryAsync(conn, "sp_usuario_listar_deportes",
            new() { ["p_id_usuario"] = idUsuario });
        return Ok(rows);
    }

    // GET api/Usuario/mis-resultados?pagina=1&tamano_pagina=10
    [HttpGet("mis-resultados")]
    public async Task<IActionResult> MisResultados([FromQuery] int pagina = 1, [FromQuery] int tamano_pagina = 10)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var rows = await SpHelper.QueryAsync(conn, "sp_resultado_listar_historial",
            new() { ["p_id_usuario"] = idUsuario, ["p_filtro_fecha"] = null, ["p_busqueda_nombre"] = null });
        return Ok(rows);
    }

    // POST api/Usuario/perfil-profesor
    [HttpPost("perfil-profesor")]
    public async Task<IActionResult> CrearPerfilProfesor([FromBody] Dictionary<string, object?> body)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_usuario_crear_perfil_profesor",
            inParams: new()
            {
                ["p_id_usuario"]          = idUsuario,
                ["p_anios_experiencia"]   = body.GetValueOrDefault("anios_experiencia"),
                ["p_descripcion"]         = body.GetValueOrDefault("descripcion"),
                ["p_tarifa_referencial"]  = body.GetValueOrDefault("tarifa_referencial")
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

    // NUEVO: datos privados del usuario (para "Editar perfil" y Detalles de mi perfil)
    // GET api/Usuario/mis-datos
    [HttpGet("mis-datos")]
    public async Task<IActionResult> MisDatos()
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var rows = await SpHelper.QueryAsync(conn, "sp_usuario_obtener_mis_datos",
            new() { ["p_id_usuario"] = idUsuario });
        return Ok(rows.FirstOrDefault());
    }

    // NUEVO: guarda "Editar perfil"
    // PUT api/Usuario/perfil   body: { nombre, apellidos, telefono, fecha_nacimiento: "1995-06-20", sobre_mi }
    [HttpPut("perfil")]
    public async Task<IActionResult> ActualizarPerfil([FromBody] Dictionary<string, object?> body)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_usuario_actualizar_perfil",
            inParams: new()
            {
                ["p_id_usuario"]       = idUsuario,
                ["p_nombre"]           = body.GetValueOrDefault("nombre"),
                ["p_apellidos"]        = body.GetValueOrDefault("apellidos"),
                ["p_telefono"]         = body.GetValueOrDefault("telefono"),
                ["p_fecha_nacimiento"] = body.GetValueOrDefault("fecha_nacimiento"),
                ["p_sobre_mi"]         = body.GetValueOrDefault("sobre_mi")
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

    // NUEVO (APP-AVO-036)
    // PUT api/Usuario/mano-dominante   body: { "codigo": "MANO_DIESTRO" | "MANO_ZURDO" | "MANO_AMBIDIESTRO" }
    [HttpPut("mano-dominante")]
    public async Task<IActionResult> ActualizarManoDominante([FromBody] Dictionary<string, object?> body)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_usuario_actualizar_mano_dominante",
            inParams: new()
            {
                ["p_id_usuario"] = idUsuario,
                ["p_codigo"]     = body.GetValueOrDefault("codigo")
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
