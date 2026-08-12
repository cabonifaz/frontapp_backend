using MySqlConnector;
using System.Data;
using System.Text.Json; // <-- IMPORTANTE: Se agregó esta librería para leer JsonElement

namespace AppFronton.Helpers;

/// <summary>
/// Helper central para ejecutar Stored Procedures.
/// El backend es un bypass puro: recibe parámetros → llama al SP → retorna el resultado.
/// NINGUNA lógica de negocio vive aquí; toda la lógica está en los SP de MySQL.
/// </summary>
public static class SpHelper
{
    /// <summary>
    /// Ejecuta un SP que devuelve un result set (SELECT).
    /// Retorna una lista de diccionarios lista para serializar como JSON.
    /// </summary>
    public static async Task<List<Dictionary<string, object?>>> QueryAsync(
        MySqlConnection conn,
        string spName,
        Dictionary<string, object?>? inParams = null)
    {
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(spName, conn)
        {
            CommandType = CommandType.StoredProcedure
        };

        AddInputParams(cmd, inParams);

        using var reader = await cmd.ExecuteReaderAsync();
        var results = new List<Dictionary<string, object?>>();

        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            results.Add(row);
        }

        return results;
    }

    /// <summary>
    /// Ejecuta un SP que devuelve parámetros OUT (INSERT/UPDATE/validaciones).
    /// Retorna un diccionario con los valores OUT del SP.
    /// </summary>
    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        MySqlConnection conn,
        string spName,
        Dictionary<string, object?>? inParams = null,
        Dictionary<string, MySqlDbType>? outParams = null)
    {
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(spName, conn)
        {
            CommandType = CommandType.StoredProcedure
        };

        AddInputParams(cmd, inParams);
        AddOutputParams(cmd, outParams);

        await cmd.ExecuteNonQueryAsync();

        var result = new Dictionary<string, object?>();
        if (outParams != null)
            foreach (var key in outParams.Keys)
            {
                var val = cmd.Parameters[$"@{key}"].Value;
                result[key] = val == DBNull.Value ? null : val;
            }

        return result;
    }

    /// <summary>
    /// Ejecuta un SP que devuelve TANTO result set COMO parámetros OUT.
    /// </summary>
    public static async Task<(List<Dictionary<string, object?>> Rows, Dictionary<string, object?> OutValues)>
        QueryWithOutAsync(
            MySqlConnection conn,
            string spName,
            Dictionary<string, object?>? inParams = null,
            Dictionary<string, MySqlDbType>? outParams = null)
    {
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(spName, conn)
        {
            CommandType = CommandType.StoredProcedure
        };

        AddInputParams(cmd, inParams);
        AddOutputParams(cmd, outParams);

        using var reader = await cmd.ExecuteReaderAsync();
        var rows = new List<Dictionary<string, object?>>();

        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }

        // Cerrar reader antes de leer OUT params
        await reader.CloseAsync();

        var outValues = new Dictionary<string, object?>();
        if (outParams != null)
            foreach (var key in outParams.Keys)
            {
                var val = cmd.Parameters[$"@{key}"].Value;
                outValues[key] = val == DBNull.Value ? null : val;
            }

        return (rows, outValues);
    }

    // ── Privados ────────────────────────────────────────────────────────────
    
    private static void AddInputParams(MySqlCommand cmd, Dictionary<string, object?>? inParams)
    {
        if (inParams == null) return;
        foreach (var (key, value) in inParams)
        {
            // CAMBIO AQUÍ: Llamamos a ExtractValue para limpiar el JSON
            cmd.Parameters.AddWithValue($"@{key}", ExtractValue(value));
        }
    }

    private static void AddOutputParams(MySqlCommand cmd, Dictionary<string, MySqlDbType>? outParams)
    {
        if (outParams == null) return;
        foreach (var (key, dbType) in outParams)
        {
            var p = new MySqlParameter($"@{key}", dbType)
            {
                Direction = ParameterDirection.Output,
                Size = 500
            };
            cmd.Parameters.Add(p);
        }
    }

    // NUEVO MÉTODO DE JAZIR: Desempaqueta los JsonElement que envía .NET
    private static object ExtractValue(object? value)
    {
        if (value is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.Number =>
                    je.TryGetInt64(out var l) ? (object)l : je.GetDouble(),
                JsonValueKind.String  => je.GetString() ?? (object)DBNull.Value,
                JsonValueKind.True    => true,
                JsonValueKind.False   => false,
                JsonValueKind.Null    => DBNull.Value,
                _                     => DBNull.Value,
            };
        }
        return value ?? DBNull.Value;
    }
}