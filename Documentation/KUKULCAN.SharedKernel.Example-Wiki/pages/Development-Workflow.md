# Development Workflow
**Language:** [Spanish](Flujo-de-Desarrollo)


## Working agreement

Development must be test-driven and behavior-focused. The current agreed workflow for the Example is:

**TEST → RED → Source → GREEN → Coverage**

The current project-owner instruction is to work directly on `Develop` for this repository unless that working agreement is explicitly changed. Do not assume the feature-branch approach used by other KUKULCAN repositories automatically applies here.

## Phase definitions

### 1. TEST

Select an acceptance criterion from an Issue and add a test that expresses observable behavior. Tests should cover a real requirement, error, rule, integration contract, or regression risk.

### 2. RED

Run the test and confirm it fails for the intended missing behavior. If the agreed process requires a published GitHub Actions RED before implementation, wait until that result is real and inspect the failure reason. A compile failure unrelated to the requested behavior is not a valid substitute.

### 3. Source

Implement the minimum production change required to satisfy the behavior. Reuse the public KUKULCAN package contracts and avoid duplicated infrastructure.

### 4. GREEN

Run the relevant focused tests and then the solution test suite. Confirm the new behavior passes and existing behavior remains valid. A workflow that finds no projects is not a meaningful GREEN for application behavior.

### 5. Coverage

Generate a real coverage report and inspect uncovered lines/branches. Add tests because they prove valuable behavior, not solely to hit a target percentage. If coverage does not measure the expected production assemblies, correct the configuration before interpreting the percentage.

## Integration and documentation

For database or external-service work, record the provider/service prerequisites, local commands, required environment variables, and expected failure handling. Do not commit secrets. For any changed public contract or supported integration, update the relevant wiki page and repository documentation.

## Completion criteria for an Issue

- Acceptance criteria are fulfilled and reproducible.
- Relevant tests demonstrate success and the required failure behavior.
- Restore/build/test actually process the intended projects.
- Provider/client claims have matching real validation.
- Coverage is recorded only when the report is non-empty and valid.
- Documentation reflects shipped behavior.

## Current state

The process is defined, but the Example's source and test projects still need to be introduced. The workflow itself is not yet evidence that the application behavior exists.
