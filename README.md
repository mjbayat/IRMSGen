# IRMSGen

IRMSGen is a metadata-first microservice generator for .NET 8 and PostgreSQL.

## Projects

- `IRMSGen.App`: Blazor wizard UI
- `IRMSGen.Application`: workflows and orchestration
- `IRMSGen.Domain`: core project and metadata model
- `IRMSGen.Infrastructure`: database, Git, Kubernetes, secrets, schema readers
- `IRMSGen.Generator`: output templates and rendering
- `IRMSGen.Cli`: command-line entry point
- `IRMSGen.Contracts`: shared contracts

## Run

```bash
dotnet run --project src/IRMSGen.App
```

## Generate from CLI

```bash
dotnet run --project src/IRMSGen.Cli -- generate --input samples/order-service.json --output generated
```
