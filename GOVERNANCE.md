# Governance

## Purpose

`KUKULCAN.SharedKernel.Example` is governed as a reference application that demonstrates how the KUKULCAN ecosystem components can be integrated into a functional solution. Decisions must protect architectural clarity, reproducibility, maintainability and consistency across the demonstrated integration patterns.

The repository must provide a practical example of how to build a Web API, integrate shared infrastructure libraries, persist data, authenticate users and expose application functionality through multiple web interfaces.

## Principles

- **Single responsibility:** the Example demonstrates ecosystem integration and application behavior; it must not duplicate responsibilities already provided by KUKULCAN shared libraries.
- **Low coupling:** consume established contracts from `KUKULCAN.SharedKernel`, `KUKULCAN.SharedKernel.Auth` and `KUKULCAN.SharedKernel.Database` instead of duplicating their abstractions.
- **Explicitness:** application behavior, authentication requirements, persistence configuration and external dependencies must be visible in configuration or clearly named components.
- **Stability:** existing integration examples and public contracts should change only when justified by a functional requirement, a compatibility defect or a documented architectural improvement.
- **Provider configurability:** database provider selection and connection strings must be supplied through configuration or environment variables. Credentials and secrets must not be hard-coded in source code or committed to the repository.
- **Separation of concerns:** API, application logic, persistence, authentication and user interfaces must remain clearly separated.
- **Consistent API consumption:** the Razor, Blazor, Angular and React interfaces must consume the same Example Web API rather than implement independent business or persistence logic.
- **Security by default:** authentication and authorization must be enforced by the server. Browser applications must not contain server-side secrets, and tenant context must not be trusted solely because it was supplied by a client.
- **Demonstrable behavior:** examples must represent real application behavior and must not introduce artificial implementation or tests solely to satisfy coverage metrics.

## Decision Process

1. Identify the functional requirement or integration problem that the Example must demonstrate.
2. Determine whether the responsibility belongs to an existing KUKULCAN library, the Example Web API, a web interface or a dedicated infrastructure component.
3. Reuse established public contracts wherever possible and avoid unnecessary dependencies or duplicated abstractions.
4. Assess the impact on the solution structure, public API, persistence model, authentication, supported database providers and existing web interfaces.
5. Define behavior-focused tests before implementing the corresponding production changes.
6. Verify that the tests fail for the expected reason before changing production code.
7. Implement the minimum required functionality and verify that the relevant tests pass.
8. Validate build quality, integration behavior and coverage using the repository's configured quality checks.
9. Update the documentation to describe the demonstrated behavior, configuration requirements and any relevant architectural decisions.

## Frozen Components

Existing, validated integration patterns and contracts should not be changed without evidence of a real requirement, a compatibility defect or a documented architectural improvement.

In particular:

- Do not duplicate functionality already provided by the KUKULCAN shared libraries.
- Do not introduce a concrete database provider into a component whose purpose is to remain provider-neutral.
- Do not change authentication or persistence contracts merely to simplify an individual endpoint or user interface.
- Do not introduce independent business rules or persistence mechanisms into Razor, Blazor, Angular or React clients.
- Do not remove established functionality, tests or documentation without identifying the intended scope and consequences of the change.

When a change affects an established integration pattern, its purpose and compatibility implications must be documented and verified through appropriate tests.

## Public API Policy

New public types, endpoints, DTOs, interfaces and configuration options must have a clear functional or consumer-facing purpose.

- Prefer existing contracts from the KUKULCAN ecosystem over duplicate definitions.
- Keep implementation details private whenever possible.
- Design API endpoints and request/response contracts consistently across the Example application.
- Validate incoming data and return predictable HTTP status codes and error responses.
- Do not expose internal persistence entities or infrastructure