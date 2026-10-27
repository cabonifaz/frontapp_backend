using MySqlConnector;

namespace AppFronton.Helpers;

/// <summary>
/// NUEVO (07): ¿la cuenta es administradora? (usuario.es_admin = 1, se marca por base de datos)
/// Se consulta en cada petición para que quitar el permiso tenga efecto inmediato.
/// </summary>
public static class AdminHelper
{
    public static async Task<bool> EsAdminAsync(MySqlConnection conn, int idUsuario)
    {
        var r = await SpHelper.ExecuteAsync(conn, "sp_usuario_es_admin",
            inParams: new() { ["p_id_usuario"] = idUsuario },
            outParams: new() { ["p_es_admin"] = MySqlDbType.Byte });
        return Convert.ToInt32(r["p_es_admin"] ?? 0) == 1;
    }
}
