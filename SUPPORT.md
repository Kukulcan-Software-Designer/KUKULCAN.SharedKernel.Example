# Support

`KUKULCAN.SharedKernel.Example` is a reference application demonstrating the integration of KUKULCAN shared libraries, an ASP.NET Core Web API, database persistence, authentication and multiple web interfaces.

## Before Asking for Help

Check `README.md`, `GOVERNANCE.md`, `SECURITY.md`, `CHANGELOG.md` and the existing GitHub Issues and Discussions first.

For bugs, provide the repository version or commit, .NET version, relevant package versions, database provider and version, operating system, configuration details with secrets removed, exception, stack trace and a minimal reproduction where possible.

For issues involving a web interface, also identify the affected interface (Razor, Blazor, Angular or React), the browser and version, the API endpoint involved and the relevant HTTP status code or response, excluding sensitive information.

## Reporting Bugs

Use GitHub Issues for reproducible defects in the Example application.

Reports should identify the affected component and explain the expected and actual behavior. Include reproduction steps and relevant test results where possible.

## Architectural Questions

Use GitHub Discussions for questions about:

- Integration with `KUKULCAN.SharedKernel`, `KUKULCAN.SharedKernel.Auth` and `KUKULCAN.SharedKernel.Database`.
- Web API design, endpoint contracts and application architecture.
- Authentication, authorization, administrator initialization and session handling.
- Database configuration, persistence, migrations and provider integration.
- The Sample data model, including clients, providers, products, warehouses, inventory, sales and purchases.
- Integration of the Razor, Blazor, Angular and React interfaces with the Example Web API.
- Testing, CI workflows, code coverage and the intended purpose of the reference implementation.

## Feature Requests

Explain the functional or integration problem the proposed feature would solve and why it belongs in the Example application.

Requests should clarify whether the feature demonstrates an existing KUKULCAN capability, requires a new application-specific behavior or implies a change to a shared library.

The Example should remain a focused reference implementation and should not duplicate reusable functionality that belongs in the KUKULCAN shared libraries.

## Out of Scope

General C# programming, unrelated ASP.NET Core or EF Core support, database administration, IDE configuration, framework-specific troubleshooting unrelated to the Example, and application-specific requirements beyond the reference application's intended scope are outside this project's support scope.

Issues originating in a KUKULCAN shared library should be reported to the corresponding repository when appropriate.

## Security

Security vulnerabilities must follow `SECURITY.md` and must not be reported through public issues or discussions.