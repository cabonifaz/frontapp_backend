using MySqlConnector;

namespace AppFronton.Data;

public class AppDbContext
{
    private readonly string _connectionString;

    public AppDbContext(IConfiguration config)
    {
        var host     = config["DB_HOST"];
        var port     = config["DB_PORT"];
        var db       = config["DB_NAME"];
        var user     = config["DB_USER"];
        var password = config["DB_PASSWORD"];

        _connectionString =
            $"Server={host};Port={port};Database={db};User={user};Password={password};AllowPublicKeyRetrieval=true;SslMode=None;";
    }

    public MySqlConnection CreateConnection() => new MySqlConnection(_connectionString);
}
