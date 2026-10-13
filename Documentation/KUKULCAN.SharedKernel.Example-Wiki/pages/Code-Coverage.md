# Code Coverage
**Language:** [Spanish](Cobertura-de-Codigo)


## Current measured percentage

**Current verified line coverage: N/A. Current verified branch coverage: N/A.** No successful coverage report has been observed for the Example as of 13 October 2026. This is not the same as reporting 0% coverage: a percentage is not meaningful until production assemblies are built, tests actually run against them, and a coverage report is generated.

## What is configured

The [`coverage.yml`](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/blob/Develop/.github/workflows/coverage.yml) workflow:

- uses .NET SDK `10.0.x`;
- restores and builds `KUKULCAN.SharedKernel.Example.slnx`;
- invokes `dotnet test` with the XPlat Code Coverage collector;
- requests Cobertura coverage output;
- limits collection to `[KUKULCAN.SharedKernel.Example.Web]*` and excludes compiler-generated code by attribute;
- uses ReportGenerator to create HTML, Cobertura, and text summary reports;
- uploads the reports as the `example-code-coverage` artifact for 14 days.

The workflow is configured for pushes to `main`, pull requests targeting `main`, and manual dispatch. It has not appeared in the latest observed Actions runs; the latest listed runs were CI and branch cleanup.

## Why there is no percentage yet

The current solution file contains only solution folders/items and does not declare a Web API or test project. The restore log from the latest successful CI run explicitly reports `Unable to find a project to restore!`. The build then succeeds with no compilation work. No source/test assemblies exist for Coverlet to measure, and the configured include filter expects an assembly named `KUKULCAN.SharedKernel.Example.Web`.

Therefore the right status is **not yet measurable**, not “coverage is capped at X%” and not “the project has zero coverage.” The configured collector/reporting steps alone do not create a valid result when no testable production assembly and tests exist.

## Why coverage may stop increasing later

Once real projects and tests exist, the percentage can plateau for legitimate reasons:

- some code is defensive, platform-specific, or only reachable under explicit external failures;
- line coverage may be high while branches, authorization combinations, or provider-specific behavior remain untested;
- integration and UI code may not be included in the current assembly filter;
- generated code and auto-properties are intentionally excluded;
- coverage tools measure execution, not whether assertions are meaningful.

A plateau must be investigated against uncovered lines/branches and actual risks. Do not add artificial tests merely to raise the percentage. Add tests where they prove business, security, persistence, integration, or regression behavior. A target threshold must be explicitly agreed and configured; none is defined in the current workflow.

## Process for publishing a trustworthy report

1. Add the real API and test projects to the `.slnx`.
2. Confirm the test project references its test framework, runner/adapter, and `coverlet.collector`.
3. Run the tests locally with the same runsettings and configuration as CI.
4. Inspect the generated Cobertura file and ReportGenerator summary for non-empty assemblies, executed lines, and branches.
5. Verify the include filter matches the actual production assembly names.
6. Run Code Coverage from GitHub Actions and download `example-code-coverage`.
7. Record the line and branch percentages with workflow-run URL and commit SHA; include dates, and do not treat missing reports as 0%.

## Links

- [Code Coverage workflow](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/blob/Develop/.github/workflows/coverage.yml)
- [GitHub Actions runs](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/actions)
- [Testing Strategy](Testing-Strategy)
