using System.Text;
using System.Text.Json;
using IRMSGen.Domain.Metadata;

namespace IRMSGen.Generator.Generation;

public sealed class ServiceGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string Generate(ServiceMetadata metadata, string outputPath)
    {
        var root = Path.GetFullPath(Path.Combine(outputPath, metadata.ServiceName));
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        Directory.CreateDirectory(root);

        Write(root, "metadata.json", JsonSerializer.Serialize(metadata, JsonOptions));
        Write(root, ".irmsgen/project.metadata.json", JsonSerializer.Serialize(metadata, JsonOptions));
        Write(root, ".irmsgen/generation.manifest.json", RenderManifest(metadata));
        Write(root, $"{metadata.ServiceName}.sln", RenderSolution(metadata.ServiceName));
        Write(root, "README.md", RenderReadme(metadata));
        Write(root, "docker-compose.yml", RenderDockerCompose(metadata.ServiceName));
        Write(root, "deploy/k8s/deployment.yaml", RenderKubernetes(metadata.ServiceName));
        WriteHelmChart(root, metadata.ServiceName);
        WriteProjects(root, metadata);

        return root;
    }

    private static void WriteProjects(string root, ServiceMetadata metadata)
    {
        var name = metadata.ServiceName;
        var entities = metadata.Entities;
        var crudEntities = entities.Where(entity => entity.GenerateCrud).ToList();
        var useCqrs = UsesCqrs(metadata);

        WriteDomain(root, name, entities);
        WriteContracts(root, name, crudEntities);
        WriteApplication(root, name, crudEntities, useCqrs);
        WriteInfrastructure(root, name, entities, crudEntities);
        WriteApi(root, name, crudEntities, useCqrs);
    }

    private static void WriteHelmChart(string root, string name)
    {
        var chartPath = $"deploy/helm/{ToKebabCase(name)}";
        Write(root, $"{chartPath}/Chart.yaml", RenderHelmChart(name));
        Write(root, $"{chartPath}/values.yaml", RenderHelmValues(name));
        Write(root, $"{chartPath}/templates/_helpers.tpl", RenderHelmHelpers(name));
        Write(root, $"{chartPath}/templates/deployment.yaml", RenderHelmDeployment(name));
        Write(root, $"{chartPath}/templates/service.yaml", RenderHelmService(name));
        Write(root, $"{chartPath}/templates/ingress.yaml", RenderHelmIngress(name));
        Write(root, $"{chartPath}/templates/configmap.yaml", RenderHelmConfigMap(name));
        Write(root, $"{chartPath}/templates/secret.yaml", RenderHelmSecret(name));
    }

    private static void WriteDomain(string root, string name, IEnumerable<EntityMetadata> entities)
    {
        Write(root, $"src/{name}.Domain/{name}.Domain.csproj", RenderClassLibraryProject());
        Write(root, $"src/{name}.Domain/Events/DomainEvent.cs", RenderDomainEvent(name));
        Write(root, $"src/{name}.Domain/ValueObjects/.gitkeep", string.Empty);
        Write(root, $"src/{name}.Domain/Repositories/README.md", "Repository contracts live here only when they are part of the domain model.\n");

        foreach (var entity in entities)
        {
            Write(root, $"src/{name}.Domain/Entities/{entity.Name}.cs", RenderEntity(name, entity));
        }
    }

    private static void WriteContracts(string root, string name, IEnumerable<EntityMetadata> entities)
    {
        Write(root, $"src/{name}.Contracts/{name}.Contracts.csproj", RenderClassLibraryProject());
        Write(root, $"src/{name}.Contracts/Events/IntegrationEvent.cs", RenderIntegrationEvent(name));
        Write(root, $"src/{name}.Contracts/IntegrationDtos/ServiceOperationLog.cs", RenderServiceOperationLog(name));

        foreach (var entity in entities)
        {
            Write(root, $"src/{name}.Contracts/IntegrationDtos/{entity.Name}IntegrationDto.cs", RenderIntegrationDto(name, entity));
        }
    }

    private static void WriteApplication(string root, string name, IEnumerable<EntityMetadata> entities, bool useCqrs)
    {
        Write(root, $"src/{name}.Application/{name}.Application.csproj", RenderApplicationProject(name));
        Write(root, $"src/{name}.Application/Common/PagedResult.cs", RenderPagedResult(name));
        Write(root, $"src/{name}.Application/Common/Responses/GeneralResponse.cs", RenderGeneralResponse(name));
        Write(root, $"src/{name}.Application/Common/Responses/ResponseStatusCode.cs", RenderResponseStatusCode(name));
        Write(root, $"src/{name}.Application/Abstractions/Caching/ICacheService.cs", RenderCacheAbstraction(name));
        Write(root, $"src/{name}.Application/Abstractions/Messaging/IMessageBus.cs", RenderMessageBusAbstraction(name));
        Write(root, $"src/{name}.Application/Abstractions/Security/IEncryptionService.cs", RenderEncryptionAbstraction(name));

        foreach (var externalServiceName in ExternalServiceNames(entities))
        {
            Write(root, $"src/{name}.Application/Abstractions/ExternalServices/I{externalServiceName}Client.cs", RenderExternalClientAbstraction(name, externalServiceName));
        }

        foreach (var entity in entities)
        {
            var feature = Pluralize(entity.Name);
            Write(root, $"src/{name}.Application/Abstractions/Repositories/I{entity.Name}Repository.cs", RenderRepositoryInterface(name, entity));
            Write(root, $"src/{name}.Application/Features/{feature}/DTOs/{entity.Name}Dto.cs", RenderDto(name, entity));
            if (useCqrs)
            {
                Write(root, $"src/{name}.Application/Features/{feature}/Commands/Create{entity.Name}Command.cs", RenderApplicationInput(name, entity, $"Create{entity.Name}Command", "Commands"));
                Write(root, $"src/{name}.Application/Features/{feature}/Commands/Update{entity.Name}Command.cs", RenderApplicationInput(name, entity, $"Update{entity.Name}Command", "Commands"));
                Write(root, $"src/{name}.Application/Features/{feature}/Queries/{entity.Name}Queries.cs", RenderQueries(name, entity));
            }
            else
            {
                Write(root, $"src/{name}.Application/Features/{feature}/DTOs/Create{entity.Name}Input.cs", RenderApplicationInput(name, entity, $"Create{entity.Name}Input", "DTOs"));
                Write(root, $"src/{name}.Application/Features/{feature}/DTOs/Update{entity.Name}Input.cs", RenderApplicationInput(name, entity, $"Update{entity.Name}Input", "DTOs"));
            }

            Write(root, $"src/{name}.Application/Features/{feature}/{entity.Name}FeatureService.cs", RenderService(name, entity, useCqrs));

            foreach (var method in CustomMethods(entity))
            {
                var folder = useCqrs ? method.OperationType == "Command" ? "Commands" : "Queries" : "Operations";
                var suffix = CustomMethodSuffix(method, useCqrs);
                Write(root, $"src/{name}.Application/Features/{feature}/{folder}/{method.Name}{suffix}.cs", RenderMethodContract(name, entity, method, useCqrs));
            }
        }
    }

    private static void WriteInfrastructure(string root, string name, IEnumerable<EntityMetadata> entities, IEnumerable<EntityMetadata> crudEntities)
    {
        Write(root, $"src/{name}.Infrastructure/{name}.Infrastructure.csproj", RenderInfrastructureProject(name));
        Write(root, $"src/{name}.Infrastructure/DependencyInjection.cs", RenderInfrastructureDependencyInjection(name, crudEntities));
        Write(root, $"src/{name}.Infrastructure/Persistence/AppDbContext.cs", RenderDbContext(name, entities));
        Write(root, $"src/{name}.Infrastructure/Persistence/Migrations/.gitkeep", string.Empty);
        Write(root, $"src/{name}.Infrastructure/Caching/RedisOptions.cs", RenderRedisOptions(name));
        Write(root, $"src/{name}.Infrastructure/Caching/RedisCacheService.cs", RenderRedisCacheService(name));
        Write(root, $"src/{name}.Infrastructure/Messaging/RabbitMqOptions.cs", RenderRabbitMqOptions(name));
        Write(root, $"src/{name}.Infrastructure/Messaging/RabbitMqMessageBus.cs", RenderRabbitMqMessageBus(name));
        Write(root, $"src/{name}.Infrastructure/Security/CertificateOptions.cs", RenderCertificateOptions(name));
        Write(root, $"src/{name}.Infrastructure/Security/JweEncryptionService.cs", RenderJweEncryptionService(name));
        Write(root, $"src/{name}.Infrastructure/ExternalServices/README.md", "Generated external service clients will be placed here.\n");

        foreach (var externalServiceName in ExternalServiceNames(entities))
        {
            Write(root, $"src/{name}.Infrastructure/ExternalServices/{externalServiceName}/{externalServiceName}Options.cs", RenderExternalServiceOptions(name, externalServiceName));
            Write(root, $"src/{name}.Infrastructure/ExternalServices/{externalServiceName}/{externalServiceName}Client.cs", RenderExternalServiceClient(name, externalServiceName));
            Write(root, $"src/{name}.Infrastructure/ExternalServices/{externalServiceName}/Models/{externalServiceName}ExternalResponse.cs", RenderExternalServiceResponse(name, externalServiceName));
        }

        foreach (var entity in entities)
        {
            Write(root, $"src/{name}.Infrastructure/Persistence/Configurations/{entity.Name}Configuration.cs", RenderEntityConfiguration(name, entity));
        }

        foreach (var entity in crudEntities)
        {
            Write(root, $"src/{name}.Infrastructure/Persistence/Repositories/{entity.Name}Repository.cs", RenderRepository(name, entity));
        }
    }

    private static void WriteApi(string root, string name, IEnumerable<EntityMetadata> entities, bool useCqrs)
    {
        Write(root, $"src/{name}.Api/{name}.Api.csproj", RenderApiProject(name));
        Write(root, $"src/{name}.Api/Program.cs", RenderApiProgram(name, entities));
        Write(root, $"src/{name}.Api/Controllers/HealthController.cs", RenderHealthController(name));
        Write(root, $"src/{name}.Api/Responses/ErrorResponse.cs", RenderErrorResponse(name));
        Write(root, $"src/{name}.Api/Middlewares/CorrelationIdMiddleware.cs", RenderCorrelationMiddleware(name));
        Write(root, $"src/{name}.Api/Middlewares/ExceptionHandlingMiddleware.cs", RenderExceptionMiddleware(name));

        foreach (var entity in entities)
        {
            Write(root, $"src/{name}.Api/Controllers/{entity.Name}Controller.cs", RenderController(name, entity, useCqrs));
            Write(root, $"src/{name}.Api/Requests/Create{entity.Name}Request.cs", RenderApiRequest(name, entity, $"Create{entity.Name}Request", useCqrs));
            Write(root, $"src/{name}.Api/Requests/Update{entity.Name}Request.cs", RenderApiRequest(name, entity, $"Update{entity.Name}Request", useCqrs));
            Write(root, $"src/{name}.Api/Responses/{entity.Name}Response.cs", RenderApiResponse(name, entity));
        }

        Write(root, $"src/{name}.Api/appsettings.json", RenderSettings(name));
        Write(root, $"src/{name}.Api/Properties/launchSettings.json", RenderLaunchSettings());
    }

    private static void Write(string root, string relativePath, string contents)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
    }

    private static string RenderSolution(string name) => $$"""
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "{{name}}.Api", "src\{{name}}.Api\{{name}}.Api.csproj", "{07B5D8C6-1748-4EA7-873F-5C8B9911AC01}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "{{name}}.Application", "src\{{name}}.Application\{{name}}.Application.csproj", "{07B5D8C6-1748-4EA7-873F-5C8B9911AC02}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "{{name}}.Domain", "src\{{name}}.Domain\{{name}}.Domain.csproj", "{07B5D8C6-1748-4EA7-873F-5C8B9911AC03}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "{{name}}.Infrastructure", "src\{{name}}.Infrastructure\{{name}}.Infrastructure.csproj", "{07B5D8C6-1748-4EA7-873F-5C8B9911AC04}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "{{name}}.Contracts", "src\{{name}}.Contracts\{{name}}.Contracts.csproj", "{07B5D8C6-1748-4EA7-873F-5C8B9911AC05}"
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{07B5D8C6-1748-4EA7-873F-5C8B9911AC01}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{07B5D8C6-1748-4EA7-873F-5C8B9911AC01}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{07B5D8C6-1748-4EA7-873F-5C8B9911AC02}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{07B5D8C6-1748-4EA7-873F-5C8B9911AC02}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{07B5D8C6-1748-4EA7-873F-5C8B9911AC03}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{07B5D8C6-1748-4EA7-873F-5C8B9911AC03}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{07B5D8C6-1748-4EA7-873F-5C8B9911AC04}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{07B5D8C6-1748-4EA7-873F-5C8B9911AC04}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{07B5D8C6-1748-4EA7-873F-5C8B9911AC05}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{07B5D8C6-1748-4EA7-873F-5C8B9911AC05}.Debug|Any CPU.Build.0 = Debug|Any CPU
	EndGlobalSection
EndGlobal
""";

    private static string RenderClassLibraryProject() => """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);1591</NoWarn>
  </PropertyGroup>
</Project>
""";

    private static string RenderApplicationProject(string name) => $$"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\{{name}}.Domain\{{name}}.Domain.csproj" />
  </ItemGroup>
