using AppFronton.Data;
using AppFronton.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace AppFronton.Controllers;

/// <summary>
/// NUEVO (07): funciones del perfil administrador.
/// Una cuenta es admin cuando usuario.es_admin = 1 (se marca por base de datos).
/// Todas las rutas, salvo "yo", responden 403 a quien no es admin.
/// </summary>
[ApiController]
[Route("api/Admin")]
[Authorize]
public class AdminController(AppDbContext db) : ControllerBase
{
    // GET api/Admin/yo  ->  { esAdmin: true|false }
    [HttpGet("yo")]
    public async Task<IActionResult> Yo()
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        return Ok(new { esAdmin = await AdminHelper.EsAdminAsync(conn, idUsuario) });
    }

    // GET api/Admin/inscripciones?filtro=PENDIENTES|ACTIVAS|TODAS
    [HttpGet("inscripciones")]
    public async Task<IActionResult> Inscripciones([FromQuery] string? filtro = "PENDIENTES")
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        if (!await AdminHelper.EsAdminAsync(conn, idUsuario)) return SinPermiso();

        var rows = await SpHelper.QueryAsync(conn, "sp_admin_listar_inscripciones",
            new() { ["p_filtro"] = (filtro ?? "PENDIENTES").ToUpperInvariant() });
        return Ok(rows);
    }

    // POST api/Admin/inscripciones/{id}/validar   body: { monto: 30, referencia: "Yape 123" }  (monto vacío = cuota de la liga)
    [HttpPost("inscripciones/{id:int}/validar")]
    public async Task<IActionResult> ValidarPago(int id, [FromBody] Dictionary<string, object?>? body)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        if (!await AdminHelper.EsAdminAsync(conn, idUsuario)) return SinPermiso();

        var result = await SpHelper.ExecuteAsync(conn, "sp_admin_validar_pago",
            inParams: new()
            {
                ["p_id_liga_inscripcion"] = id,
                ["p_monto"]               = body?.GetValueOrDefault("monto"),
                ["p_referencia"]          = body?.GetValueOrDefault("referencia"),
                ["p_id_admin"]            = idUsuario
            },
            outParams: new() { ["p_exito"] = MySqlDbType.Byte, ["p_mensaje"] = MySqlDbType.VarChar });

        if (Convert.ToInt32(result["p_exito"]) == 0)
            return BadRequest(new { mensaje = result["p_mensaje"] });
        return Ok(new { exito = true, mensaje = result["p_mensaje"] });
    }

    // POST api/Admin/inscripciones/{id}/rechazar
    [HttpPost("inscripciones/{id:int}/rechazar")]
    public async Task<IActionResult> Rechazar(int id)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        if (!await AdminHelper.EsAdminAsync(conn, idUsuario)) return SinPermiso();

        var result = await SpHelper.ExecuteAsync(conn, "sp_admin_rechazar_inscripcion",
            inParams: new() { ["p_id_liga_inscripcion"] = id, ["p_id_admin"] = idUsuario },
            outParams: new() { ["p_exito"] = MySqlDbType.Byte, ["p_mensaje"] = MySqlDbType.VarChar });

        if (Convert.ToInt32(result["p_exito"]) == 0)
            return BadRequest(new { mensaje = result["p_mensaje"] });
        return Ok(new { exito = true, mensaje = result["p_mensaje"] });
    }

    private ObjectResult SinPermiso() =>
        StatusCode(403, new { mensaje = "Esta opción es solo para administradores." });
}
