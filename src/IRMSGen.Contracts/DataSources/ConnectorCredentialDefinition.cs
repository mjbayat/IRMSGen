namespace IRMSGen.Contracts.DataSources;

public sealed class ConnectorCredentialDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "MainDb";

    public string ConnectorType { get; set; } = "PostgreSQL";

    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 5432;

    public string Database { get; set; } = "orders";

    public string Username { get; set; } = "postgres";

    public string Password { get; set; } = string.Empty;

    public string SecretReference { get; set; } = "local:postgres-password";

    public string Description { get; set; } = "Default PostgreSQL credential";

    public string DomainName { get; set; } = string.Empty;

    public bool UseTls { get; set; }

    public bool IgnoreSslIssues { get; set; }

    public int ConnectTimeout { get; set; } = 30;

    public int RequestTimeout { get; set; } = 30;

    public string TdsVersion { get; set; } = "7.4";

    public bool UseSsl { get; set; }

    public bool UseSshTunnel { get; set; }

    public int DatabaseNumber { get; set; }

    public string VirtualHost { get; set; } = "/";

    public bool Passwordless { get; set; }

    public string ClientCertificate { get; set; } = string.Empty;

    public string ClientKey { get; set; } = string.Empty;

    public string Passphrase { get; set; } = string.Empty;

    public string CaCertificates { get; set; } = string.Empty;

    public string IndexPrefix { get; set; } = "logs-*";
}
