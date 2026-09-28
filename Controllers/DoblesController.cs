using AppFronton.Data;
using AppFronton.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.Text.Json.Serialization;

namespace AppFronton.Controllers;

// ─── DTOs ────────────────────────────────────────────────────────────────────

public class CrearDoblesDto
{
    [JsonPropertyName("id_companero")] public int IdCompanero { get; set; }
    [JsonPropertyName("id_rival1")]    public int? IdRival1   { get; set; }   // null = convocatoria
    [JsonPropertyName("id_rival2")]    public int? IdRival2   { get; set; }
    [JsonPropertyName("id_deporte")]   public int IdDeporte   { get; set; }
    [JsonPropertyName("id_cancha")]    public int IdCancha    { get; set; }
    [JsonPropertyName("fecha")]        public string Fecha    { get; set; } = "";
    [JsonPropertyName("hora")]         public string Hora     { get; set; } = "";
    [JsonPropertyName("num_sets")]     public int NumSets     { get; set; } = 5;
}

public class ResponderInvitacionDoblesDto
{
    [JsonPropertyName("aceptar")] public bool Aceptar { get; set; }
}

public class PostularDoblesDto
{
    [JsonPropertyName("id_companero")] public int IdCompanero { get; set; }
}

public class ResponderParejaDto
{
    [JsonPropertyName("id_lider")] public int  IdLider { get; set; }
    [JsonPropertyName("aceptar")]  public bool Aceptar { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────

[ApiController]
[Authorize]
public class DoblesController(AppDbContext db) : ControllerBase
{
    // POST api/PartidoAmistoso/crear-dobles
    // body: { id_companero, id_rival1?, id_rival2?, id_deporte, id_cancha, fecha, hora, num_sets }
    [HttpPost("api/PartidoAmistoso/crear-dobles")]
    public async Task<IActionResult> Crear([FromBody] CrearDoblesDto body)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_dobles_crear",
            inParams: new()
            {
                ["p_id_usuario"]   = idUsuario,
                ["p_id_companero"] = body.IdCompanero,
                ["p_id_rival1"]    = body.IdRival1,
                ["p_id_rival2"]    = body.IdRival2,
                ["p_id_deporte"]   = body.IdDeporte,
                ["p_id_cancha"]    = body.IdCancha,
                ["p_fecha"]        = body.Fecha,
                ["p_hora"]         = body.Hora,
                ["p_num_sets"]     = body.NumSets
            },
            outParams: new()
            {
                ["p_id_partido_creado"] = MySqlDbType.Int32,
                ["p_exito"]             = MySqlDbType.Byte,
                ["p_mensaje"]           = MySqlDbType.VarChar
            });

        if (Convert.ToInt32(result["p_exito"]) == 0)
            return BadRequest(new { mensaje = result["p_mensaje"] });

        return Ok(new { idPartidoCreado = result["p_id_partido_creado"], exito = true, mensaje = result["p_mensaje"] });
    }

    // GET api/Dobles/convocatorias?id_deporte=17
    // Convocatorias de dobles publicadas que puedo retar (con los datos de la pareja)
    [HttpGet("api/Dobles/convocatorias")]
    public async Task<IActionResult> Convocatorias([FromQuery] int id_deporte)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var rows = await SpHelper.QueryAsync(conn, "sp_dobles_buscar_convocatorias",
            new() { ["p_id_usuario"] = idUsuario, ["p_id_deporte"] = id_deporte });
        return Ok(rows);
    }

    // GET api/Dobles/pendientes?id_deporte=17
    // Invitaciones recibidas + parejas que retan mi convocatoria
    [HttpGet("api/Dobles/pendientes")]
    public async Task<IActionResult> Pendientes([FromQuery] int? id_deporte)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var rows = await SpHelper.QueryAsync(conn, "sp_dobles_listar_pendientes",
            new() { ["p_id_usuario"] = idUsuario, ["p_id_deporte"] = id_deporte });
        return Ok(rows);
    }

    // GET api/Dobles/{idPartido}/participantes
    [HttpGet("api/Dobles/{idPartido:int}/participantes")]
    public async Task<IActionResult> Participantes(int idPartido)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var rows = await SpHelper.QueryAsync(conn, "sp_dobles_participantes",
            new() { ["p_id_partido"] = idPartido, ["p_id_usuario"] = idUsuario });
        return Ok(rows);
    }

    // POST api/Dobles/{idPartido}/responder-invitacion   body: { aceptar: true }
    [HttpPost("api/Dobles/{idPartido:int}/responder-invitacion")]
    public async Task<IActionResult> ResponderInvitacion(int idPartido, [FromBody] ResponderInvitacionDoblesDto body)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_dobles_responder_invitacion",
            inParams: new()
            {
                ["p_id_partido"] = idPartido,
                ["p_id_usuario"] = idUsuario,
                ["p_aceptar"]    = body.Aceptar ? 1 : 0
            },
            outParams: new()
            {
                ["p_exito"]   = MySqlDbType.Byte,
                ["p_mensaje"] = MySqlDbType.VarChar,
                ["p_estado"]  = MySqlDbType.VarChar
            });

        if (Convert.ToInt32(result["p_exito"]) == 0)
            return BadRequest(new { mensaje = result["p_mensaje"] });

        return Ok(new { exito = true, mensaje = result["p_mensaje"], estado = result["p_estado"] });
    }

    // POST api/Dobles/{idPartido}/postular   body: { id_companero }
    [HttpPost("api/Dobles/{idPartido:int}/postular")]
    public async Task<IActionResult> Postular(int idPartido, [FromBody] PostularDoblesDto body)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_dobles_postular",
            inParams: new()
            {
                ["p_id_partido"]   = idPartido,
                ["p_id_usuario"]   = idUsuario,
                ["p_id_companero"] = body.IdCompanero
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

    // POST api/Dobles/{idPartido}/responder-pareja   body: { id_lider, aceptar }
    [HttpPost("api/Dobles/{idPartido:int}/responder-pareja")]
    public async Task<IActionResult> ResponderPareja(int idPartido, [FromBody] ResponderParejaDto body)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_dobles_responder_pareja",
            inParams: new()
            {
                ["p_id_partido"] = idPartido,
                ["p_id_usuario"] = idUsuario,
                ["p_id_lider"]   = body.IdLider,
                ["p_aceptar"]    = body.Aceptar ? 1 : 0
            },
            outParams: new()
            {
                ["p_exito"]   = MySqlDbType.Byte,
                ["p_mensaje"] = MySqlDbType.VarChar,
                ["p_estado"]  = MySqlDbType.VarChar
            });

        if (Convert.ToInt32(result["p_exito"]) == 0)
            return BadRequest(new { mensaje = result["p_mensaje"] });

        return Ok(new { exito = true, mensaje = result["p_mensaje"], estado = result["p_estado"] });
    }
}