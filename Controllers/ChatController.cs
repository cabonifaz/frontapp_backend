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
            var mensajes = await _mensajesCollection
                .Find(m => m.PartidoId == partidoId)
                .SortBy(m => m.FechaEnvio)
                .ToListAsync();

            return Ok(mensajes);
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

            var nuevoMensaje = new MensajeChat
            {
                PartidoId = dto.PartidoId,
                UsuarioId = int.Parse(usuarioIdClaim),
                NombreUsuario = nombreClaim,
                Mensaje = dto.Mensaje,
                FechaEnvio = DateTime.UtcNow
            };

            await _mensajesCollection.InsertOneAsync(nuevoMensaje);

            return Ok(nuevoMensaje);
        }
    }
}
