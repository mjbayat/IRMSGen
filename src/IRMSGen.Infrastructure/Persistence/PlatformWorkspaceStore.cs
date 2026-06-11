using System.Text.Json;
using IRMSGen.Contracts.Wizard;
using Microsoft.Extensions.Options;
using Npgsql;

namespace IRMSGen.Infrastructure.Persistence;

public sealed class PlatformWorkspaceStore(IOptions<PlatformDbOptions> options)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly PlatformDbOptions _options = options.Value;

    public async Task<IReadOnlyList<PlatformProjectSummary>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_options.BuildConnectionString());
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            select id, name, root_namespace, target_framework, architecture, status, created_at, updated_at
            from projects
            order by updated_at desc;
            """,
            connection);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var projects = new List<PlatformProjectSummary>();

        while (await reader.ReadAsync(cancellationToken))
        {
            projects.Add(new PlatformProjectSummary(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetFieldValue<DateTimeOffset>(6),
                reader.GetFieldValue<DateTimeOffset>(7)));
        }

        return projects;
    }

    public async Task<IReadOnlyList<PlatformConnectorSummary>> GetConnectorsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_options.BuildConnectionString());
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            select id, name, type, status, metadata_json::text, updated_at
            from platform_connectors
            order by name;
            """,
            connection);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var connectors = new List<PlatformConnectorSummary>();

        while (await reader.ReadAsync(cancellationToken))
        {
            connectors.Add(new PlatformConnectorSummary(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetFieldValue<DateTimeOffset>(5)));
        }

        return connectors;
    }

    public async Task<Guid> SaveDraftAsync(WizardState state, string architecture, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_options.BuildConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var projectId = await UpsertProjectAsync(connection, transaction, state, architecture, cancellationToken);
        var version = await GetNextMetadataVersionAsync(connection, transaction, projectId, cancellationToken);
        await InsertMetadataVersionAsync(connection, transaction, projectId, version, state, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return projectId;
    }

    public async Task AddGenerationLogAsync(Guid projectId, string level, string message, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_options.BuildConnectionString());
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            insert into project_generation_logs (project_id, level, message)
            values (@projectId, @level, @message);
            """,
            connection);
        command.Parameters.AddWithValue("projectId", projectId);
        command.Parameters.AddWithValue("level", level);
        command.Parameters.AddWithValue("message", message);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<Guid> UpsertProjectAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        WizardState state,
        string architecture,
        CancellationToken cancellationToken)
    {
        var projectId = Guid.NewGuid();
        await using var command = new NpgsqlCommand(
            """
            insert into projects (id, name, root_namespace, target_framework, architecture, status, created_at, updated_at)
            values (@id, @name, @rootNamespace, @targetFramework, @architecture, 'Draft', now(), now())
            on conflict ((lower(name))) do update set
                root_namespace = excluded.root_namespace,
                target_framework = excluded.target_framework,
                architecture = excluded.architecture,
                status = excluded.status,
                updated_at = now()
            returning id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("id", projectId);
        command.Parameters.AddWithValue("name", state.Project.ServiceName);
        command.Parameters.AddWithValue("rootNamespace", state.Project.RootNamespace);
        command.Parameters.AddWithValue("targetFramework", state.Project.TargetFramework);
        command.Parameters.AddWithValue("architecture", architecture);

        return (Guid)(await command.ExecuteScalarAsync(cancellationToken) ?? projectId);
    }

    private static async Task<int> GetNextMetadataVersionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "select coalesce(max(version), 0) + 1 from project_metadata_versions where project_id = @projectId",
            connection,
            transaction);
        command.Parameters.AddWithValue("projectId", projectId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task InsertMetadataVersionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        int version,
        WizardState state,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            insert into project_metadata_versions (id, project_id, version, wizard_state_json)
            values (@id, @projectId, @version, @wizardStateJson::jsonb);
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("projectId", projectId);
        command.Parameters.AddWithValue("version", version);
        command.Parameters.AddWithValue("wizardStateJson", JsonSerializer.Serialize(state, JsonOptions));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
