namespace AppFronton.Models;

/// <summary>
/// Body para responder solicitudes: { "aceptar": true | false }
/// Se usa en AmistadController y en PartidoController (reto directo).
/// </summary>
public record AceptarDto(bool Aceptar);