# CI and Quality Gates
**Language:** [Spanish](CI-y-Criterios-de-Calidad)


## Observed workflows on `Develop`

| Workflow | Configuration | Current limitations |
|---|---|---|
| [CI](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/blob/Develop/.github/workflows/ci.yml) | .NET 10 restore, Release build, and test for the `.slnx` | Latest run was green but restore reported no project to restore; the solution currently contains no actual projects. |
| [Reference Client](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/blob/Develop/.github/workflows/client.yml) | Restores and builds the expected Example Web API `.csproj` | The referenced `.csproj` is not present in the current `Develop` tree. It cannot provide meaningful validation until that project exists. |
| [Code Coverage](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/blob/Develop/.github/workflows/coverage.yml) | XPlat Code Coverage, ReportGenerator, Cobertura/HTML, artifact upload | No testable projects exist yet and no verified report is available. |
| [Delete merged branches](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/blob/Develop/.github/workflows/delete-merged-branches.yml) | Intended to delete merged same-repository source branches while preserving `main` and `Develop` | The latest observed run failed. The workflow file must be reviewed before relying on automatic cleanup; it contains a repeated `env` key in one step. |

## Verified CI observation

The latest observed CI run is [run #1, 13 October 2026](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/actions/runs/38419497568). It concluded successfully, but restore output included:

```text
warning : Unable to find a project to restore!
```

The build reported zero warnings and errors, which does not mean the solution compiled application code: there were no projects to build. The test command also had no declared test project to exercise. The run is currently proof that the workflow commands complete against an empty solution, not a passing application regression suite.

## Intended quality gates

Once projects exist, CI should validate:

- deterministic restore and Release build on .NET 10;
- all unit tests and configured integration tests, with external dependencies explicitly provisioned;
- API and each web client's production build;
- coverage output that contains real production assemblies and executed tests;
- provider-backed integration tests for each claimed database provider;
- safe workflow permissions, branch cleanup rules, and fork handling;
- no committed or emitted secrets.

## Branch cleanup expectations

The cleanup workflow must run only after a merged pull request, delete only a same-repository source branch, and never delete `main` or `Develop`. Workflow syntax and permission requirements must be validated. It should not be treated as a mandatory quality gate until the observed failure has been fixed and the workflow has passed an actual PR-close scenario.

## Quality policy

A green status is meaningful only when the intended projects, jobs, and assertions are actually present and run. Do not state that tests, coverage, provider compatibility, or client builds pass based only on a workflow's `success` conclusion when the workflow did not execute those checks against real projects.
