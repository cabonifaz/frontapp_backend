using AppFronton.Data;
using AppFronton.Helpers;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace AppFronton.Controllers;

[ApiController]
[Route("api/Auth")]
public class AuthController(AppDbContext db, IConfiguration config) : ControllerBase
{
    // POST api/Auth/login
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] Dictionary<string, string> body)
    {
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_auth_login",
            inParams: new()
            {
                ["p_correo"]          = body.GetValueOrDefault("correo"),
                ["p_contrasena_hash"] = body.GetValueOrDefault("contrasena_hash")
            },
            outParams: new()
            {
                ["p_id_usuario"]     = MySqlDbType.Int32,
                ["p_exito"]          = MySqlDbType.Byte,
                ["p_mensaje"]        = MySqlDbType.VarChar,
                ["p_token_payload"]  = MySqlDbType.Text
            });

        if (Convert.ToInt32(result["p_exito"]) == 0)
            return Unauthorized(new { mensaje = result["p_mensaje"] });

        var idUsuario = Convert.ToInt32(result["p_id_usuario"]);
        var correo    = body.GetValueOrDefault("correo") ?? "";
        var token     = JwtHelper.GenerateToken(idUsuario, correo, config);

        return Ok(new { token, idUsuario });
    }

    // POST api/Auth/login-social
    [HttpPost("login-social")]
    public async Task<IActionResult> LoginSocial([FromBody] Dictionary<string, object?> body)
    {
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_auth_login_proveedor",
            inParams: new()
            {
                ["p_correo"]      = body.GetValueOrDefault("correo"),
                ["p_id_proveedor"] = body.GetValueOrDefault("id_proveedor")
            },
            outParams: new()
            {
                ["p_id_usuario"] = MySqlDbType.Int32,
                ["p_es_nuevo"]   = MySqlDbType.Byte,
                ["p_exito"]      = MySqlDbType.Byte
            });

        if (Convert.ToInt32(result["p_exito"]) == 0)
            return Unauthorized(new { mensaje = "No se pudo autenticar con el proveedor." });

        var idUsuario = Convert.ToInt32(result["p_id_usuario"]);
        var correo    = body.GetValueOrDefault("correo")?.ToString() ?? "";
        var token     = JwtHelper.GenerateToken(idUsuario, correo, config);
        var esNuevo   = Convert.ToInt32(result["p_es_nuevo"]) == 1;

        return Ok(new { token, idUsuario, esNuevo });
    }

    // POST api/Auth/registrar
    [HttpPost("registrar")]
    public async Task<IActionResult> Registrar([FromBody] Dictionary<string, object?> body)
    {
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_auth_registrar",
            inParams: new()
            {
                ["p_nombre"]           = body.GetValueOrDefault("nombre"),
                ["p_apellidos"]        = body.GetValueOrDefault("apellidos"),
                ["p_correo"]           = body.GetValueOrDefault("correo"),
                ["p_contrasena_hash"]  = body.GetValueOrDefault("contrasena_hash"),
                ["p_id_proveedor_auth"]= body.GetValueOrDefault("id_proveedor_auth"),
                ["p_telefono"]         = body.GetValueOrDefault("telefono"),
                ["p_id_pais"]          = body.GetValueOrDefault("id_pais"),
                ["p_id_ciudad"]        = body.GetValueOrDefault("id_ciudad"),
                ["p_id_distrito"]      = body.GetValueOrDefault("id_distrito"),
                ["p_es_profesor"]      = body.GetValueOrDefault("es_profesor") ?? 0
            },
            outParams: new()
            {
                ["p_id_usuario_nuevo"] = MySqlDbType.Int32,
                ["p_exito"]            = MySqlDbType.Byte,
                ["p_mensaje"]          = MySqlDbType.VarChar
            });

        if (Convert.ToInt32(result["p_exito"]) == 0)
            return BadRequest(new { mensaje = result["p_mensaje"] });

        return Ok(new { idUsuarioNuevo = result["p_id_usuario_nuevo"], exito = true, mensaje = result["p_mensaje"] });
    }

    // GET api/Auth/verificar-correo?correo=xxx
    [HttpGet("verificar-correo")]
    public async Task<IActionResult> VerificarCorreo([FromQuery] string correo)
    {
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_auth_verificar_correo_disponible",
            inParams: new() { ["p_correo"] = correo },
            outParams: new() { ["p_disponible"] = MySqlDbType.Byte });

        return Ok(new { disponible = Convert.ToInt32(result["p_disponible"]) == 1 });
    }
}
