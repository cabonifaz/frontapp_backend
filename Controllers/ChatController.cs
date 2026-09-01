using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using AppFronton.Models;
using System.Security.Claims;

namespace AppFronton.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly IMongoCollection<MensajeChat> _mensajesCollection;

        public ChatController()
        {
            var mongoUri = Environment.GetEnvironmentVariable("MONGODB_URI");
            var mongoDbName = Environment.GetEnvironmentVariable("MONGODB_DB") ?? "AppFrontonChat";

            var client = new MongoClient(mongoUri);
            var database = client.GetDatabase(mongoDbName);
            _mensajesCollection = database.GetCollection<MensajeChat>("MensajesPartido");
        }

        [HttpGet("partido/{partidoId}")]
        public async Task<IActionResult> ObtenerMensajes(int partidoId)
        {
            var usuarioIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                ?? User.FindFirst("sub")?.Value
                                ?? User.FindFirst("id")?.Value;

            int.TryParse(usuarioIdClaim, out int usuarioActualId);

            var mensajes = await _mensajesCollection
                .Find(m => m.PartidoId == partidoId)
                .SortBy(m => m.FechaEnvio)
                .ToListAsync();

            var resultado = mensajes.Select(m => new
            {
                id_mensaje = m.Id,
                partido_id = m.PartidoId,
                usuario_id = m.UsuarioId,
                nombre_usuario = m.NombreUsuario,
                mensaje = m.Mensaje,
                fecha_envio = m.FechaEnvio,
                es_mio = m.UsuarioId == usuarioActualId
            });

            return Ok(resultado);
        }

        [HttpGet("clase/{claseId}")]
        public async Task<IActionResult> ObtenerMensajesClase(int claseId)
        {
            var usuarioIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                ?? User.FindFirst("sub")?.Value
                                ?? User.FindFirst("id")?.Value;

            int.TryParse(usuarioIdClaim, out int usuarioActualId);

            var mensajes = await _mensajesCollection
                .Find(m => m.ClaseId == claseId)
                .SortBy(m => m.FechaEnvio)
                .ToListAsync();

            var resultado = mensajes.Select(m => new
            {
                id_mensaje = m.Id,
                clase_id = m.ClaseId,
                usuario_id = m.UsuarioId,
                nombre_usuario = m.NombreUsuario,
                mensaje = m.Mensaje,
                fecha_envio = m.FechaEnvio,
                es_mio = m.UsuarioId == usuarioActualId
            });

            return Ok(resultado);
        }

        [HttpPost("enviar")]
        public async Task<IActionResult> EnviarMensaje([FromBody] CrearMensajeDto dto)
        {
            var usuarioIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                ?? User.FindFirst("sub")?.Value
                                ?? User.FindFirst("id")?.Value;

            var nombreClaim = User.FindFirst(ClaimTypes.Name)?.Value ?? "Usuario";

            if (string.IsNullOrEmpty(usuarioIdClaim))
                return Unauthorized();

            int usuarioId = int.Parse(usuarioIdClaim);

            var nuevoMensaje = new MensajeChat
            {
                PartidoId = dto.PartidoId,
                ClaseId = dto.ClaseId,
                UsuarioId = usuarioId,
                NombreUsuario = nombreClaim,
                Mensaje = dto.Mensaje,
                FechaEnvio = DateTime.UtcNow
            };

            await _mensajesCollection.InsertOneAsync(nuevoMensaje);

            return Ok(new
            {
                id_mensaje = nuevoMensaje.Id,
                partido_id = nuevoMensaje.PartidoId,
                clase_id = nuevoMensaje.ClaseId,
                usuario_id = nuevoMensaje.UsuarioId,
                nombre_usuario = nuevoMensaje.NombreUsuario,
                mensaje = nuevoMensaje.Mensaje,
                fecha_envio = nuevoMensaje.FechaEnvio,
                es_mio = true
            });
        }
    }
}
