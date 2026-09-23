using AppFronton.Data;
using AppFronton.Helpers;
using AppFronton.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace AppFronton.Controllers;

[ApiController]
[Route("api/Amistad")]
[Authorize]
public class AmistadController(AppDbContext db) : ControllerBase
{
    // GET api/Amistad?id_deporte=17
    [HttpGet]
    public async Task<IActionResult> ListarAmigos([FromQuery] int id_deporte)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var rows = await SpHelper.QueryAsync(conn, "sp_amistad_listar_amigos",
            new() { ["p_id_usuario"] = idUsuario, ["p_id_deporte"] = id_deporte });
        return Ok(rows);
    }

    // GET api/Amistad/solicitudes-recibidas?id_deporte=17
    [HttpGet("solicitudes-recibidas")]
    public async Task<IActionResult> SolicitudesRecibidas([FromQuery] int id_deporte)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var rows = await SpHelper.QueryAsync(conn, "sp_amistad_listar_solicitudes_recibidas",
            new() { ["p_id_usuario"] = idUsuario, ["p_id_deporte"] = id_deporte });
        return Ok(rows);
    }

    // GET api/Amistad/estado/{idOtro}
    // estado: MISMO_USUARIO | NINGUNA | PENDIENTE_ENVIADA | PENDIENTE_RECIBIDA | AMIGOS
    [HttpGet("estado/{idOtro:int}")]
    public async Task<IActionResult> Estado(int idOtro)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_amistad_obtener_estado",
            inParams: new() { ["p_id_usuario"] = idUsuario, ["p_id_otro"] = idOtro },
            outParams: new()
            {
                ["p_estado"]     = MySqlDbType.VarChar,
                ["p_id_amistad"] = MySqlDbType.Int32
            });

        return Ok(new { estado = result["p_estado"], idAmistad = result["p_id_amistad"] });
    }

    // POST api/Amistad/solicitud/{idReceptor}
    [HttpPost("solicitud/{idReceptor:int}")]
    public async Task<IActionResult> EnviarSolicitud(int idReceptor)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_amistad_enviar_solicitud",
            inParams: new() { ["p_id_solicitante"] = idUsuario, ["p_id_receptor"] = idReceptor },
            outParams: new()
            {
                ["p_exito"]   = MySqlDbType.Byte,
                ["p_mensaje"] = MySqlDbType.VarChar,
                ["p_estado"]  = MySqlDbType.VarChar
            });

        if (Convert.ToInt32(result["p_exito"]) == 0)
            return BadRequest(new { mensaje = result["p_mensaje"], estado = result["p_estado"] });

        return Ok(new { exito = true, mensaje = result["p_mensaje"], estado = result["p_estado"] });
    }

    // POST api/Amistad/{idAmistad}/responder   body: { "aceptar": true }
    [HttpPost("{idAmistad:int}/responder")]
    public async Task<IActionResult> Responder(int idAmistad, [FromBody] AceptarDto dto)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_amistad_responder",
            inParams: new()
            {
                ["p_id_amistad"] = idAmistad,
                ["p_id_usuario"] = idUsuario,
                ["p_aceptar"]    = dto.Aceptar ? 1 : 0
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

    // DELETE api/Amistad/{idOtro}
    // Elimina la amistad o cancela una solicitud enviada pendiente.
    [HttpDelete("{idOtro:int}")]
    public async Task<IActionResult> Eliminar(int idOtro)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_amistad_eliminar",
            inParams: new() { ["p_id_usuario"] = idUsuario, ["p_id_otro"] = idOtro },
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