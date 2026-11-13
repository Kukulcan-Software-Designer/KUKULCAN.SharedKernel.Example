# Issue #20 — PostgreSQL Persistence Validation

## Completion record

- **Issue:** [#20 — Validate PostgreSQL persistence with provider-backed integration tests](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/20)
- **Pull request:** [#43 — Validate PostgreSQL persistence and business searches](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/pull/43)
- **Merge target:** `Develop`
- **Provider:** PostgreSQL 16
- **Container image:** `postgres:16-alpine`
- **Test framework:** NUnit
- **Container integration:** Testcontainers.PostgreSql
- **Target framework:** .NET 10

This document records the provider-backed persistence validation delivered for Issue #20. The implementation was merged into `Develop` through PR #43 on 2026-10-30. It records the actual PostgreSQL checks and their evidence; it does not extend the scope of the original issue to SQL Server or MySQL.

## 1. Implementation and test environment

The Infrastructure integration-test project uses Testcontainers to start a disposable PostgreSQL 16 instance from `postgres:16-alpine`. The test fixtures create isolated databases and dispose of their containers when the tests finish.

A running Docker daemon is required. A separately managed PostgreSQL container is not required for these tests, and the tests do not depend on a manually created Docker network. The PostgreSQL version explicitly exercised by this suite is 16; this record does not claim that other PostgreSQL major versions were validated.

Test-only credentials and database names are supplied by the test fixtures. Real database credentials are not committed to source control.

## 2. Configuration contract

The Example application reads its database settings from these .NET configuration keys:

| Configuration key | Canonical environment variable |
|---|---|
| `Kukulcan:Database:Provider` | `KUKULCAN__DATABASE__PROVIDER` |
| `Kukulcan:Database:ConnectionString` | `KUKULCAN__DATABASE__CONNECTIONSTRING` |

For PostgreSQL, the provider value is `PostgresSql`. A local migration or application connection can be configured using a protected environment or secret store:

```bash
export KUKULCAN__DATABASE__PROVIDER='PostgresSql'
export KUKULCAN__DATABASE__CONNECTIONSTRING='Host=localhost;Database=Example;Username=...;Password=...;'
```

The example connection string contains placeholders only. Do not commit real credentials, put passwords in documentation, or expose connection strings in logs.

The Testcontainers integration tests construct their provider options from the disposable container's generated connection string. They do not require the application environment variables to be exported in the shell. Set those variables when running the application or the provider-specific EF Core migration commands.

## 3. Schema and migration behavior

PostgreSQL uses the logical business schema `Example`. The Example-owned business tables and the `DatabaseIntegrationRecords` persistence-demonstration table are mapped to that schema.

The provider-specific migration project is:

`Source/WebAPI/KUKULCAN.SharedKernel.Example.Migrations.PostgreSQL`

It contains the design-time factory and the `InitialExampleSchema` migration. `ExampleDbContext` selects the provider-specific migration assembly from the configured database provider; PostgreSQL migrations must not be applied to SQL Server or MySQL databases.

The real-container migration integration coverage verifies fresh initialization and repeat application against PostgreSQL. The test checks the expected Example schema objects, migration history and persistence behavior after migrations are reapplied. This is distinct from the earlier Issue #4 persistence demonstration that used `EnsureCreatedAsync`; `EnsureCreated` is not used as a replacement for versioned production migrations.

The business model includes Clients, Providers, Products, Warehouses, WarehouseStocks, Invoices and ProdInvoices, together with their line/detail entities. The PostgreSQL validation covers the business model and its relational constraints, not only the minimal `DatabaseIntegrationRecord` entity.

Authentication-owned tables are not recreated as part of the Example business schema migrations.

## 4. Provider-backed integration coverage

The PostgreSQL integration tests run against the real PostgreSQL engine in Testcontainers rather than substituting an in-memory EF Core provider.

The suite exercised the following areas:

| Area | PostgreSQL behavior validated |
|---|---|
| Schema initialization | Creation of schema `Example` and its Example-owned tables |
| Migrations | Initial migration, migration history and safe reapplication |
| Relational constraints | Foreign keys, required relationships, unique/commercial constraints and invalid-reference rejection |
| Persistence | Creation, retrieval and update of business entities and related data |
| Decimal values | Persistence using the configured decimal precision and scale |
| Tenant isolation | Tenant-scoped queries and rejection of cross-tenant relationships |
| Transactions | Commit and rollback behavior against PostgreSQL |
| Soft deletion | Soft-deleted master records are excluded from normal queries while historical commercial data is preserved |
| Business search | Provider-translated client, location, product, warehouse and invoice searches |

### 4.1 Business search integration test

PR #43 added `PostgreSqlBusinessSearchIntegrationTests`, with the test:

`PostgreSqlBusinessSearches_FilterClientsWarehousesAndInvoicesByRequestedCriteria`

The test starts PostgreSQL 16 using Testcontainers, applies the Example migrations, inserts a controlled set of clients, products, warehouses, stock relationships and invoices, and then executes the searches through EF Core against PostgreSQL.

It verifies:

- Client-name search using PostgreSQL `ILIKE`, including case-insensitive matching.
- Combined city, state and postal-code filtering.
- Warehouse search by stocked product code.
- Warehouse search by stocked product name.
- Invoice filtering by client and date range, with an inclusive start and exclusive end boundary.
- Exclusion of unrelated clients, products, warehouses and invoices from the respective results.

This test ensures the search expressions are translated and executed by the PostgreSQL provider, including relationship predicates over warehouse stock.

### 4.2 Tenant and historical-data guarantees

The broader PostgreSQL persistence suite validates tenant-scoped persistence and query behavior, including composite relational constraints that prevent commercial relationships from crossing tenant boundaries.

Soft deletion is tested as a preservation rule, not as physical removal: master records are hidden from normal active-record queries while historical sales and purchase documents remain available. Transaction tests verify that uncommitted partial changes are rolled back and previously committed data remains intact.

## 5. Test and workflow results

The validation command recorded in PR #43 was:

```bash
dotnet test KUKULCAN.SharedKernel.Example.slnx \
  --no-build \
  --no-restore \
  --configuration Release \
  --settings /tmp/example-coverage.runsettings \
  --collect:"XPlat Code Coverage" \
  --results-directory ./TestResults
```

The reported result was **197 passed, 0 failed, 0 skipped** across the four test projects:

| Test project | Passed |
|---|---:|
| Domain | 72 |
| Application | 25 |
| WebAPI | 41 |
| Infrastructure integration tests | 59 |
| **Total** | **197** |

The 59 Infrastructure integration tests include the provider-backed PostgreSQL persistence and business-search checks.

### 5.1 Pull request and Code Coverage

PR #43 recorded **Code Coverage workflow #207 as successful**. Its validation section reports the 197/197 test result above and describes the PostgreSQL/Testcontainers coverage for schema initialization, migration reapplication, constraints, persistence, transactions, soft deletion and business searches.

PR #43 was merged into `Develop` on 2026-10-30 at 16:36:15 UTC. The merge commit was:

`4afe03a90e96de00a5712d76bb70920b5561edca`

### 5.2 Post-merge validation and cleanup

The post-merge repository checks recorded for this issue were:

| Workflow | Run | Result |
|---|---:|---|
| CI (post-merge) | #350 | Success |
| Delete merged branches (except main/develop) | #35 | Success |

The feature branch was deleted after the successful merge. The protected `main` branch was not changed; `Develop` received the merge commit.

## 6. Local reproduction

Run the complete Infrastructure integration-test project from the repository root:

```bash
dotnet test \
  Tests/KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests/KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests.csproj \
  --configuration Release
```

To run only the PostgreSQL business-search test:

```bash
dotnet test \
  Tests/KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests/KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests.csproj \
  --configuration Release \
  --filter "FullyQualifiedName~PostgreSqlBusinessSearchIntegrationTests" \
  --logger "console;verbosity=detailed"
```

The Testcontainers fixture starts the PostgreSQL container automatically. No manually started PostgreSQL container is needed for this test path.

For manual migration operations, install EF Core tooling on the .NET 10 line if necessary, configure the provider and connection string securely, and use the PostgreSQL migrations project:

```bash
dotnet tool install --global dotnet-ef --version 10.*
dotnet ef migrations list \
  --project Source/WebAPI/KUKULCAN.SharedKernel.Example.Migrations.PostgreSQL/KUKULCAN.SharedKernel.Example.Migrations.PostgreSQL.csproj \
  --context ExampleDbContext

dotnet ef database update \
  --project Source/WebAPI/KUKULCAN.SharedKernel.Example.Migrations.PostgreSQL/KUKULCAN.SharedKernel.Example.Migrations.PostgreSQL.csproj \
  --context ExampleDbContext
```

Use a disposable development/test database when reproducing migration behavior. Production migrations should be reviewed and applied as an explicit deployment step, with automatic startup migration disabled. Keep migration and runtime identities separate and grant only the privileges required for each role.

## 7. Issue #20 acceptance criteria

| Acceptance criterion | Evidence / status |
|---|---|
| Provider-backed tests run against a real PostgreSQL instance and pass | **Verified** — PostgreSQL 16 via Testcontainers; the reported solution test run passed 197/197 tests. |
| Fresh initialization of schema `Example` and subsequent migration behavior are verified | **Verified** — provider-specific PostgreSQL migration project and real-container migration coverage. |
| Required search and deletion semantics are validated against PostgreSQL | **Verified** — business searches, tenant isolation, soft deletion and historical-document preservation are covered. |
| CI prerequisites and secrets are documented without committing credentials | **Verified** — Docker/Testcontainers prerequisite, configuration contract and test-only credentials are documented. |
| PostgreSQL is described as supported only after these checks pass | **Verified for PostgreSQL 16** — PR #43 and its successful coverage and post-merge checks provide the recorded evidence. |

## 8. Scope and limitations

- This record validates PostgreSQL 16 using the `postgres:16-alpine` Testcontainers image. It does not claim validation against every PostgreSQL release.
- The search integration test uses PostgreSQL's `ILIKE` operator for case-insensitive matching. This is provider-specific behavior and should not be assumed to translate identically on SQL Server or MySQL.
- The record covers the Example business schema and its persistence behavior. It does not validate SQL Server or MySQL; those providers have their own validation issues (#21 and #22).
- Testcontainers creates disposable databases and containers. The tests do not validate a separately administered production PostgreSQL server's network, backup, privilege or operational configuration.
- Credentials in the documented command examples are placeholders. Real credentials must be supplied outside source control.

## 9. Completion status

Issue #20 was completed through PR #43, merged into `Develop` on 2026-10-30. The PR reported 197 passed tests with no failures or skips, including 59 Infrastructure integration tests. Code Coverage #207 and the recorded post-merge CI #350 and branch-cleanup #35 workflows completed successfully.

The merge commit is `4afe03a90e96de00a5712d76bb70920b5561edca`. Issue #20 was closed, the feature branch was deleted, and `main` remained unchanged.

This document is a retrospective validation record. It does not reopen Issue #20, change the merged implementation, or claim that the other database providers have been validated.

---

*Evidence snapshot: Issue #20; PR #43 merged into `Develop`; PostgreSQL 16 (`postgres:16-alpine`) with Testcontainers; 197 tests passed, 0 failed, 0 skipped (Domain 72, Application 25, WebAPI 41, Infrastructure integration 59); Code Coverage #207 successful; post-merge CI #350 and branch cleanup #35 successful; merge commit `4afe03a90e96de00a5712d76bb70920b5561edca`.*
