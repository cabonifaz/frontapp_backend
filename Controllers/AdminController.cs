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

    // NUEVO (08) ─ GET api/Admin/configuracion   (solo lo necesario, agrupado por secciones)
    [HttpGet("configuracion")]
    public async Task<IActionResult> Configuracion()
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        if (!await AdminHelper.EsAdminAsync(conn, idUsuario)) return SinPermiso();
        return Ok(await SpHelper.QueryAsync(conn, "sp_admin_configuracion_listar"));
    }

    // NUEVO (08) ─ GET api/Admin/ligas
    [HttpGet("ligas")]
    public async Task<IActionResult> Ligas()
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        if (!await AdminHelper.EsAdminAsync(conn, idUsuario)) return SinPermiso();
        return Ok(await SpHelper.QueryAsync(conn, "sp_admin_liga_listar"));
    }

    // NUEVO (08) ─ POST api/Admin/ligas   (sin id_liga = crear; con id_liga = editar)
    [HttpPost("ligas")]
    public async Task<IActionResult> GuardarLiga([FromBody] Dictionary<string, object?> body)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        if (!await AdminHelper.EsAdminAsync(conn, idUsuario)) return SinPermiso();

        var result = await SpHelper.ExecuteAsync(conn, "sp_admin_liga_guardar",
            inParams: new()
            {
                ["p_id_liga"]            = body.GetValueOrDefault("id_liga"),
                ["p_nombre"]             = body.GetValueOrDefault("nombre"),
                ["p_descripcion"]        = body.GetValueOrDefault("descripcion"),
                ["p_es_dobles"]          = body.GetValueOrDefault("es_dobles"),
                ["p_estado_liga"]        = body.GetValueOrDefault("estado_liga"),
                ["p_nivel_min"]          = body.GetValueOrDefault("nivel_min"),
                ["p_nivel_max"]          = body.GetValueOrDefault("nivel_max"),
                ["p_fecha_inicio"]       = body.GetValueOrDefault("fecha_inicio"),
                ["p_fecha_fin"]          = body.GetValueOrDefault("fecha_fin"),
                ["p_lugar"]              = body.GetValueOrDefault("lugar"),
                ["p_cupo_maximo"]        = body.GetValueOrDefault("cupo_maximo"),
                ["p_cuota"]              = body.GetValueOrDefault("cuota_inscripcion"),
                ["p_premio"]             = body.GetValueOrDefault("premio"),
                ["p_instrucciones_pago"] = body.GetValueOrDefault("instrucciones_pago"),
                ["p_imagen_url"]         = body.GetValueOrDefault("imagen_url"),
                ["p_visible"]            = body.GetValueOrDefault("visible"),
                ["p_auspiciador_nombre"] = body.GetValueOrDefault("auspiciador_nombre"),
                ["p_auspiciador_logo"]   = body.GetValueOrDefault("auspiciador_logo_url"),
                ["p_auspiciador_web"]    = body.GetValueOrDefault("auspiciador_sitio_web"),
                ["p_id_deporte"]         = body.GetValueOrDefault("id_deporte")
            },
            outParams: new()
            {
                ["p_id_liga_guardada"] = MySqlDbType.Int32,
                ["p_exito"]            = MySqlDbType.Byte,
                ["p_mensaje"]          = MySqlDbType.VarChar
            });

        if (Convert.ToInt32(result["p_exito"]) == 0)
            return BadRequest(new { mensaje = result["p_mensaje"] });
        return Ok(new { exito = true, mensaje = result["p_mensaje"], idLiga = result["p_id_liga_guardada"] });
    }

    // NUEVO (08) ─ GET api/Admin/ligas/{idLiga}/participantes
    [HttpGet("ligas/{idLiga:int}/participantes")]
    public async Task<IActionResult> ParticipantesLiga(int idLiga)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        if (!await AdminHelper.EsAdminAsync(conn, idUsuario)) return SinPermiso();
        return Ok(await SpHelper.QueryAsync(conn, "sp_admin_liga_participantes", new() { ["p_id_liga"] = idLiga }));
    }

    private ObjectResult SinPermiso() =>
        StatusCode(403, new { mensaje = "Esta opción es solo para administradores." });
}
