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

    // NUEVO: POST api/Auth/facebook
    [HttpPost("facebook")]
    public async Task<IActionResult> FacebookLogin([FromBody] Dictionary<string, string> body)
    {
        var accessToken = body.GetValueOrDefault("accessToken");
        if (string.IsNullOrEmpty(accessToken))
            return BadRequest(new { mensaje = "Token no proporcionado" });

        // 1. Validar Token con la API de Facebook
        using var client = new HttpClient();
        var fbUrl = $"https://graph.facebook.com/me?fields=id,name,email,picture.type(large)&access_token={accessToken}";
        var response = await client.GetAsync(fbUrl);

        if (!response.IsSuccessStatusCode)
            return Unauthorized(new { mensaje = "Token de Facebook inválido." });

        // 2. Extraer los datos del perfil
        var fbData = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(fbData);
        var root = doc.RootElement;

        var email = root.TryGetProperty("email", out var emailProp) ? emailProp.GetString() : null;
        var name = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : "Usuario de Facebook";
        
        string? pictureUrl = null;
        if (root.TryGetProperty("picture", out var picProp) && picProp.TryGetProperty("data", out var dataProp) && dataProp.TryGetProperty("url", out var urlProp))
        {
            pictureUrl = urlProp.GetString();
        }

        if (string.IsNullOrEmpty(email))
            return BadRequest(new { mensaje = "Se requiere el correo electrónico de Facebook. Por favor acepta los permisos de correo." });

        using var conn = db.CreateConnection();
        
        // 3. Revisar si el correo es nuevo en nuestra base de datos
        var resultCheck = await SpHelper.ExecuteAsync(conn, "sp_auth_verificar_correo_disponible",
            inParams: new() { ["p_correo"] = email },
            outParams: new() { ["p_disponible"] = MySqlDbType.Byte });

        bool esNuevo = Convert.ToInt32(resultCheck["p_disponible"]) == 1;

        // 4. Si el usuario no existe, registrarlo con ID_Proveedor = 50 (Facebook)
        if (esNuevo)
        {
            var partesNombre = name.Split(' ', 2);
            var nombre = partesNombre[0];
            var apellidos = partesNombre.Length > 1 ? partesNombre[1] : "";

            var resultReg = await SpHelper.ExecuteAsync(conn, "sp_auth_registrar",
                inParams: new()
                {
                    ["p_nombre"]            = nombre,
                    ["p_apellidos"]         = apellidos,
                    ["p_correo"]            = email,
                    ["p_contrasena_hash"]   = null,
                    ["p_id_proveedor_auth"] = 50,
                    ["p_telefono"]          = null,
                    ["p_direccion"]         = null,
                    ["p_foto_perfil_url"]   = pictureUrl,
                    ["p_id_pais"]           = null,
                    ["p_id_ciudad"]         = null,
                    ["p_id_distrito"]       = null,
                    ["p_es_profesor"]       = 0
                },
                outParams: new()
                {
                    ["p_id_usuario_nuevo"] = MySqlDbType.Int32,
                    ["p_exito"]            = MySqlDbType.Byte,
                    ["p_mensaje"]          = MySqlDbType.VarChar
                });

            if (Convert.ToInt32(resultReg["p_exito"]) == 0)
                return BadRequest(new { mensaje = "Error al crear cuenta: " + resultReg["p_mensaje"] });
        }

        // 5. Iniciar sesión cruzando el SP nativo (obtiene el ID final y valida estado)
        var resultLogin = await SpHelper.ExecuteAsync(conn, "sp_auth_login_proveedor",
            inParams: new()
            {
                ["p_correo"]       = email,
                ["p_id_proveedor"] = 50
            },
            outParams: new()
            {
                ["p_id_usuario"] = MySqlDbType.Int32,
                ["p_es_nuevo"]   = MySqlDbType.Byte,
                ["p_exito"]      = MySqlDbType.Byte
            });

        if (Convert.ToInt32(resultLogin["p_exito"]) == 0)
            return Unauthorized(new { mensaje = "Error iniciando sesión en el sistema." });

        // 6. Generar token de aplicación
        var idUsuario = Convert.ToInt32(resultLogin["p_id_usuario"]);
        var token = JwtHelper.GenerateToken(idUsuario, email, config);

        return Ok(new { token, idUsuario, esNuevo });
    }

    // POST api/Auth/registrar
    [HttpPost("registrar")]
    public async Task<IActionResult> Registrar([FromBody] Dictionary<string, object?> body)
    {
        using var conn = db.CreateConnection();
        
        var correoBruto = ObtenerValor(body, "correo")?.ToString() ?? "";
        var correoLimpio = correoBruto.Trim().ToLower();

        var result = await SpHelper.ExecuteAsync(conn, "sp_auth_registrar",
            inParams: new()
            {
                ["p_nombre"]            = ObtenerValor(body, "nombre"),
                ["p_apellidos"]         = ObtenerValor(body, "apellidos"),
                ["p_correo"]            = correoLimpio,
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
        var correoLimpio = correo?.Trim().ToLower() ?? "";

        var result = await SpHelper.ExecuteAsync(conn, "sp_auth_verificar_correo_disponible",
            inParams: new() { ["p_correo"] = correoLimpio },
            outParams: new() { ["p_disponible"] = MySqlDbType.Byte });

        return Ok(new { disponible = Convert.ToInt32(result["p_disponible"]) == 1 });
    }

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