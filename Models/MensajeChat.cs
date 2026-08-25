using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AppFronton.Models
{
    public class MensajeChat
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("partidoId")]
        public int PartidoId { get; set; }

        [BsonElement("usuarioId")]
        public int UsuarioId { get; set; }

        [BsonElement("nombreUsuario")]
        public string NombreUsuario { get; set; } = string.Empty;

        [BsonElement("mensaje")]
        public string Mensaje { get; set; } = string.Empty;

        [BsonElement("fechaEnvio")]
        public DateTime FechaEnvio { get; set; } = DateTime.UtcNow;
    }

    public class CrearMensajeDto
    {
        public int PartidoId { get; set; }
        public string Mensaje { get; set; } = string.Empty;
    }
}