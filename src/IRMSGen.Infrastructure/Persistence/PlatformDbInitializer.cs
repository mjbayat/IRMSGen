using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace IRMSGen.Infrastructure.Persistence;

public sealed class PlatformDbInitializer(
    IOptions<PlatformDbOptions> options,
    ILogger<PlatformDbInitializer> logger)
{
    private readonly PlatformDbOptions _options = options.Value;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await EnsureDatabaseAsync(cancellationToken);
        await EnsureSchemaAsync(cancellationToken);
        await SeedConnectorsAsync(cancellationToken);
    }

    private async Task EnsureDatabaseAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_options.BuildConnectionString("postgres"));
        await connection.OpenAsync(cancellationToken);

        await using var existsCommand = new NpgsqlCommand(
            "select 1 from pg_database where datname = @database",
            connection);
        existsCommand.Parameters.AddWithValue("database", _options.Database);

        var exists = await existsCommand.ExecuteScalarAsync(cancellationToken);
        if (exists is not null)
        {
            return;
        }

        var databaseName = _options.Database.Replace("\"", "\"\"");
        await using var createCommand = new NpgsqlCommand($"""create database "{databaseName}" """, connection);
        await createCommand.ExecuteNonQueryAsync(cancellationToken);
        logger.LogInformation("Created IRMSGen platform database {Database}.", _options.Database);
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_options.BuildConnectionString());
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(SchemaSql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task SeedConnectorsAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_options.BuildConnectionString());
        await connection.OpenAsync(cancellationToken);

        foreach (var connector in DefaultConnectors)
        {
            await using var command = new NpgsqlCommand(
                """
                insert into platform_connectors (name, type, status, metadata_json, updated_at)
                values (@name, @type, @status, @metadata::jsonb, now())
                on conflict (name) do nothing;
                """,
                connection);
            command.Parameters.AddWithValue("name", connector.Name);
            command.Parameters.AddWithValue("type", connector.Type);
            command.Parameters.AddWithValue("status", connector.Status);
            command.Parameters.AddWithValue("metadata", connector.MetadataJson);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static readonly PlatformConnectorSummary[] DefaultConnectors =
    [
        new(Guid.Empty, "MainDb - PostgreSQL", "PostgreSQL", "Connected", """{"host":"127.0.0.1","port":5432,"database":"orders","provider":"EF Core"}""", DateTimeOffset.UtcNow),
        new(Guid.Empty, "SessionCache - Redis", "Redis", "Ready", """{"host":"127.0.0.1","port":6379,"ttlSeconds":3600}""", DateTimeOffset.UtcNow),
        new(Guid.Empty, "EventBus - RabbitMQ", "RabbitMQ", "Ready", """{"host":"127.0.0.1","port":5672,"exchange":"service.events"}""", DateTimeOffset.UtcNow),
        new(Guid.Empty, "Observability - ELK", "ELK", "Ready", """{"elasticsearch":"http://127.0.0.1:9200","kibana":"http://127.0.0.1:5601"}""", DateTimeOffset.UtcNow)
    ];

    private const string SchemaSql = """
create extension if not exists pgcrypto;

create table if not exists projects (
    id uuid primary key,
    name text not null,
    root_namespace text not null,
    target_framework text not null default 'net8.0',
    architecture text not null,
    status text not null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create unique index if not exists ux_projects_name on projects (lower(name));

create table if not exists project_metadata_versions (
    id uuid primary key,
    project_id uuid not null references projects(id) on delete cascade,
    version integer not null,
    wizard_state_json jsonb not null,
    created_at timestamptz not null default now(),
    unique (project_id, version)
);

create table if not exists platform_connectors (
    id uuid primary key default gen_random_uuid(),
    name text not null unique,
    type text not null,
    status text not null,
    metadata_json jsonb not null default '{}'::jsonb,
    updated_at timestamptz not null default now()
);

create table if not exists generation_runs (
    id uuid primary key,
    project_id uuid not null references projects(id) on delete cascade,
    metadata_version integer not null,
    output_path text not null default '',
    status text not null,
    started_at timestamptz not null default now(),
    finished_at timestamptz null
);

create table if not exists project_generation_logs (
    id bigserial primary key,
    generation_run_id uuid null references generation_runs(id) on delete cascade,
    project_id uuid not null references projects(id) on delete cascade,
    level text not null,
    message text not null,
    created_at timestamptz not null default now()
);

create table if not exists project_locks (
    project_id uuid primary key references projects(id) on delete cascade,
    locked_by text not null,
    lock_type text not null,
    acquired_at timestamptz not null default now(),
    expires_at timestamptz not null
);
""";
}
