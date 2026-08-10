using AppFronton.Data;
using AppFronton.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace AppFronton.Controllers;

[ApiController]
[Route("api/Ranking")]
[Authorize]
public class RankingController(AppDbContext db) : ControllerBase
{
    // GET api/Ranking?id_deporte=1&filtro_genero=GENERO_MASCULINO&pagina=1&tamano_pagina=20
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] int id_deporte,
        [FromQuery] string? filtro_genero,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamano_pagina = 20)
    {
        using var conn = db.CreateConnection();
        var rows = await SpHelper.QueryAsync(conn, "sp_ranking_listar", new()
        {
            ["p_id_deporte"]    = id_deporte,
            ["p_filtro_genero"] = filtro_genero,
            ["p_pagina"]        = pagina,
            ["p_tamano_pagina"] = tamano_pagina
        });
        return Ok(rows);
    }

    // GET api/Ranking/{id}?id_deporte=1
    [HttpGet("{id:int}")]
    public async Task<IActionResult> ObtenerPerfil(int id, [FromQuery] int id_deporte)
    {
        using var conn = db.CreateConnection();
        var rows = await SpHelper.QueryAsync(conn, "sp_ranking_obtener_perfil",
            new() { ["p_id_usuario"] = id, ["p_id_deporte"] = id_deporte });
        return Ok(rows.FirstOrDefault());
    }

    // GET api/Ranking/{id}/resultados?id_deporte=1&pagina=1&tamano_pagina=10
    [HttpGet("{id:int}/resultados")]
    public async Task<IActionResult> ObtenerResultados(
        int id,
        [FromQuery] int id_deporte,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamano_pagina = 10)
    {
        using var conn = db.CreateConnection();
        var rows = await SpHelper.QueryAsync(conn, "sp_resultado_listar_historial",
            new() { ["p_id_usuario"] = id, ["p_filtro_fecha"] = null, ["p_busqueda_nombre"] = null });
        return Ok(rows);
    }

    // GET api/Ranking/{id}/validar-reto?id_deporte=1
    [HttpGet("{id:int}/validar-reto")]
    public async Task<IActionResult> ValidarReto(int id, [FromQuery] int id_deporte)
    {
        var idRetador = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_ranking_validar_reto",
            inParams: new()
            {
                ["p_id_retador"]  = idRetador,
                ["p_id_retado"]   = id,
                ["p_id_deporte"]  = id_deporte
            },
            outParams: new()
            {
                ["p_es_elegible"]       = MySqlDbType.Byte,
                ["p_mensaje"]           = MySqlDbType.VarChar,
                ["p_tipo_habilitacion"] = MySqlDbType.VarChar
            });

        return Ok(new
        {
            esElegible       = Convert.ToInt32(result["p_es_elegible"]) == 1,
            mensaje          = result["p_mensaje"],
            tipoHabilitacion = result["p_tipo_habilitacion"]
        });
    }

    // GET api/Ranking/mi-posicion?id_deporte=1
    [HttpGet("mi-posicion")]
    public async Task<IActionResult> MiPosicion([FromQuery] int id_deporte)
    {
        var idUsuario = JwtHelper.GetUserId(HttpContext);
        using var conn = db.CreateConnection();
        var result = await SpHelper.ExecuteAsync(conn, "sp_ranking_ir_a_mi_posicion",
            inParams: new() { ["p_id_usuario"] = idUsuario, ["p_id_deporte"] = id_deporte },
            outParams: new()
            {
                ["p_posicion"]       = MySqlDbType.Int32,
                ["p_pagina_en_lista"]= MySqlDbType.Int32
            });

        return Ok(new { posicion = result["p_posicion"], paginaEnLista = result["p_pagina_en_lista"] });
    }
}
