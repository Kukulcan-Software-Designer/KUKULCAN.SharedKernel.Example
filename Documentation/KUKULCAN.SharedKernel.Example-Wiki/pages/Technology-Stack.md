# Technology Stack
**Language:** [Spanish](Tecnologias)


## Technologies already visible in the repository

| Technology | Observed use | Current status |
|---|---|---|
| .NET SDK 10.0.x | CI setup and target solution | Configured; no application project yet |
| `.slnx` | `KUKULCAN.SharedKernel.Example.slnx` | Present, currently solution folders/items only |
| GitHub Actions | CI, reference-client, code-coverage, merged-branch cleanup workflows on `Develop` | Workflow definitions present; several checks cannot validate missing projects yet |
| Markdown | Repository governance and project documentation | Present |

`Directory.Build.props` currently declares version metadata `1.0.0` and enables NuGet audit (`NuGetAudit=true`). This does not imply that an application package or release has been built.

## Target backend stack

- ASP.NET Core Web API targeting .NET 10.
- Entity Framework Core and persistence through the public contracts of `KUKULCAN.SharedKernel.Database`.
- `KUKULCAN.SharedKernel` shared contracts.
- `KUKULCAN.SharedKernel.Auth` authentication capabilities supported by its published package API.
- `KUKULCAN.SharedKernel.JsonEngine` for an explicit application-level JSON use case.
- HTTP integration with the separately deployed `KUKULCAN.SharedKernel.I18N` API.

Exact package versions should be derived from the project files and supported package releases once the application projects are created; they must not be guessed in this wiki.

## Target client stack

| Interface | Intended technology | Status |
|---|---|---|
| Razor Pages | ASP.NET Core Razor Pages | Planned, project not yet present |
| Blazor | Blazor | Planned; hosting model not yet decided |
| Angular | Angular + Node.js toolchain | Planned, package manifest not yet present |
| React | React + Node.js toolchain | Planned, package manifest not yet present |

All clients will consume the same Example Web API. Exact framework versions, package-manager choice, build commands, browser testing framework, and supported browsers must be recorded once the clients are implemented.

## Database targets

PostgreSQL, Microsoft SQL Server, and MySQL are validation targets. They should be described as supported only after the Example has the relevant provider package/configuration and provider-backed migration, persistence, query, and lifecycle tests passing.

## Test and reporting tools

The current Code Coverage workflow configures the XPlat Code Coverage collector and ReportGenerator. There is not yet a test project in the solution, so a test framework has not been verified as active in this repository. The test framework, adapter versions, and coverage settings must be explicit in the test project and its workflow after implementation.
