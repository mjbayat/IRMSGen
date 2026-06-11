# IRMSGen Architecture

IRMSGen is organized as a metadata-first service-generation platform.

```text
src/
├── IRMSGen.App             Blazor wizard UI
├── IRMSGen.Application     workflows, validation, orchestration
├── IRMSGen.Domain          project model, metadata rules, generation state
├── IRMSGen.Infrastructure  database, Git, Kubernetes, secrets, schema readers
├── IRMSGen.Generator       templates and code rendering
├── IRMSGen.Cli             command-line host
└── IRMSGen.Contracts       cross-boundary DTOs and contracts
```

The intended dependency direction is:

```text
App -> Application -> Domain
App -> Generator
Cli -> Application / Generator
Infrastructure -> Application / Domain / Contracts
Generator -> Domain
```

Generated services should use a feature-based clean architecture output with DTOs, error contracts, database mappings, routines, external-service clients, Git metadata, and deployment artifacts added progressively.
