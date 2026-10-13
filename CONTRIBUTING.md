# Contributing to KUKULCAN.SharedKernel.Example

## Philosophy

Contributions should preserve this project's role as a reference application demonstrating the integration of the KUKULCAN ecosystem. Changes should solve a real functional or integration problem and maintain a clear separation between shared-library responsibilities, application-specific behavior and user-interface concerns.

The Example should demonstrate realistic application behavior without duplicating functionality already provided by `KUKULCAN.SharedKernel`, `KUKULCAN.SharedKernel.Auth` or `KUKULCAN.SharedKernel.Database`.

## Before Contributing

- Read `README.md`, `GOVERNANCE.md`, `SECURITY.md` and `SUPPORT.md`.
- Check existing GitHub Issues and Discussions.
- Verify that the proposed behavior is not already provided by an existing KUKULCAN shared library.
- Confirm that the change belongs to the Example application rather than a shared library.
- Review the existing solution structure, tests and integration patterns.
- Identify the affected components, API contracts, database entities and web interfaces.

## Public API

Keep the public surface minimal and consistent. New public types, endpoints, DTOs, interfaces and configuration options require a clear functional or consumer-facing purpose.

- Prefer established KUKULCAN contracts over duplicate abstractions.
- Keep implementation details private whenever possible.
- Maintain consistent API contracts and predictable HTTP status codes.
- Do not expose internal persistence entities, credentials or infrastructure details unnecessarily.
- Avoid introducing business rules or database access into user-interface projects.
- Document public endpoints and configuration options when they form part of the demonstrated integration contract.

Application-specific entities and contracts are appropriate when required by the Example's functionality, but reusable abstractions should remain in the corresponding shared libraries.

## Coding Standards

- Target .NET 10 for .NET projects, unless an explicitly documented architectural decision establishes otherwise.
- Keep nullable reference types enabled.
- Treat warnings