namespace IRMSGen.Infrastructure.Persistence;

public sealed class PlatformDbOptions
{
    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 5432;

    public string Database { get; set; } = "irmsgen_platform";

    public string Username { get; set; } = "postgres";

    public string Password { get; set; } = string.Empty;

    public string BuildConnectionString(string? database = null)
    {
        var builder = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = Host,
            Port = Port,
            Database = database ?? Database,
            Username = Username,
            Password = Password,
            IncludeErrorDetail = true
        };

        return builder.ConnectionString;
    }
}
