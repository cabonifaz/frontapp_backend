using AppFronton.Data;
using AppFronton.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace AppFronton.Controllers;

[ApiController]
[Authorize]
public class ChatController(AppDbContext db) : ControllerBase
{
    // GET api/Chat/{idPartido}/mensajes
    [HttpGet("api/Chat/{idPartido:int}/mensajes")]
    public async Task<IActionResult> ListarMensajes(int idPartido)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var rows = await SpHelper.QueryAsync(conn, "sp_chat_listar_mensajes", new()
        {
            ["p_id_partido"] = idPartido,
            ["p_id_usuario"] = idUsuario
        });
        return Ok(rows);
    }

    // POST api/Chat/{idPartido}/mensajes
    [HttpPost("api/Chat/{idPartido:int}/mensajes")]
    public async Task<IActionResult> EnviarMensaje(int idPartido, [FromBody] Dictionary<string, object?> body)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_chat_enviar_mensaje",
            inParams: new()
            {
                ["p_id_partido"] = idPartido,
                ["p_id_usuario"] = idUsuario,
                ["p_mensaje"]    = body.GetValueOrDefault("mensaje")?.ToString()
            },
            outParams: new()
            {
                ["p_id_mensaje"] = MySqlDbType.Int32,
                ["p_exito"]      = MySqlDbType.Byte,
                ["p_mensaje_r"]  = MySqlDbType.VarChar
            });

        if (Convert.ToInt32(result["p_exito"]) == 0)
            return BadRequest(new { mensaje = result["p_mensaje_r"] });

        return Ok(new { idMensaje = result["p_id_mensaje"], exito = true });
    }
}
