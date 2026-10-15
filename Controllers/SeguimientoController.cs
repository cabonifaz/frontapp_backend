using AppFronton.Data;
using AppFronton.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace AppFronton.Controllers;

// NUEVO (APP-AVO-037): seguir jugadores para ver sus resultados sin retarlos.
// Es de una sola vía y no necesita aprobación. Si ya son amigos no hace falta seguir.
[ApiController]
[Route("api/Seguimiento")]
[Authorize]
public class SeguimientoController(AppDbContext db) : ControllerBase
{
    // GET api/Seguimiento/estado/{idOtro}  ->  { siguiendo: bool, seguidores: int }
    [HttpGet("estado/{idOtro:int}")]
    public async Task<IActionResult> Estado(int idOtro)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_seguimiento_estado",
            inParams: new() { ["p_id_usuario"] = idUsuario, ["p_id_otro"] = idOtro },
            outParams: new()
            {
                ["p_siguiendo"]  = MySqlDbType.Byte,
                ["p_seguidores"] = MySqlDbType.Int32
            });

        return Ok(new
        {
            siguiendo  = Convert.ToInt32(result["p_siguiendo"] ?? 0) == 1,
            seguidores = Convert.ToInt32(result["p_seguidores"] ?? 0)
        });
    }

    // GET api/Seguimiento/siguiendo?id_deporte=17  ->  jugadores que sigo
    [HttpGet("siguiendo")]
    public async Task<IActionResult> Siguiendo([FromQuery] int id_deporte)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var rows = await SpHelper.QueryAsync(conn, "sp_seguimiento_listar_siguiendo",
            new() { ["p_id_usuario"] = idUsuario, ["p_id_deporte"] = id_deporte });
        return Ok(rows);
    }

    // POST api/Seguimiento/{idSeguido}
    [HttpPost("{idSeguido:int}")]
    public async Task<IActionResult> Seguir(int idSeguido)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_seguimiento_seguir",
            inParams: new() { ["p_id_seguidor"] = idUsuario, ["p_id_seguido"] = idSeguido },
            outParams: new()
            {
                ["p_exito"]   = MySqlDbType.Byte,
                ["p_mensaje"] = MySqlDbType.VarChar
            });

        if (Convert.ToInt32(result["p_exito"]) == 0)
            return BadRequest(new { mensaje = result["p_mensaje"] });

        return Ok(new { exito = true, mensaje = result["p_mensaje"] });
    }

    // DELETE api/Seguimiento/{idSeguido}
    [HttpDelete("{idSeguido:int}")]
    public async Task<IActionResult> DejarDeSeguir(int idSeguido)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_seguimiento_dejar",
            inParams: new() { ["p_id_seguidor"] = idUsuario, ["p_id_seguido"] = idSeguido },
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
