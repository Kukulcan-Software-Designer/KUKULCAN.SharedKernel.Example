# KUKULCAN.SharedKernel.Example Wiki
**Language:** [Spanish](Inicio)


Welcome to the project wiki for **KUKULCAN.SharedKernel.Example**, the reference application intended to demonstrate how the KUKULCAN shared-library ecosystem can be integrated into a business-oriented web application.

> **Project status — 13 October 2026:** the repository is in its foundation and planning stage. The `.NET 10` solution and GitHub Actions workflows exist, but the API, test projects, domain implementation, and four user interfaces are not yet present as tracked projects. Coverage is therefore **not yet measurable**. A green CI run must not be interpreted as proof that application code or tests are being exercised.

## Wiki navigation

### Project and architecture

- [Project Overview](Project-Overview) — purpose, scope, current status, and non-goals.
- [Architecture](Architecture) — target responsibilities, boundaries, and request flow.
- [Technology Stack](Technology-Stack) — existing and planned technologies.
- [Domain Model](Domain-Model) — Sample entities, relationships, searches, and lifecycle rules.
- [Database and Migrations](Database-and-Migrations) — provider-neutral persistence and database configuration.

### Security and integrations

- [Authentication and Security](Authentication-and-Security) — login, administrator bootstrap, authorization, and secrets.
- [KUKULCAN Integrations](KUKULCAN-Integrations) — SharedKernel, Auth, Database, JsonEngine, and I18N.
- [Web Interfaces](Web-Interfaces) — Razor Pages, Blazor, Angular, and React.

### Engineering and delivery

- [Testing Strategy](Testing-Strategy) — unit, integration, contract, and end-to-end testing.
- [Code Coverage](Code-Coverage) — coverage workflow, current measurement status, and how to interpret the result.
- [CI and Quality Gates](CI-and-Quality-Gates) — current workflows and known limitations.
- [Development Workflow](Development-Workflow) — the required TEST → RED → Source → GREEN → Coverage sequence.
- [Roadmap and Milestones](Roadmap-and-Milestones) — the six project Milestones and all 22 Issues.
- [Troubleshooting](Troubleshooting) — common setup, CI, and coverage questions.

## Repository links

- [Repository](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example)
- [Develop branch](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/tree/Develop)
- [GitHub Issues](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues)
- [GitHub Actions](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/actions)
- [CI workflow](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/blob/Develop/.github/workflows/ci.yml)
- [Code Coverage workflow](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/blob/Develop/.github/workflows/coverage.yml)

## Documentation conventions

This wiki distinguishes three states:

- **Implemented and verified:** confirmed in repository source or an observed successful test/run.
- **Configured but not yet validated:** configuration exists, but the required application/test projects or a successful execution are missing.
- **Planned:** specified in Issues and Milestones but not yet implemented.

The current state should be updated as the repository evolves. Do not document planned functionality as already available, and do not publish a code-coverage percentage unless it comes from an actual generated report.