</Project>
""";

    private static string RenderInfrastructureProject(string name) => $$"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.11" />
    <PackageReference Include="Microsoft.Extensions.Caching.StackExchangeRedis" Version="8.0.11" />
    <PackageReference Include="Microsoft.Extensions.Http" Version="8.0.1" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.11" />
    <PackageReference Include="RabbitMQ.Client" Version="6.8.1" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\{{name}}.Application\{{name}}.Application.csproj" />
    <ProjectReference Include="..\{{name}}.Domain\{{name}}.Domain.csproj" />
  </ItemGroup>
</Project>
""";

    private static string RenderApiProject(string name) => $$"""
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Serilog.AspNetCore" Version="8.0.3" />
    <PackageReference Include="Serilog.Sinks.Console" Version="5.0.1" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.6.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\{{name}}.Application\{{name}}.Application.csproj" />
    <ProjectReference Include="..\{{name}}.Contracts\{{name}}.Contracts.csproj" />
    <ProjectReference Include="..\{{name}}.Infrastructure\{{name}}.Infrastructure.csproj" />
  </ItemGroup>
</Project>
""";

    private static string RenderEntity(string serviceName, EntityMetadata entity)
    {
        var builder = new StringBuilder($$"""
namespace {{serviceName}}.Domain.Entities;

public sealed class {{entity.Name}}
{
""");

        foreach (var field in entity.Fields)
        {
            var initializer = field.Type == "string" && field.Required ? " = string.Empty;" : string.Empty;
            builder.AppendLine($"    public {MakeNullable(field.Type, field.Required)} {field.Name} {{ get; set; }}{initializer}");
        }

        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string RenderDomainEvent(string serviceName) => $$"""
namespace {{serviceName}}.Domain.Events;

public abstract record DomainEvent(Guid Id, DateTimeOffset OccurredAtUtc);
""";

    private static string RenderIntegrationEvent(string serviceName) => $$"""
namespace {{serviceName}}.Contracts.Events;

public abstract record IntegrationEvent(Guid Id, DateTimeOffset OccurredAtUtc, string CorrelationId);
""";

    private static string RenderServiceOperationLog(string serviceName) => $$"""
namespace {{serviceName}}.Contracts.IntegrationDtos;

public sealed record ServiceOperationLog(
    string Operation,
    string OperationType,
    string Route,
    string HttpMethod,
    bool FromCache,
    bool UsedExternalService,
    bool UsedEncryption,
    DateTimeOffset OccurredAtUtc);
""";

    private static string RenderIntegrationDto(string serviceName, EntityMetadata entity) => $$"""
namespace {{serviceName}}.Contracts.IntegrationDtos;

public sealed record {{entity.Name}}IntegrationDto({{RenderParameters(entity.Fields)}});
""";

    private static string RenderDto(string serviceName, EntityMetadata entity) => $$"""
namespace {{serviceName}}.Application.Features.{{Pluralize(entity.Name)}}.DTOs;

public sealed record {{entity.Name}}Dto({{RenderParameters(entity.Fields)}});
""";

    private static string RenderApplicationInput(string serviceName, EntityMetadata entity, string inputName, string folder) => $$"""
namespace {{serviceName}}.Application.Features.{{Pluralize(entity.Name)}}.{{folder}};

public sealed record {{inputName}}({{RenderParameters(entity.Fields.Where(field => field.Name != "Id"))}});
""";

    private static string RenderQueries(string serviceName, EntityMetadata entity) => $$"""
namespace {{serviceName}}.Application.Features.{{Pluralize(entity.Name)}}.Queries;

public sealed record Get{{entity.Name}}ByIdQuery(Guid Id);

public sealed record List{{Pluralize(entity.Name)}}Query(int Page = 1, int PageSize = 50);
""";

    private static string RenderMethodContract(string serviceName, EntityMetadata entity, MethodMetadata method, bool useCqrs)
    {
        var folder = useCqrs ? method.OperationType == "Command" ? "Commands" : "Queries" : "Operations";
        var suffix = CustomMethodSuffix(method, useCqrs);
        return $$"""
namespace {{serviceName}}.Application.Features.{{Pluralize(entity.Name)}}.{{folder}};

/// <summary>
/// Defines the request contract for the {{OperationContractDescription(method, useCqrs)}} operation `{{method.Name}}`.
/// </summary>
/// <param name="UsesDatabase">Indicates whether this operation reads from or writes to the service database.</param>
/// <param name="UsesExternalService">Indicates whether this operation calls an external service.</param>
/// <param name="ExternalServiceName">External service client name used by this operation, when applicable.</param>
/// <param name="UsesPaging">Indicates whether paging metadata is expected for this operation.</param>
/// <param name="UsesCache">Indicates whether cache lookup or cache invalidation should be applied.</param>
/// <param name="UsesMessageLog">Indicates whether an operation log message should be published.</param>
/// <param name="UsesEncryption">Indicates whether certificate-based encryption is required.</param>
public sealed record {{method.Name}}{{suffix}}(
    bool UsesDatabase = {{method.UsesDatabase.ToString().ToLowerInvariant()}},
    bool UsesExternalService = {{method.UsesExternalService.ToString().ToLowerInvariant()}},
    string ExternalServiceName = "{{SanitizeIdentifier(method.ExternalServiceName, "ExternalService")}}",
    bool UsesPaging = {{method.UsesPaging.ToString().ToLowerInvariant()}},
    bool UsesCache = {{method.UsesCache.ToString().ToLowerInvariant()}},
    bool UsesMessageLog = {{method.UsesMessageLog.ToString().ToLowerInvariant()}},
    bool UsesEncryption = {{method.UsesEncryption.ToString().ToLowerInvariant()}});
""";
    }

    private static string RenderPagedResult(string serviceName) => $$"""
namespace {{serviceName}}.Application.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
""";

    private static string RenderGeneralResponse(string serviceName) => $$"""
namespace {{serviceName}}.Application.Common.Responses;

public sealed class GeneralResponse<T>
{
    public ResponseStatusCode ResponseCode { get; init; } = ResponseStatusCode.OK;

    public string ResponseMessage { get; init; } = "عملیات موفق";

    public string? Description { get; init; }

    public string? ReferenceNumber { get; init; }

    public T? Data { get; init; }
}
""";

    private static string RenderResponseStatusCode(string serviceName) => $$"""
namespace {{serviceName}}.Application.Common.Responses;

public enum ResponseStatusCode
{
    OK = 100,
    NotAuthorized = 103,
    NotFound = 104,
    Exception = 105,
    BadRequest = 106
}
""";

    private static string RenderCacheAbstraction(string serviceName) => $$"""
namespace {{serviceName}}.Application.Abstractions.Caching;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken);

    Task SetAsync<T>(string key, T value, TimeSpan? expiration, CancellationToken cancellationToken);

    Task RemoveAsync(string key, CancellationToken cancellationToken);
}
""";

    private static string RenderMessageBusAbstraction(string serviceName) => $$"""
namespace {{serviceName}}.Application.Abstractions.Messaging;

public interface IMessageBus
{
    Task PublishAsync<TMessage>(string exchange, string routingKey, TMessage message, CancellationToken cancellationToken);
}
""";

    private static string RenderEncryptionAbstraction(string serviceName) => $$"""
namespace {{serviceName}}.Application.Abstractions.Security;

public interface IEncryptionService
{
    string Encrypt(string value);
}
""";

    private static string RenderExternalClientAbstraction(string serviceName, string externalServiceName) => $$"""
namespace {{serviceName}}.Application.Abstractions.ExternalServices;

public interface I{{externalServiceName}}Client
{
    Task<object> SendAsync(string operation, object payload, CancellationToken cancellationToken);
}
""";

    private static string RenderRepositoryInterface(string serviceName, EntityMetadata entity) => $$"""
namespace {{serviceName}}.Application.Abstractions.Repositories;

public interface I{{entity.Name}}Repository
{
    Task<IReadOnlyList<global::{{serviceName}}.Domain.Entities.{{entity.Name}}>> ListAsync(CancellationToken cancellationToken);

    Task<global::{{serviceName}}.Domain.Entities.{{entity.Name}}?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(global::{{serviceName}}.Domain.Entities.{{entity.Name}} entity, CancellationToken cancellationToken);

    Task UpdateAsync(global::{{serviceName}}.Domain.Entities.{{entity.Name}} entity, CancellationToken cancellationToken);

    Task DeleteAsync(global::{{serviceName}}.Domain.Entities.{{entity.Name}} entity, CancellationToken cancellationToken);
}
""";

    private static string RenderService(string serviceName, EntityMetadata entity, bool useCqrs)
    {
        var customMethods = RenderFeatureCustomMethods(entity, useCqrs);
        var extraUsings = useCqrs
            ? $"""
using {serviceName}.Application.Features.{Pluralize(entity.Name)}.Commands;
using {serviceName}.Application.Features.{Pluralize(entity.Name)}.Queries;
"""
            : string.Empty;
        var createInputType = useCqrs ? $"Create{entity.Name}Command" : $"Create{entity.Name}Input";
        var updateInputType = useCqrs ? $"Update{entity.Name}Command" : $"Update{entity.Name}Input";
        var inputLabel = useCqrs ? "command" : "input";
        var inputDescription = useCqrs ? "Command" : "Input";
        return $$"""
using {{serviceName}}.Application.Abstractions.Repositories;
using {{serviceName}}.Application.Features.{{Pluralize(entity.Name)}}.DTOs;
{{extraUsings}}

namespace {{serviceName}}.Application.Features.{{Pluralize(entity.Name)}};

public sealed class {{entity.Name}}FeatureService(I{{entity.Name}}Repository repository)
{
    /// <summary>
    /// Retrieves all {{Pluralize(entity.Name)}} from the persistence store and maps them to DTOs.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>A read-only list of {{entity.Name}} DTOs.</returns>
    public async Task<IReadOnlyList<{{entity.Name}}Dto>> ListAsync(CancellationToken cancellationToken)
    {
        var items = await repository.ListAsync(cancellationToken);
        return items.Select(ToDto).ToList();
    }

    /// <summary>
    /// Retrieves a single {{entity.Name}} by its unique identifier.
    /// </summary>
    /// <param name="id">Unique identifier of the {{entity.Name}}.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>The matching {{entity.Name}} DTO, or null when it does not exist.</returns>
    public async Task<{{entity.Name}}Dto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await repository.GetByIdAsync(id, cancellationToken);
        return item is null ? null : ToDto(item);
    }

    /// <summary>
    /// Creates a new {{entity.Name}} from the supplied {{inputLabel}} and persists it.
    /// </summary>
    /// <param name="{{inputLabel}}">{{inputDescription}} containing the values required to create the {{entity.Name}}.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>The created {{entity.Name}} DTO.</returns>
    public async Task<{{entity.Name}}Dto> CreateAsync({{createInputType}} {{inputLabel}}, CancellationToken cancellationToken)
    {
        var item = new global::{{serviceName}}.Domain.Entities.{{entity.Name}}
        {
{{RenderCreateAssignments(entity, inputLabel)}}
        };

        await repository.AddAsync(item, cancellationToken);
        return ToDto(item);
    }

    /// <summary>
    /// Updates an existing {{entity.Name}} with values from the supplied {{inputLabel}}.
    /// </summary>
    /// <param name="id">Unique identifier of the {{entity.Name}} to update.</param>
    /// <param name="{{inputLabel}}">{{inputDescription}} containing the updated values.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>The updated {{entity.Name}} DTO, or null when the entity does not exist.</returns>
    public async Task<{{entity.Name}}Dto?> UpdateAsync(Guid id, {{updateInputType}} {{inputLabel}}, CancellationToken cancellationToken)
    {
        var item = await repository.GetByIdAsync(id, cancellationToken);
        if (item is null)
        {
            return null;
        }

{{RenderUpdateAssignments(entity, "item", inputLabel)}}
        await repository.UpdateAsync(item, cancellationToken);
        return ToDto(item);
    }

    /// <summary>
    /// Deletes an existing {{entity.Name}} by its unique identifier.
    /// </summary>
    /// <param name="id">Unique identifier of the {{entity.Name}} to delete.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>True when the {{entity.Name}} was deleted; otherwise false.</returns>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await repository.GetByIdAsync(id, cancellationToken);
        if (item is null)
        {
            return false;
        }

        await repository.DeleteAsync(item, cancellationToken);
        return true;
    }

{{customMethods}}
    /// <summary>
    /// Maps a domain {{entity.Name}} entity to its application DTO representation.
    /// </summary>
    /// <param name="item">Domain entity to map.</param>
    /// <returns>The mapped {{entity.Name}} DTO.</returns>
    private static {{entity.Name}}Dto ToDto(global::{{serviceName}}.Domain.Entities.{{entity.Name}} item) => new({{RenderValueArguments(entity, "item")}});
}
""";
    }

    private static string RenderDbContext(string name, IEnumerable<EntityMetadata> entities)
    {
        var sets = string.Join(Environment.NewLine, entities.Select(entity => $"    public DbSet<global::{name}.Domain.Entities.{entity.Name}> {Pluralize(entity.Name)} => Set<global::{name}.Domain.Entities.{entity.Name}>();"));

        return $$"""
using Microsoft.EntityFrameworkCore;

namespace {{name}}.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
{{sets}}

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
""";
    }

    private static string RenderEntityConfiguration(string serviceName, EntityMetadata entity) => $$"""
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace {{serviceName}}.Infrastructure.Persistence.Configurations;

public sealed class {{entity.Name}}Configuration : IEntityTypeConfiguration<global::{{serviceName}}.Domain.Entities.{{entity.Name}}>
{
    public void Configure(EntityTypeBuilder<global::{{serviceName}}.Domain.Entities.{{entity.Name}}> builder)
    {
        builder.ToTable("{{ToSnakeCase(Pluralize(entity.Name))}}");
        builder.HasKey(item => item.Id);
    }
}
""";

    private static string RenderRepository(string serviceName, EntityMetadata entity) => $$"""
using Microsoft.EntityFrameworkCore;
using {{serviceName}}.Application.Abstractions.Repositories;

namespace {{serviceName}}.Infrastructure.Persistence.Repositories;

public sealed class {{entity.Name}}Repository(AppDbContext dbContext) : I{{entity.Name}}Repository
{
    /// <summary>
    /// Retrieves all {{Pluralize(entity.Name)}} from the database without change tracking.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the asynchronous database operation.</param>
    /// <returns>A read-only list of {{entity.Name}} entities.</returns>
    public async Task<IReadOnlyList<global::{{serviceName}}.Domain.Entities.{{entity.Name}}>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.{{Pluralize(entity.Name)}}.AsNoTracking().ToListAsync(cancellationToken);

    /// <summary>
    /// Retrieves a {{entity.Name}} entity by its unique identifier.
    /// </summary>
    /// <param name="id">Unique identifier of the {{entity.Name}}.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous database operation.</param>
    /// <returns>The matching entity, or null when it does not exist.</returns>
    public Task<global::{{serviceName}}.Domain.Entities.{{entity.Name}}?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.{{Pluralize(entity.Name)}}.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    /// <summary>
    /// Adds a new {{entity.Name}} entity to the database and saves changes.
    /// </summary>
    /// <param name="entity">Entity instance to add.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous database operation.</param>
    public async Task AddAsync(global::{{serviceName}}.Domain.Entities.{{entity.Name}} entity, CancellationToken cancellationToken)
    {
        dbContext.{{Pluralize(entity.Name)}}.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Updates an existing {{entity.Name}} entity in the database and saves changes.
    /// </summary>
    /// <param name="entity">Entity instance with updated values.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous database operation.</param>
    public async Task UpdateAsync(global::{{serviceName}}.Domain.Entities.{{entity.Name}} entity, CancellationToken cancellationToken)
    {
        dbContext.{{Pluralize(entity.Name)}}.Update(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Removes a {{entity.Name}} entity from the database and saves changes.
    /// </summary>
    /// <param name="entity">Entity instance to remove.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous database operation.</param>
    public async Task DeleteAsync(global::{{serviceName}}.Domain.Entities.{{entity.Name}} entity, CancellationToken cancellationToken)
    {
        dbContext.{{Pluralize(entity.Name)}}.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
""";

    private static string RenderRedisOptions(string serviceName) => $$"""
namespace {{serviceName}}.Infrastructure.Caching;

public sealed class RedisOptions
{
    public string ConnectionString { get; set; } = "127.0.0.1:6379";

    public string InstanceName { get; set; } = "{{serviceName}}:";
}
""";

    private static string RenderRedisCacheService(string serviceName) => $$"""
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using {{serviceName}}.Application.Abstractions.Caching;

namespace {{serviceName}}.Infrastructure.Caching;

public sealed class RedisCacheService(IDistributedCache cache) : ICacheService
{
    /// <summary>
    /// Reads a cached value by key and deserializes it to the requested type.
    /// </summary>
    /// <typeparam name="T">Expected value type.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous cache operation.</param>
    /// <returns>The cached value, or default when the key is not found.</returns>
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken)
    {
        var value = await cache.GetStringAsync(key, cancellationToken);
        return value is null ? default : JsonSerializer.Deserialize<T>(value);
    }

    /// <summary>
    /// Serializes and stores a value in the distributed cache.
    /// </summary>
    /// <typeparam name="T">Value type.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="value">Value to cache.</param>
    /// <param name="expiration">Optional absolute expiration relative to now.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous cache operation.</param>
    public Task SetAsync<T>(string key, T value, TimeSpan? expiration, CancellationToken cancellationToken)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(10)
        };

        return cache.SetStringAsync(key, JsonSerializer.Serialize(value), options, cancellationToken);
    }

    /// <summary>
    /// Removes a cached value by key.
    /// </summary>
    /// <param name="key">Cache key to remove.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous cache operation.</param>
    public Task RemoveAsync(string key, CancellationToken cancellationToken) =>
        cache.RemoveAsync(key, cancellationToken);
}
""";

    private static string RenderRabbitMqOptions(string serviceName) => $$"""
namespace {{serviceName}}.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public string HostName { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 5672;

    public string UserName { get; set; } = "guest";

    public string Password { get; set; } = "guest";
}
""";

    private static string RenderRabbitMqMessageBus(string serviceName) => $$"""
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using {{serviceName}}.Application.Abstractions.Messaging;

namespace {{serviceName}}.Infrastructure.Messaging;

public sealed class RabbitMqMessageBus(ILogger<RabbitMqMessageBus> logger) : IMessageBus
{
    /// <summary>
    /// Publishes, or prepares to publish, a message to the configured message bus.
    /// </summary>
    /// <typeparam name="TMessage">Message payload type.</typeparam>
    /// <param name="exchange">Target exchange name.</param>
    /// <param name="routingKey">Routing key used by subscribers.</param>
    /// <param name="message">Message payload to publish.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous publish operation.</param>
    public Task PublishAsync<TMessage>(string exchange, string routingKey, TMessage message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(message);
        logger.LogInformation("RabbitMQ publish prepared. Exchange={Exchange}, RoutingKey={RoutingKey}, Bytes={Bytes}",
            exchange,
            routingKey,
            Encoding.UTF8.GetByteCount(payload));

        return Task.CompletedTask;
    }
}
""";

    private static string RenderCertificateOptions(string serviceName) => $$"""
namespace {{serviceName}}.Infrastructure.Security;

public sealed class CertificateOptions
{
    public string PublicKeyPath { get; set; } = "Cert/service-public-key.pem";
}
""";

    private static string RenderJweEncryptionService(string serviceName) => $$"""
using Microsoft.Extensions.Options;
using {{serviceName}}.Application.Abstractions.Security;

namespace {{serviceName}}.Infrastructure.Security;

public sealed class JweEncryptionService(IOptions<CertificateOptions> options) : IEncryptionService
{
    /// <summary>
    /// Encrypts a string value using the configured certificate settings.
    /// </summary>
    /// <param name="value">Plain text value to encrypt.</param>
    /// <returns>Encrypted representation of the supplied value.</returns>
    public string Encrypt(string value)
    {
        // Replace this placeholder with the certificate/JWE implementation required by the target service.
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{options.Value.PublicKeyPath}:{value}"));
    }
}
""";

    private static string RenderExternalServiceOptions(string serviceName, string externalServiceName) => $$"""
namespace {{serviceName}}.Infrastructure.ExternalServices.{{externalServiceName}};

public sealed class {{externalServiceName}}Options
{
    public string BaseUrl { get; set; } = "https://api.example.com";

    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}
""";

    private static string RenderExternalServiceClient(string serviceName, string externalServiceName) => $$"""
using Microsoft.Extensions.Options;
using {{serviceName}}.Application.Abstractions.ExternalServices;

namespace {{serviceName}}.Infrastructure.ExternalServices.{{externalServiceName}};

public sealed class {{externalServiceName}}Client(IOptions<{{externalServiceName}}Options> options) : I{{externalServiceName}}Client
{
    /// <summary>
    /// Sends an operation payload to the external service client abstraction.
    /// </summary>
    /// <param name="operation">External operation name.</param>
    /// <param name="payload">Payload sent to the external service.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous external call.</param>
    /// <returns>The external service result envelope.</returns>
    public Task<object> SendAsync(string operation, object payload, CancellationToken cancellationToken)
    {
        var result = new
        {
            Service = "{{externalServiceName}}",
            Operation = operation,
            options.Value.BaseUrl,
            Payload = payload
        };

        return Task.FromResult<object>(result);
    }
}
""";

    private static string RenderExternalServiceResponse(string serviceName, string externalServiceName) => $$"""
namespace {{serviceName}}.Infrastructure.ExternalServices.{{externalServiceName}}.Models;

public sealed class {{externalServiceName}}ExternalResponse
{
    public int StatusCode { get; set; }

    public string? Content { get; set; }

    public string? Description { get; set; }
}
""";

    private static string RenderInfrastructureDependencyInjection(string serviceName, IEnumerable<EntityMetadata> entities)
    {
        var entityList = entities.ToList();
        var externalServiceNames = ExternalServiceNames(entityList);
        var externalApplicationUsing = externalServiceNames.Count > 0
            ? $"using {serviceName}.Application.Abstractions.ExternalServices;"
            : string.Empty;
        var registrations = string.Join(Environment.NewLine, entityList.Select(entity =>
            $"        services.AddScoped<I{entity.Name}Repository, {entity.Name}Repository>();"));
        var externalServiceRegistrations = string.Join(Environment.NewLine, externalServiceNames.Select(externalServiceName =>
            $$"""
        services.Configure<{{externalServiceName}}Options>(configuration.GetSection("ExternalServices:{{externalServiceName}}"));
        services.AddHttpClient<I{{externalServiceName}}Client, {{externalServiceName}}Client>();
"""));

        return $$"""
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using {{serviceName}}.Application.Abstractions.Caching;
{{externalApplicationUsing}}
using {{serviceName}}.Application.Abstractions.Messaging;
using {{serviceName}}.Application.Abstractions.Repositories;
using {{serviceName}}.Application.Abstractions.Security;
using {{serviceName}}.Infrastructure.Caching;
{{RenderExternalServiceUsings(serviceName, entityList)}}
using {{serviceName}}.Infrastructure.Messaging;
using {{serviceName}}.Infrastructure.Persistence;
using {{serviceName}}.Infrastructure.Persistence.Repositories;
using {{serviceName}}.Infrastructure.Security;

namespace {{serviceName}}.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("PostgreSql")));

        services.Configure<RedisOptions>(options =>
        {
            options.ConnectionString = configuration["Redis:ConnectionString"] ?? options.ConnectionString;
            options.InstanceName = configuration["Redis:InstanceName"] ?? options.InstanceName;
        });

        services.Configure<RabbitMqOptions>(options =>
        {
            options.HostName = configuration["RabbitMq:HostName"] ?? options.HostName;
            options.UserName = configuration["RabbitMq:UserName"] ?? options.UserName;
            options.Password = configuration["RabbitMq:Password"] ?? options.Password;
            options.Port = int.TryParse(configuration["RabbitMq:Port"], out var port) ? port : options.Port;
        });

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration["Redis:ConnectionString"];
            options.InstanceName = configuration["Redis:InstanceName"];
        });

        services.AddSingleton<ICacheService, RedisCacheService>();
        services.AddSingleton<IMessageBus, RabbitMqMessageBus>();
        services.Configure<CertificateOptions>(configuration.GetSection("Certificate"));
        services.AddSingleton<IEncryptionService, JweEncryptionService>();
{{externalServiceRegistrations}}

{{registrations}}
        return services;
    }
}
""";
    }

    private static string RenderApiProgram(string name, IEnumerable<EntityMetadata> entities)
    {
        var serviceUsings = string.Join(Environment.NewLine, entities.Select(entity =>
            $"using {name}.Application.Features.{Pluralize(entity.Name)};"));
        var serviceRegistrations = string.Join(Environment.NewLine, entities.Select(entity =>
            $"builder.Services.AddScoped<{entity.Name}FeatureService>();"));

        return $$"""
{{serviceUsings}}
using Serilog;
using System.Reflection;
using {{name}}.Api.Middlewares;
using {{name}}.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .MinimumLevel.Information()
        .Enrich.FromLogContext()
        .WriteTo.Console();
});

builder.Services.AddInfrastructure(builder.Configuration);
{{serviceRegistrations}}
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();
""";
    }

    private static string RenderHealthController(string serviceName) => $$"""
using Microsoft.AspNetCore.Mvc;

namespace {{serviceName}}.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    /// <summary>
    /// Returns the health status of the running API process.
    /// </summary>
    /// <returns>An HTTP 200 response when the service is reachable.</returns>
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "healthy" });
}
""";

    private static string RenderController(string serviceName, EntityMetadata entity, bool useCqrs)
    {
        var route = ToKebabCase(Pluralize(entity.Name));
        var customActions = RenderControllerCustomActions(entity, useCqrs);
        var hasCustomMethods = CustomMethods(entity).Any();
        var extraUsings = useCqrs
            ? $"""
using {serviceName}.Application.Features.{Pluralize(entity.Name)}.Commands;
using {serviceName}.Application.Features.{Pluralize(entity.Name)}.Queries;
"""
            : hasCustomMethods ? $"""
using {serviceName}.Application.Features.{Pluralize(entity.Name)}.Operations;
""" : string.Empty;
        var requestMapper = useCqrs ? "ToCommand" : "ToInput";
        return $$"""
using Microsoft.AspNetCore.Mvc;
using {{serviceName}}.Api.Requests;
using {{serviceName}}.Api.Responses;
using {{serviceName}}.Application.Features.{{Pluralize(entity.Name)}};
{{extraUsings}}

namespace {{serviceName}}.Api.Controllers;

[ApiController]
[Route("api/{{route}}")]
public sealed class {{entity.Name}}Controller({{entity.Name}}FeatureService service) : ControllerBase
{
    /// <summary>
    /// Returns all {{Pluralize(entity.Name)}} available to the service.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the asynchronous request.</param>
    /// <returns>An HTTP 200 response containing the {{entity.Name}} collection.</returns>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await service.ListAsync(cancellationToken));

    /// <summary>
    /// Returns a single {{entity.Name}} by its unique identifier.
    /// </summary>
    /// <param name="id">Unique identifier of the {{entity.Name}}.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous request.</param>
    /// <returns>An HTTP 200 response with the {{entity.Name}}, or HTTP 404 when it is not found.</returns>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        return result is null
            ? NotFound(new ErrorResponse("{{entity.Name.ToUpperInvariant()}}_NOT_FOUND", "{{entity.Name}} was not found."))
            : Ok(result);
    }

    /// <summary>
    /// Creates a new {{entity.Name}} from the request body.
    /// </summary>
    /// <param name="request">Request payload containing the values for the new {{entity.Name}}.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous request.</param>
    /// <returns>An HTTP 201 response containing the created {{entity.Name}}.</returns>
    [HttpPost]
    public async Task<IActionResult> Create(Create{{entity.Name}}Request request, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request.{{requestMapper}}(), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, {{entity.Name}}Response.FromDto(result));
    }

    /// <summary>
    /// Updates an existing {{entity.Name}} by its unique identifier.
    /// </summary>
    /// <param name="id">Unique identifier of the {{entity.Name}} to update.</param>
    /// <param name="request">Request payload containing the updated values.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous request.</param>
    /// <returns>An HTTP 200 response with the updated {{entity.Name}}, or HTTP 404 when it is not found.</returns>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, Update{{entity.Name}}Request request, CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(id, request.{{requestMapper}}(), cancellationToken);
        return result is null
            ? NotFound(new ErrorResponse("{{entity.Name.ToUpperInvariant()}}_NOT_FOUND", "{{entity.Name}} was not found."))
            : Ok({{entity.Name}}Response.FromDto(result));
    }

    /// <summary>
    /// Deletes an existing {{entity.Name}} by its unique identifier.
    /// </summary>
    /// <param name="id">Unique identifier of the {{entity.Name}} to delete.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous request.</param>
    /// <returns>An HTTP 204 response when deleted, or HTTP 404 when it is not found.</returns>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await service.DeleteAsync(id, cancellationToken)
            ? NoContent()
            : NotFound(new ErrorResponse("{{entity.Name.ToUpperInvariant()}}_NOT_FOUND", "{{entity.Name}} was not found."));

{{customActions}}
}
""";
    }

    private static string RenderApiRequest(string serviceName, EntityMetadata entity, string requestName, bool useCqrs)
    {
        var folder = useCqrs ? "Commands" : "DTOs";
        var suffix = useCqrs ? "Command" : "Input";
        var mapperName = useCqrs ? "ToCommand" : "ToInput";
        var contractKind = useCqrs ? "command" : "input";
        return $$"""
using {{serviceName}}.Application.Features.{{Pluralize(entity.Name)}}.{{folder}};

namespace {{serviceName}}.Api.Requests;

public sealed record {{requestName}}({{RenderParameters(entity.Fields.Where(field => field.Name != "Id"))}})
{
    /// <summary>
    /// Maps the API request payload to the application {{contractKind}} contract.
    /// </summary>
    /// <returns>The application {{contractKind}} created from this request.</returns>
    public {{requestName.Replace("Request", suffix)}} {{mapperName}}() => new({{RenderRequestValueArguments(entity)}});
}
""";
    }

    private static string RenderApiResponse(string serviceName, EntityMetadata entity) => $$"""
using {{serviceName}}.Application.Features.{{Pluralize(entity.Name)}}.DTOs;

namespace {{serviceName}}.Api.Responses;

public sealed record {{entity.Name}}Response({{RenderParameters(entity.Fields)}})
{
    /// <summary>
    /// Maps an application DTO to the API response contract.
    /// </summary>
    /// <param name="dto">Application DTO to map.</param>
    /// <returns>The API response created from the DTO.</returns>
    public static {{entity.Name}}Response FromDto({{entity.Name}}Dto dto) => new({{RenderValueArguments(entity, "dto")}});
}
""";

    private static string RenderErrorResponse(string serviceName) => $$"""
namespace {{serviceName}}.Api.Responses;

public sealed record ErrorResponse(string Code, string Message, IReadOnlyDictionary<string, string[]>? Details = null);
""";

    private static string RenderCorrelationMiddleware(string serviceName) => $$"""
namespace {{serviceName}}.Api.Middlewares;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    /// <summary>
    /// Ensures every request and response has a correlation identifier.
    /// </summary>
    /// <param name="context">Current HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var value)
            ? value.ToString()
            : Guid.NewGuid().ToString("N");

        context.Response.Headers[HeaderName] = correlationId;
        await next(context);
    }
}
""";

    private static string RenderExceptionMiddleware(string serviceName) => $$"""
using {{serviceName}}.Api.Responses;

namespace {{serviceName}}.Api.Middlewares;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    /// <summary>
    /// Converts unhandled exceptions into a standardized API error response.
    /// </summary>
    /// <param name="context">Current HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled request error.");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new ErrorResponse("INTERNAL_ERROR", "An internal error occurred."));
        }
    }
}
""";

    private static string RenderSettings(string name) => $$"""
{
  "ConnectionStrings": {
    "PostgreSql": "Host=127.0.0.1;Port=5432;Database={{name.ToLowerInvariant()}};Username=postgres;Password=CHANGE_ME"
  },
  "Redis": {
    "ConnectionString": "127.0.0.1:6379",
    "InstanceName": "{{name}}:"
  },
  "RabbitMq": {
    "HostName": "127.0.0.1",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest"
  },
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      { "Name": "Console" }
    ]
  },
  "AllowedHosts": "*"
}
""";

    private static string RenderLaunchSettings() => """
{
  "$schema": "http://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "launchBrowser": true,
      "launchUrl": "swagger",
      "dotnetRunMessages": true,
      "applicationUrl": "http://localhost:5080",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
""";

    private static string RenderDockerCompose(string name) => $$"""
services:
  postgres:
    image: postgres:16
    environment:
      POSTGRES_DB: {{name.ToLowerInvariant()}}
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: CHANGE_ME
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"

  rabbitmq:
    image: rabbitmq:3-management
    ports:
      - "5672:5672"
      - "15672:15672"

  elasticsearch:
    image: docker.elastic.co/elasticsearch/elasticsearch:8.15.3
    environment:
      discovery.type: single-node
      xpack.security.enabled: "false"
      ES_JAVA_OPTS: "-Xms512m -Xmx512m"
    ports:
      - "9200:9200"
    volumes:
      - elasticsearch_data:/usr/share/elasticsearch/data

  kibana:
    image: docker.elastic.co/kibana/kibana:8.15.3
    depends_on:
      - elasticsearch
    ports:
      - "5601:5601"
    environment:
      ELASTICSEARCH_HOSTS: http://elasticsearch:9200

  logstash:
    image: docker.elastic.co/logstash/logstash:8.15.3
    depends_on:
      - elasticsearch
    ports:
      - "5044:5044"

volumes:
  postgres_data:
  elasticsearch_data:
""";

    private static string RenderKubernetes(string name)
    {
        var imageName = name.ToLowerInvariant();
        return $$"""
apiVersion: apps/v1
kind: Deployment
metadata:
  name: {{imageName}}
spec:
  replicas: 1
  selector:
    matchLabels:
      app: {{imageName}}
  template:
    metadata:
      labels:
        app: {{imageName}}
    spec:
      containers:
        - name: {{imageName}}
          image: {{imageName}}:latest
          ports:
            - containerPort: 8080
---
apiVersion: v1
kind: Service
metadata:
  name: {{imageName}}
spec:
  selector:
    app: {{imageName}}
  ports:
    - port: 80
      targetPort: 8080
""";
    }

    private static string RenderHelmChart(string name) => string.Join(Environment.NewLine, new[]
    {
        "apiVersion: v2",
        $"name: {ToKebabCase(name)}",
        $"description: {name} microservice chart generated by IRMSGen",
        "type: application",
        "version: 0.1.0",
        "appVersion: \"1.0.0\"",
        string.Empty
    });

    private static string RenderHelmValues(string name) => string.Join(Environment.NewLine, new[]
    {
        "replicaCount: 1",
        string.Empty,
        "image:",
        $"  repository: {name.ToLowerInvariant()}",
        "  pullPolicy: IfNotPresent",
        "  tag: latest",
        string.Empty,
        "service:",
        "  type: ClusterIP",
        "  port: 80",
        "  targetPort: 8080",
        string.Empty,
        "ingress:",
        "  enabled: false",
        "  className: \"\"",
        "  annotations: {}",
        "  hosts:",
        $"    - host: {ToKebabCase(name)}.local",
        "      paths:",
        "        - path: /",
        "          pathType: Prefix",
        "  tls: []",
        string.Empty,
        "resources: {}",
        string.Empty,
        "autoscaling:",
        "  enabled: false",
        "  minReplicas: 1",
        "  maxReplicas: 3",
        "  targetCPUUtilizationPercentage: 80",
        string.Empty,
        "app:",
        "  environment: Production",
        "  connectionStrings:",
        $"    postgres: Host=postgres;Port=5432;Database={name.ToLowerInvariant()};Username=postgres;Password=CHANGE_ME",
        "  redis:",
        "    connectionString: redis:6379",
        "  rabbitmq:",
        "    hostName: rabbitmq",
        "    port: 5672",
        "    userName: guest",
        "    password: CHANGE_ME",
        string.Empty
    });

    private static string RenderHelmHelpers(string name)
    {
        var chartName = ToKebabCase(name);
        return string.Join(Environment.NewLine, new[]
        {
            "{{/*",
            "Expand the chart name.",
            "*/}}",
            "{{- define \"" + chartName + ".name\" -}}",
            "{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix \"-\" }}",
            "{{- end }}",
            string.Empty,
            "{{/*",
            "Create a fully qualified app name.",
            "*/}}",
            "{{- define \"" + chartName + ".fullname\" -}}",
            "{{- if .Values.fullnameOverride }}",
            "{{- .Values.fullnameOverride | trunc 63 | trimSuffix \"-\" }}",
            "{{- else }}",
            "{{- $name := default .Chart.Name .Values.nameOverride }}",
            "{{- if contains $name .Release.Name }}",
            "{{- .Release.Name | trunc 63 | trimSuffix \"-\" }}",
            "{{- else }}",
            "{{- printf \"%s-%s\" .Release.Name $name | trunc 63 | trimSuffix \"-\" }}",
            "{{- end }}",
            "{{- end }}",
            "{{- end }}",
            string.Empty,
            "{{/*",
            "Common labels.",
            "*/}}",
            "{{- define \"" + chartName + ".labels\" -}}",
            "helm.sh/chart: {{ include \"" + chartName + ".name\" . }}-{{ .Chart.Version | replace \"+\" \"_\" }}",
            "app.kubernetes.io/name: {{ include \"" + chartName + ".name\" . }}",
            "app.kubernetes.io/instance: {{ .Release.Name }}",
            "app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}",
            "app.kubernetes.io/managed-by: {{ .Release.Service }}",
            "{{- end }}",
            string.Empty
        });
    }

    private static string RenderHelmDeployment(string name)
    {
        var chartName = ToKebabCase(name);
        return string.Join(Environment.NewLine, new[]
        {
            "apiVersion: apps/v1",
            "kind: Deployment",
            "metadata:",
            "  name: {{ include \"" + chartName + ".fullname\" . }}",
            "  labels:",
            "    {{- include \"" + chartName + ".labels\" . | nindent 4 }}",
            "spec:",
            "  {{- if not .Values.autoscaling.enabled }}",
            "  replicas: {{ .Values.replicaCount }}",
            "  {{- end }}",
            "  selector:",
            "    matchLabels:",
            "      app.kubernetes.io/name: {{ include \"" + chartName + ".name\" . }}",
            "      app.kubernetes.io/instance: {{ .Release.Name }}",
            "  template:",
            "    metadata:",
            "      labels:",
            "        app.kubernetes.io/name: {{ include \"" + chartName + ".name\" . }}",
            "        app.kubernetes.io/instance: {{ .Release.Name }}",
            "    spec:",
            "      containers:",
            $"        - name: {ToKebabCase(name)}",
            "          image: \"{{ .Values.image.repository }}:{{ .Values.image.tag }}\"",
            "          imagePullPolicy: {{ .Values.image.pullPolicy }}",
            "          ports:",
            "            - name: http",
            "              containerPort: {{ .Values.service.targetPort }}",
            "              protocol: TCP",
            "          envFrom:",
            "            - configMapRef:",
            "                name: {{ include \"" + chartName + ".fullname\" . }}-config",
            "            - secretRef:",
            "                name: {{ include \"" + chartName + ".fullname\" . }}-secret",
            "          resources:",
            "            {{- toYaml .Values.resources | nindent 12 }}",
            string.Empty
        });
    }

    private static string RenderHelmService(string name)
    {
        var chartName = ToKebabCase(name);
        return string.Join(Environment.NewLine, new[]
        {
            "apiVersion: v1",
            "kind: Service",
            "metadata:",
            "  name: {{ include \"" + chartName + ".fullname\" . }}",
            "  labels:",
            "    {{- include \"" + chartName + ".labels\" . | nindent 4 }}",
            "spec:",
            "  type: {{ .Values.service.type }}",
            "  ports:",
            "    - port: {{ .Values.service.port }}",
            "      targetPort: http",
            "      protocol: TCP",
            "      name: http",
            "  selector:",
            "    app.kubernetes.io/name: {{ include \"" + chartName + ".name\" . }}",
            "    app.kubernetes.io/instance: {{ .Release.Name }}",
            string.Empty
        });
    }

    private static string RenderHelmIngress(string name)
    {
        var chartName = ToKebabCase(name);
        return string.Join(Environment.NewLine, new[]
        {
            "{{- if .Values.ingress.enabled -}}",
            "apiVersion: networking.k8s.io/v1",
            "kind: Ingress",
            "metadata:",
            "  name: {{ include \"" + chartName + ".fullname\" . }}",
            "  labels:",
            "    {{- include \"" + chartName + ".labels\" . | nindent 4 }}",
            "  {{- with .Values.ingress.annotations }}",
            "  annotations:",
            "    {{- toYaml . | nindent 4 }}",
            "  {{- end }}",
            "spec:",
            "  {{- with .Values.ingress.className }}",
            "  ingressClassName: {{ . }}",
            "  {{- end }}",
            "  rules:",
            "    {{- range .Values.ingress.hosts }}",
            "    - host: {{ .host | quote }}",
            "      http:",
            "        paths:",
            "          {{- range .paths }}",
            "          - path: {{ .path }}",
            "            pathType: {{ .pathType }}",
            "            backend:",
            "              service:",
            "                name: {{ include \"" + chartName + ".fullname\" $ }}",
            "                port:",
            "                  number: {{ $.Values.service.port }}",
            "          {{- end }}",
            "    {{- end }}",
            "{{- end }}",
            string.Empty
        });
    }

    private static string RenderHelmConfigMap(string name)
    {
        var chartName = ToKebabCase(name);
        return string.Join(Environment.NewLine, new[]
        {
            "apiVersion: v1",
            "kind: ConfigMap",
            "metadata:",
            "  name: {{ include \"" + chartName + ".fullname\" . }}-config",
            "  labels:",
            "    {{- include \"" + chartName + ".labels\" . | nindent 4 }}",
            "data:",
            "  ASPNETCORE_ENVIRONMENT: {{ .Values.app.environment | quote }}",
            "  Redis__ConnectionString: {{ .Values.app.redis.connectionString | quote }}",
            "  RabbitMq__HostName: {{ .Values.app.rabbitmq.hostName | quote }}",
            "  RabbitMq__Port: {{ .Values.app.rabbitmq.port | quote }}",
            string.Empty
        });
    }

    private static string RenderHelmSecret(string name)
    {
        var chartName = ToKebabCase(name);
        return string.Join(Environment.NewLine, new[]
        {
            "apiVersion: v1",
            "kind: Secret",
            "metadata:",
            "  name: {{ include \"" + chartName + ".fullname\" . }}-secret",
            "  labels:",
            "    {{- include \"" + chartName + ".labels\" . | nindent 4 }}",
            "type: Opaque",
            "stringData:",
            "  ConnectionStrings__PostgreSql: {{ .Values.app.connectionStrings.postgres | quote }}",
            "  RabbitMq__UserName: {{ .Values.app.rabbitmq.userName | quote }}",
            "  RabbitMq__Password: {{ .Values.app.rabbitmq.password | quote }}",
            string.Empty
        });
    }

    private static string RenderManifest(ServiceMetadata metadata) => JsonSerializer.Serialize(
        new
        {
            metadata.ServiceName,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            metadata.TargetFramework,
            metadata.Architecture,
            metadata.Database,
            Projects = new[] { "Api", "Application", "Contracts", "Domain", "Infrastructure" },
            Features = new[] { "CRUD", "DTO", "Repository", "Controller", "Redis", "RabbitMQ", "ELK", "DockerCompose", "Kubernetes", "Helm" }
        },
        JsonOptions);

    private static string RenderReadme(ServiceMetadata metadata)
    {
        var name = metadata.ServiceName;
        var applicationDescription = UsesCqrs(metadata)
            ? "Featureها، Commandها، Queryها، Handler/Serviceها، DTOها، Abstractionها، مدل پاسخ"
            : "Feature Serviceها، DTOها، Input Modelها، Operation Contractها، Abstractionها و مدل پاسخ";
        return $$"""
# {{name}}

این پروژه به صورت خودکار توسط IRMSGen تولید شده است. خروجی یک میکروسرویس مبتنی بر .NET 8 است و از PostgreSQL، Redis، RabbitMQ، ELK، Docker، Kubernetes و Helm پشتیبانی می‌کند.

## معرفی کلی

سرویس `{{name}}` از روی متادیتای تعریف‌شده در ویزارد پلتفرم ساخته شده است. در این ساختار، endpointهای HTTP داخل لایه API قرار می‌گیرند، منطق عملیات و متدها در لایه Application پیاده‌سازی می‌شود، مدل‌های دامنه در لایه Domain نگهداری می‌شوند، اتصال به دیتابیس و سرویس‌های بیرونی در Infrastructure قرار دارد، و DTOهای ارتباطی در Contracts تعریف می‌شوند.

## معماری سرویس

```text
Client / Consumer
    |
    v
{{name}}.Api
    کنترلرها، مدل‌های Request/Response، Middlewareها، Swagger
    |
    v
{{name}}.Application
    {{applicationDescription}}
    |
    v
{{name}}.Domain
    Entityها، ValueObjectها، Eventها
    |
    v
{{name}}.Infrastructure
    PostgreSQL، Redis، RabbitMQ، سرویس‌های خارجی، Security، EF Core
    |
    v
{{name}}.Contracts
    Integration DTOها و Eventها
```

## پروژه‌های Solution

| پروژه | مسئولیت |
| --- | --- |
| `src/{{name}}.Api` | لایه HTTP شامل Controllerها، مدل‌های Request/Response، Middlewareها و تنظیمات Swagger |
| `src/{{name}}.Application` | منطق عملیات، {{applicationDescription}} |
| `src/{{name}}.Domain` | مدل‌های دامنه شامل Entityها، ValueObjectها، Eventها و قراردادهای احتمالی دامنه |
| `src/{{name}}.Infrastructure` | پیاده‌سازی دیتابیس، Redis، RabbitMQ، سرویس‌های خارجی، امنیت و EF Core |
| `src/{{name}}.Contracts` | قراردادهای ارتباطی، Integration DTOها و Eventهای مشترک بین سرویس‌ها |

## ساختار فولدرهای تولیدشده

نمای کلی ساختار پروژه تولیدشده:

```text
{{name}}/
├── {{name}}.sln
├── metadata.json
├── .irmsgen/
│   ├── project.metadata.json
│   └── generation.manifest.json
├── src/
│   ├── {{name}}.Api/
│   │   ├── Controllers/
│   │   ├── Requests/
│   │   ├── Responses/
│   │   ├── Middlewares/
│   │   └── Program.cs
│   ├── {{name}}.Application/
│   │   ├── Abstractions/
│   │   ├── Common/
│   │   └── Features/
│   ├── {{name}}.Domain/
│   │   ├── Entities/
│   │   ├── ValueObjects/
│   │   └── Events/
│   ├── {{name}}.Infrastructure/
│   │   ├── Persistence/
│   │   ├── Caching/
│   │   ├── Messaging/
│   │   ├── ExternalServices/
│   │   └── Security/
│   └── {{name}}.Contracts/
│       ├── Events/
│       └── IntegrationDtos/
├── deploy/
│   ├── k8s/
│   └── helm/
└── docker-compose.yml
```

## دیتابیس و جدول‌ها

Provider دیتابیس: `{{metadata.Database}}`

در این بخش Entityهای تعریف‌شده در ویزارد به جدول‌های دیتابیس تبدیل شده‌اند. نام جدول‌ها به صورت snake_case تولید می‌شود.

{{RenderReadmeDatabase(metadata.Entities)}}

## متدها و API

همه endpointها داخل فولدر `src/{{name}}.Api/Controllers` تولید می‌شوند. فایل `Program.cs` فقط برای تنظیم DI، Middlewareها، Swagger و `MapControllers` استفاده می‌شود و منطق متدها داخل Controllerها قرار دارد.

{{RenderReadmeOperations(metadata.Entities, UsesCqrs(metadata))}}

## DTOها و قراردادها

در این بخش DTOهای ورودی، خروجی، Application DTO و Integration DTOهای تولیدشده برای هر Entity و متد نمایش داده شده‌اند.

{{RenderReadmeDtos(metadata.Entities)}}

## سرویس‌های خارجی

اگر در ویزارد برای متدی وابستگی به سرویس خارجی تعریف شده باشد، Abstraction و Client مربوط به آن در خروجی تولید می‌شود.

{{RenderReadmeExternalServices(metadata.Entities)}}

## Cache، پیام‌رسانی و امنیت

| قابلیت | فایل‌های تولیدشده | توضیح |
| --- | --- | --- |
| Redis cache | `Application/Abstractions/Caching/ICacheService.cs`, `Infrastructure/Caching/RedisCacheService.cs` | برای متدهایی استفاده می‌شود که گزینه Cache برای آن‌ها فعال شده باشد |
| RabbitMQ message log | `Application/Abstractions/Messaging/IMessageBus.cs`, `Infrastructure/Messaging/RabbitMqMessageBus.cs`, `Contracts/IntegrationDtos/ServiceOperationLog.cs` | برای ثبت لاگ عملیات و ارسال پیام به RabbitMQ استفاده می‌شود |
| Certificate encryption | `Application/Abstractions/Security/IEncryptionService.cs`, `Infrastructure/Security/JweEncryptionService.cs`, `Infrastructure/Security/CertificateOptions.cs` | برای متدهایی استفاده می‌شود که نیاز به رمزنگاری با certificate دارند |

## تنظیمات

تنظیمات runtime از `appsettings.json`، متغیرهای محیطی، ConfigMap/Secret در Kubernetes و `values.yaml` در Helm خوانده می‌شود.

کلیدهای مهم تنظیمات:

```json
{
  "ConnectionStrings": {
    "PostgreSql": "Host=postgres;Port=5432;Database={{name.ToLowerInvariant()}};Username=postgres;Password=CHANGE_ME"
  },
  "Redis": {
    "ConnectionString": "redis:6379",
    "InstanceName": "{{name}}:"
  },
  "RabbitMq": {
    "HostName": "rabbitmq",
    "Port": "5672",
    "UserName": "guest",
    "Password": "CHANGE_ME"
  },
  "Certificate": {
    "PublicKeyPath": "Cert/service-public-key.pem"
  }
}
```

## اجرای محلی

ابتدا وابستگی‌ها را با Docker Compose اجرا کنید:

```bash
docker compose up -d
```

سپس restore و اجرای API:

```bash
dotnet restore
dotnet run --project src/{{name}}.Api
```

آدرس Swagger:

```text
http://localhost:5080/swagger
```

endpoint سلامت سرویس:

```text
GET /health
```

## Build پروژه

```bash
dotnet build {{name}}.sln
```

## استقرار

استقرار با فایل Kubernetes:

```bash
kubectl apply -f deploy/k8s/deployment.yaml
```

استقرار با Helm Chart:

```bash
helm upgrade --install {{ToKebabCase(name)}} ./deploy/helm/{{ToKebabCase(name)}}
```

## لاگ و مانیتورینگ

فایل `docker-compose.yml` زیرساخت پایه ELK شامل Elasticsearch، Kibana و Logstash را ایجاد می‌کند. پروژه API همچنین Middlewareهای CorrelationId و مدیریت خطا را دارد تا trace و خطایابی سرویس ساده‌تر شود.

## متادیتای تولید

IRMSGen اطلاعات تولید پروژه را در فایل‌های زیر ذخیره می‌کند:

```text
metadata.json
.irmsgen/project.metadata.json
.irmsgen/generation.manifest.json
```

از این فایل‌ها می‌توان برای بررسی خروجی تولیدشده، audit، و بازتولید سرویس از همان متادیتای ویزارد استفاده کرد.
""";
    }

    private static IEnumerable<MethodMetadata> CustomMethods(EntityMetadata entity)
    {
        var defaultNames = new HashSet<string>(StringComparer.Ordinal)
        {
            $"Create{entity.Name}",
            $"Update{entity.Name}",
            $"Get{entity.Name}ById",
            $"Search{Pluralize(entity.Name)}",
            $"Delete{entity.Name}"
        };

        return entity.Methods
            .Where(method => !defaultNames.Contains(method.Name))
            .GroupBy(method => method.Name, StringComparer.Ordinal)
            .Select(group => group.First());
    }

    private static IReadOnlyList<string> ExternalServiceNames(IEnumerable<EntityMetadata> entities) =>
        entities
            .SelectMany(entity => entity.Methods)
            .Where(method => method.UsesExternalService)
            .Select(method => string.IsNullOrWhiteSpace(method.ExternalServiceName) ? "ExternalService" : method.ExternalServiceName)
            .Select(name => SanitizeIdentifier(name, "ExternalService"))
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static string RenderExternalServiceUsings(string serviceName, IEnumerable<EntityMetadata> entities)
    {
        var usings = ExternalServiceNames(entities)
            .Select(externalServiceName => $"using {serviceName}.Infrastructure.ExternalServices.{externalServiceName};");

        return string.Join(Environment.NewLine, usings);
    }

    private static string RenderReadmeDatabase(IEnumerable<EntityMetadata> entities)
    {
        var builder = new StringBuilder();
        foreach (var entity in entities)
        {
            builder.AppendLine($"### `{ToSnakeCase(Pluralize(entity.Name))}`");
            builder.AppendLine();
            builder.AppendLine($"Entity متناظر: `{entity.Name}`");
            builder.AppendLine();
            builder.AppendLine("| ستون | نوع CLR | اجباری | کلید |");
            builder.AppendLine("| --- | --- | --- | --- |");
            foreach (var field in entity.Fields)
            {
                var column = ToSnakeCase(field.Name);
                var key = field.Name == "Id" && field.Type == "Guid" ? "کلید اصلی" : string.Empty;
                builder.AppendLine($"| `{column}` | `{field.Type}` | `{ToPersianBool(field.Required)}` | {key} |");
            }

            builder.AppendLine();
        }

        return builder.Length == 0
            ? "هیچ Entity برای تولید جدول دیتابیس تعریف نشده است."
            : builder.ToString();
    }

    private static string RenderReadmeOperations(IEnumerable<EntityMetadata> entities, bool useCqrs)
    {
        var builder = new StringBuilder();
        foreach (var entity in entities)
        {
            builder.AppendLine($"### متدهای `{entity.Name}`");
            builder.AppendLine();
            builder.AppendLine("| نام متد | Verb | Route | نوع عملیات | DTO ورودی | DTO خروجی | Cache | سرویس خارجی | RabbitMQ Log | Encryption |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");

            var operations = entity.Methods.Count == 0
                ? DefaultReadmeMethods(entity)
                : entity.Methods;

            foreach (var method in operations)
            {
                builder.AppendLine(
                    $"| `{method.Name}` | `{method.HttpMethod}` | `{method.Route}` | `{ToPersianOperationType(method.OperationType, useCqrs)}` | `{Display(method.RequestDto)}` | `{Display(method.ResponseDto)}` | `{ToPersianBool(method.UsesCache)}` | `{Display(method.ExternalServiceName)}` | `{ToPersianBool(method.UsesMessageLog)}` | `{ToPersianBool(method.UsesEncryption)}` |");
            }

            builder.AppendLine();
        }

        return builder.Length == 0
            ? "هیچ متد API برای این سرویس تعریف نشده است."
            : builder.ToString();
    }

    private static string RenderReadmeDtos(IEnumerable<EntityMetadata> entities)
    {
        var builder = new StringBuilder();
        foreach (var entity in entities)
        {
            builder.AppendLine($"### DTOهای `{entity.Name}`");
            builder.AppendLine();
            builder.AppendLine($"- DTO لایه Application: `src/*Application/Features/{Pluralize(entity.Name)}/DTOs/{entity.Name}Dto.cs`");
            builder.AppendLine($"- مدل خروجی API: `src/*Api/Responses/{entity.Name}Response.cs`");
            builder.AppendLine($"- مدل ورودی Create: `src/*Api/Requests/Create{entity.Name}Request.cs`");
            builder.AppendLine($"- مدل ورودی Update: `src/*Api/Requests/Update{entity.Name}Request.cs`");
            builder.AppendLine($"- DTO ارتباطی بین سرویس‌ها: `src/*Contracts/IntegrationDtos/{entity.Name}IntegrationDto.cs`");

            var customDtos = entity.Methods
                .SelectMany(method => new[] { method.RequestDto, method.ResponseDto })
                .Where(dto => !string.IsNullOrWhiteSpace(dto))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (customDtos.Count > 0)
            {
                builder.AppendLine("- نام DTOهای اختصاصی متدها بر اساس metadata:");
                foreach (var dto in customDtos)
                {
                    builder.AppendLine($"  - `{dto}`");
                }
            }

            builder.AppendLine();
        }

        return builder.Length == 0
            ? "هیچ DTO برای این سرویس تعریف نشده است."
            : builder.ToString();
    }

    private static string RenderReadmeExternalServices(IEnumerable<EntityMetadata> entities)
    {
        var externalServices = ExternalServiceNames(entities);
        if (externalServices.Count == 0)
        {
            return "برای این سرویس هیچ وابستگی به API خارجی انتخاب نشده است.";
        }

        var builder = new StringBuilder();
        builder.AppendLine("| سرویس | قرارداد Application | Client در Infrastructure | تنظیمات |");
        builder.AppendLine("| --- | --- | --- | --- |");
        foreach (var externalService in externalServices)
        {
            builder.AppendLine($"| `{externalService}` | `Application/Abstractions/ExternalServices/I{externalService}Client.cs` | `Infrastructure/ExternalServices/{externalService}/{externalService}Client.cs` | `Infrastructure/ExternalServices/{externalService}/{externalService}Options.cs` |");
        }

        return builder.ToString();
    }

    private static IReadOnlyList<MethodMetadata> DefaultReadmeMethods(EntityMetadata entity)
    {
        var route = $"/api/{ToKebabCase(Pluralize(entity.Name))}";
        return
        [
            new() { Name = $"Create{entity.Name}", HttpMethod = "POST", Route = route, OperationType = "Command", RequestDto = $"Create{entity.Name}Request", ResponseDto = $"{entity.Name}Response" },
            new() { Name = $"Update{entity.Name}", HttpMethod = "PUT", Route = $"{route}/{{id}}", OperationType = "Command", RequestDto = $"Update{entity.Name}Request", ResponseDto = $"{entity.Name}Response" },
            new() { Name = $"Get{entity.Name}ById", HttpMethod = "GET", Route = $"{route}/{{id}}", OperationType = "Query", ResponseDto = $"{entity.Name}Response" },
            new() { Name = $"Search{Pluralize(entity.Name)}", HttpMethod = "GET", Route = route, OperationType = "Query", ResponseDto = $"{entity.Name}Response", UsesPaging = true },
            new() { Name = $"Delete{entity.Name}", HttpMethod = "DELETE", Route = $"{route}/{{id}}", OperationType = "Command" }
        ];
    }

    private static string Display(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value;

    private static bool UsesCqrs(ServiceMetadata metadata) =>
        metadata.Architecture.Contains("CQRS", StringComparison.OrdinalIgnoreCase);

    private static string CustomMethodSuffix(MethodMetadata method, bool useCqrs) =>
        useCqrs
            ? method.OperationType == "Command" ? "Command" : "Query"
            : "Operation";

    private static string OperationContractDescription(MethodMetadata method, bool useCqrs) =>
        useCqrs ? method.OperationType.ToLowerInvariant() : "application";

    private static string ToPersianBool(bool value) => value ? "بله" : "خیر";

    private static string ToPersianOperationType(string value, bool useCqrs) =>
        useCqrs
            ? value == "Command" ? "Command - تغییر داده" : "Query - خواندن داده"
            : value == "Command" ? "Write - تغییر داده" : "Read - خواندن داده";

    private static string RenderFeatureCustomMethods(EntityMetadata entity, bool useCqrs)
    {
        var methods = CustomMethods(entity).ToList();
        if (methods.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            methods.Select(method =>
            {
                var suffix = CustomMethodSuffix(method, useCqrs);
                return $$"""
    /// <summary>
    /// Executes the generated {{method.OperationType.ToLowerInvariant()}} operation `{{method.Name}}`.
    /// </summary>
    /// <param name="request">Operation metadata and feature flags captured from the visual wizard.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>An object describing the executed operation and enabled capabilities.</returns>
    public Task<object> {{method.Name}}Async({{method.Name}}{{suffix}} request, CancellationToken cancellationToken)
    {
        var result = new
        {
            Operation = "{{method.Name}}",
            OperationType = "{{method.OperationType}}",
            HttpMethod = "{{method.HttpMethod}}",
            Route = "{{method.Route}}",
            request.UsesDatabase,
            request.UsesExternalService,
            request.ExternalServiceName,
            request.UsesPaging,
            request.UsesCache,
            request.UsesMessageLog,
            request.UsesEncryption
        };

        return Task.FromResult<object>(result);
    }
""";
            }));
    }

    private static string RenderControllerCustomActions(EntityMetadata entity, bool useCqrs)
    {
        var methods = CustomMethods(entity).ToList();
        if (methods.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            methods.Select(method =>
            {
                var suffix = CustomMethodSuffix(method, useCqrs);
                var attribute = RenderHttpAttribute(method);
                return $$"""
    /// <summary>
    /// Handles the `{{method.Name}}` endpoint generated from visual metadata.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the asynchronous request.</param>
    /// <returns>An HTTP 200 response containing the generated operation result.</returns>
    {{attribute}}
    public async Task<IActionResult> {{method.Name}}(CancellationToken cancellationToken)
    {
        var result = await service.{{method.Name}}Async(new {{method.Name}}{{suffix}}(), cancellationToken);
        return Ok(result);
    }
""";
            }));
    }

    private static string RenderHttpAttribute(MethodMetadata method)
    {
        var verb = method.HttpMethod.ToUpperInvariant() switch
        {
            "POST" => "HttpPost",
            "PUT" => "HttpPut",
            "PATCH" => "HttpPatch",
            "DELETE" => "HttpDelete",
            _ => "HttpGet"
        };

        return $"[{verb}(\"{RenderActionRoute(method.Route)}\")]";
    }

    private static string RenderActionRoute(string route)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            return string.Empty;
        }

        var trimmed = route.Trim();
        return trimmed.StartsWith('/')
            ? $"~{trimmed}"
            : trimmed;
    }

    private static string RenderParameters(IEnumerable<FieldMetadata> fields) =>
        string.Join(", ", fields.Select(field => $"{MakeNullable(field.Type, field.Required)} {field.Name}"));

    private static string RenderValueArguments(EntityMetadata entity, string variable) =>
        string.Join(", ", entity.Fields.Select(field => $"{variable}.{field.Name}"));

    private static string RenderRequestValueArguments(EntityMetadata entity) =>
        string.Join(", ", entity.Fields.Where(field => field.Name != "Id").Select(field => field.Name));

    private static string RenderCreateAssignments(EntityMetadata entity, string source)
    {
        var assignments = entity.Fields.Select(field =>
        {
            var value = field.Name == "Id" && field.Type == "Guid"
                ? "Guid.NewGuid()"
                : $"{source}.{field.Name}";
            return $"            {field.Name} = {value}";
        });

        return string.Join($",{Environment.NewLine}", assignments);
    }

    private static string RenderUpdateAssignments(EntityMetadata entity, string target, string source) =>
        string.Join(Environment.NewLine, entity.Fields
            .Where(field => field.Name != "Id")
            .Select(field => $"        {target}.{field.Name} = {source}.{field.Name};"));

    private static string MakeNullable(string type, bool required)
    {
        if (required || type == "Guid")
        {
            return type;
        }

        return type == "string" ? "string?" : $"{type}?";
    }

    private static string Pluralize(string value) => value.EndsWith('s') ? value : $"{value}s";

    private static string ToKebabCase(string value)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (char.IsUpper(character) && index > 0)
            {
                builder.Append('-');
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    private static string ToSnakeCase(string value) => ToKebabCase(value).Replace('-', '_');

    private static string SanitizeIdentifier(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var builder = new StringBuilder();
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character) || character == '_')
            {
                builder.Append(character);
            }
        }

        if (builder.Length == 0)
        {
            return fallback;
        }

        if (char.IsDigit(builder[0]))
        {
            builder.Insert(0, '_');
        }

        return builder.ToString();
    }
}
