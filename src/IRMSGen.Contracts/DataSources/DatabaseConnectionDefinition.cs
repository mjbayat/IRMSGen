namespace IRMSGen.Contracts.DataSources;

public sealed class DatabaseConnectionDefinition
{
    public string Provider { get; set; } = "PostgreSQL";

    public string CredentialId { get; set; } = string.Empty;

    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 5432;

    public string Database { get; set; } = "orders";

    public string Username { get; set; } = "postgres";

    public string Password { get; set; } = string.Empty;

    public string SecretReference { get; set; } = "local:postgres-password";

    public bool ConnectionTested { get; set; }
}
