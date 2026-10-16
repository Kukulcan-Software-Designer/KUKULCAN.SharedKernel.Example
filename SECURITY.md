# Security Policy

## Supported Versions

Only the latest maintained release of `KUKULCAN.SharedKernel.Example` is actively supported for security fixes.

Security fixes must consider the Example application and its integration with the KUKULCAN shared libraries. Where a vulnerability originates in a dependency, the corresponding upstream project must also be notified when appropriate.

## Reporting a Vulnerability

Do not report vulnerabilities through public issues or discussions. Send responsible-disclosure reports privately to **jpardo.kukulcan@gmail.com**. If GitHub Private Vulnerability Reporting is enabled, prefer that mechanism.

When reporting a vulnerability, include a description of the affected component, the potential impact, reproduction steps and any relevant logs or evidence. Do not include real passwords, access tokens, connection strings or other sensitive production data.

## Relevant Areas

Please report vulnerabilities affecting:

- Authentication, credential validation, password handling and administrator initialization.
- Authorization, access-token validation, session management and tenant isolation.
- Web API endpoints, input validation and exposure of sensitive information.
- Database configuration, connection strings, persistence operations and database migrations.
- Client, provider, product, warehouse, inventory, sales and purchase data access.
- Communication between the Web API and the Razor, Blazor, Angular and React interfaces.
- Exposure of secrets in browser code, application configuration, logs or error responses.
- Dependencies and integrations with `KUKULCAN.SharedKernel`, `KUKULCAN.SharedKernel.Auth` and `KUKULCAN.SharedKernel.Database`.
- Build, deployment and CI workflows that could expose credentials or compromise application artifacts.

## Security Principles

- Database connection strings, authentication signing keys and other secrets must be supplied through secure configuration and must not be committed to the repository.
- Sensitive information must not be exposed through API responses, logs, error messages or client-side code.
- Passwords must be processed using the supported secure password-hashing mechanisms and must never be stored as plaintext.
- Authentication and authorization must be enforced by the server.
- Tenant identity and access permissions must be derived from validated authentication context, not trusted solely from client-supplied values.
- Browser-based applications must not contain server-side credentials or signing keys.
- Database access must remain on the server side and use the established KUKULCAN persistence contracts where applicable.
- Input validation and appropriate authorization checks must be applied to operations that access or modify application data.
- Initial administrator credentials, if configured, must not be treated as secure permanent production credentials.
- Nullable reference types, warnings-as-errors and automated security-relevant tests should be maintained where supported by the solution configuration.
- Third-party dependencies must be kept under review and updated when security fixes are required.

## Response Process

1. Acknowledge the report and, where possible, establish a private