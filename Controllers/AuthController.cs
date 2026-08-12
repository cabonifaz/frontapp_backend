using AppFronton.Data;
using AppFronton.Helpers;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.Text.Json;

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
                ["p_correo"]       = ObtenerValor(body, "correo"),
                ["p_id_proveedor"] = ObtenerValor(body, "id_proveedor")
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
        var correo    = ObtenerValor(body, "correo")?.ToString() ?? "";
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
                ["p_nombre"]            = ObtenerValor(body, "nombre"),
                ["p_apellidos"]         = ObtenerValor(body, "apellidos"),
                ["p_correo"]            = ObtenerValor(body, "correo"),
                ["p_contrasena_hash"]   = ObtenerValor(body, "contrasena_hash"),
                ["p_id_proveedor_auth"] = ObtenerValor(body, "id_proveedor_auth"),
                ["p_telefono"]          = ObtenerValor(body, "telefono"),
                ["p_direccion"]         = ObtenerValor(body, "direccion"),
                ["p_foto_perfil_url"]  = ObtenerValor(body, "foto_perfil_url"),
                ["p_id_pais"]           = ObtenerValor(body, "id_pais"),
                ["p_id_ciudad"]         = ObtenerValor(body, "id_ciudad"),
                ["p_id_distrito"]       = ObtenerValor(body, "id_distrito"),
                ["p_es_profesor"]       = ObtenerValor(body, "es_profesor") ?? 0
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

    /// <summary>
    /// Convierte objetos deserializados desde JSON (JsonElement) a tipos nativos de C#
    /// para evitar el error System.NotSupportedException en MySqlConnector.
    /// </summary>
    private static object? ObtenerValor(Dictionary<string, object?> dict, string clave)
    {
        if (!dict.TryGetValue(clave, out var val) || val is null)
            return null;

        if (val is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt32(out var i) ? i : element.GetDouble(),
                JsonValueKind.True   => 1,
                JsonValueKind.False  => 0,
                JsonValueKind.Null   => null,
                _                    => element.GetRawText()
            };
        }

        return val;
    }
}