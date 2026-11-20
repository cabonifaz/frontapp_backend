using AppFronton.Data;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace AppFronton.Controllers;

// Diagnóstico del servidor: dice qué variables de entorno existen (sin mostrar sus valores)
// y si el backend logra conectarse a MySQL. No devuelve contraseñas ni secretos.
// GET api/Health
[ApiController]
[Route("api/Health")]
public class HealthController(AppDbContext db, IConfiguration config) : ControllerBase
{
    private static readonly string[] Variables =
    {
        "DB_HOST", "DB_PORT", "DB_NAME", "DB_USER", "DB_PASSWORD",
        "JWT_SECRET", "JWT_ISSUER", "JWT_AUDIENCE", "JWT_EXPIRES_HOURS",
        "MONGODB_URI", "MONGODB_DB", "PUSH_WORKER"
    };

    // Público: solo "ok" / "error" (sirve para el monitoreo del hosting). No revela nada interno.
    // Detalle completo: GET api/Health?clave=<valor de la variable HEALTH_KEY> (solo si HEALTH_KEY está definida).
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? clave = null)
    {
        var jwt = config["JWT_SECRET"] ?? "";
        string baseDatos;
        bool dbOk;
        try
        {
            using var conn = db.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("SELECT DATABASE()", conn);
            baseDatos = $"OK: conectado a {await cmd.ExecuteScalarAsync()}";
            dbOk = true;
        }
        catch (Exception ex)
        {
            baseDatos = $"ERROR: {ex.GetType().Name}: {ex.Message}";
            dbOk = false;
        }

        var claveDetalle = config["HEALTH_KEY"];
        var verDetalle = !string.IsNullOrWhiteSpace(claveDetalle) && clave == claveDetalle;

        if (!verDetalle)
            return StatusCode(dbOk ? 200 : 503, new { status = dbOk ? "ok" : "error" });

        var variables = Variables.ToDictionary(v => v, v => !string.IsNullOrWhiteSpace(config[v]));
        return StatusCode(dbOk ? 200 : 503, new
        {
            status = dbOk ? "ok" : "error",
            variables,
            jwtSecretSuficiente = System.Text.Encoding.UTF8.GetByteCount(jwt) >= 32,
            dbHost  = config["DB_HOST"],
            dbName  = config["DB_NAME"],
            baseDatos
        });
    }
}
