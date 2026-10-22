# Project Overview
**Language:** [Spanish](Descripcion-del-Proyecto)


## Purpose

`KUKULCAN.SharedKernel.Example` is a reference application for demonstrating how reusable KUKULCAN libraries can be consumed by an application with a shared HTTP API and multiple independent web interfaces.

The intended example brings together:

- `KUKULCAN.SharedKernel` for shared primitives and application contracts.
- `KUKULCAN.SharedKernel.Auth` for the authentication capabilities supported by its public API.
- `KUKULCAN.SharedKernel.Database` for provider-configurable persistence, using its actual public contracts.
- `KUKULCAN.SharedKernel.JsonEngine` for a concrete JSON-processing use case.
- `KUKULCAN.SharedKernel.I18N` as a separately deployed Web API service.
- One Example Web API consumed by Razor Pages, Blazor, Angular, and React clients.

## Intended business demonstration

The data model is in the `Example` database schema and covers clients, providers, products, warehouses, sales, purchases, and warehouse stock. The API is expected to demonstrate ordinary create/read/update operations, appropriate search filters, authentication, data integrity, and the agreed historical-data policy.

## What this project is not

- It is not a replacement for the reusable responsibilities of the KUKULCAN libraries.
- It is not four independent backends. All four interfaces are clients of one common Example API.
- It is not a production ERP or CRM product. Its primary purpose is to demonstrate and verify integrations and patterns.
- It must not claim a database provider or front-end client is supported before its build and relevant behavior are validated.

## Repository status as of 13 October 2026

The `Develop` branch contains repository governance and documentation files, a `.NET 10` solution file, and GitHub Actions workflow files. The solution file currently contains solution folders and solution items but does not declare a production or test project. The tracked `Source/` and `Tests/` directories do not yet contain projects.

The observed CI run on 13 October 2026 completed successfully, but its restore step logged `Unable to find a project to restore!`; the build reported success with no compilation work. Therefore that green status does **not** establish that the Example application compiles or that any tests have run against production code.

The application, API, persistence model, test suite, and four interfaces remain planned work represented by GitHub Issues. Use the [Issue backlog](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues) for the accepted scope.

## Primary project deliverables

1. A buildable .NET 10 `.slnx` solution with API and automated test projects.
2. A documented architecture with explicit integration boundaries.
3. Secure authentication and idempotent administrator bootstrap using supported library contracts.
4. A Sample domain model and versioned schema initialization.
5. A common API with business rules, searches, and lifecycle policies.
6. Provider-backed validation for PostgreSQL, Microsoft SQL Server, and MySQL where the packages and implementation support them.
7. Four independently buildable, authenticated web clients.
8. Meaningful automated tests, real coverage reports, CI checks, and implementation-aligned documentation.

## Source of truth

- [Repository README](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/blob/Develop/README.md)
- [Governance](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/blob/Develop/GOVERNANCE.md)
- [Contributing](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/blob/Develop/CONTRIBUTING.md)
- [Security policy](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/blob/Develop/SECURITY.md)
