# KUKULCAN Integrations
**Language:** [Spanish](Integraciones-KUKULCAN)


## Integration principle

Consume each dependency through its verified public contract and documented package/service version. The Example should show how the ecosystem is used together; it must not duplicate the reusable implementation of a library.

## `KUKULCAN.SharedKernel`

**Intended purpose:** demonstrate selected shared types/result or response contracts in a meaningful API use case.

**Validation required:** reference the published package, verify the public API for the pinned version, and test both success and relevant failure paths. Do not copy library source or reach into internal types.

Issue: [#2](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/2).

## `KUKULCAN.SharedKernel.Auth`

**Intended purpose:** authentication, supported user access behavior, and an initial administrator bootstrap.

**Validation required:** inspect the actual login, user, password verification, role/tenant, and credential-validation contracts; use only capabilities the package exposes. Do not invent token refresh or logout behavior if the dependency does not support it.

Issue: [#3](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/3).

## `KUKULCAN.SharedKernel.Database`

**Intended purpose:** persistence using the package's supported database/context/unit-of-work contracts, with provider selection supplied by configuration.

**Configuration:** `Kukulcan__Database__Provider` and `Kukulcan__Database__ConnectionString`.

**Validation required:** persist and retrieve a real Sample entity; verify migrations, transactions, constraints, soft deletion, and provider behavior. PostgreSQL, SQL Server, and MySQL must each have independent provider-backed evidence before the Example claims support.

Issues: [#4](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/4), [#17](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/17), [#20](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/20), [#21](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/21), [#22](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/22).

## `KUKULCAN.SharedKernel.JsonEngine`

**Intended purpose:** demonstrate a real application-level JSON-engine capability, separate from persistence and domain rules.

**Validation required:** inspect the published API and select a concrete, well-defined use case. Test valid input and relevant invalid/error conditions and explain the result in the documentation.

Issue: [#5](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/5).

## `KUKULCAN.SharedKernel.I18N`

**Intended purpose:** consume the separate I18N Web API over HTTP. Do not reimplement its service in the Example.

**Validation required:** use the actual HTTP contract, externally configured base URL and credentials if needed, explicit timeout behavior, predictable unavailable/invalid-response handling, and no leakage of secrets or internal diagnostics.

Issue: [#6](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/6).

## Dependency/version policy

- Pin package versions using the project files and repository's release conventions.
- Update dependencies only with a reason and tests relevant to the changed contract.
- Keep package release state separate from Example feature state.
- Report defects to the corresponding upstream repository when the behavior belongs to that library rather than the Example.
