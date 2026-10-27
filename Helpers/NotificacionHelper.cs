using MySqlConnector;

namespace AppFronton.Helpers;

/// <summary>
/// NUEVO (07): después de una acción sobre un partido (postular, retar, aceptar...)
/// crea los avisos que falten: reto recibido, reto aceptado y partido confirmado
/// para amigos y seguidores. Toda la lógica vive en sp_notificacion_partido_sincronizar;
/// si falla, la acción principal NO se ve afectada.
/// </summary>
public static class NotificacionHelper
{
    public static async Task SincronizarPartidoAsync(MySqlConnection conn, object? idPartido, int idActor)
    {
        if (idPartido is null or DBNull) return;
        try
        {
            await SpHelper.ExecuteAsync(conn, "sp_notificacion_partido_sincronizar",
                inParams: new() { ["p_id_partido"] = idPartido, ["p_id_actor"] = idActor });
        }
        catch (Exception ex)
        {
            // Los avisos nunca deben romper la acción del usuario, pero queda registrado en los logs de Render
            Console.Error.WriteLine($"[Notificaciones] partido {idPartido}: {ex.Message}");
        }
    }
}
