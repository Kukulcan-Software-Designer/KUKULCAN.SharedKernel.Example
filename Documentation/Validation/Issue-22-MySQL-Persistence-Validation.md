# Issue #22 — MySQL Persistence Validation

## Completion record

- **Issue:** [#22 — Validate MySQL persistence with provider-backed integration tests](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/22)
- **Target branch:** `Develop`
- **Feature branch:** `FEATURES/M3-MySQL-Persistence-Validation`
- **Provider:** MySQL 8.4
- **Test framework:** NUnit 5
- **Container integration:** Testcontainers.MySql 4.16.0
- **Target framework:** .NET 10

This record documents the provider-backed checks performed for Issue #22. It distinguishes the logical Example business model from MySQL database/catalog semantics and separates completed evidence from the latest checks whose GitHub Actions executions have not yet finished.

## 1. Implementation and test environment

The integration-test project references `Testcontainers.MySql` version `4.16.0` and starts disposable containers using the `mysql:8.4` image. Tests connect through the mapped host port and use the MySQL EF Core provider against the running server.

The test fixtures use isolated databases, including:

- `example_mysql_integration` for persistence, migration and relational behavior.
- `example_mysql_search` for business search and filtering behavior.

These are disposable test database names, not production database names.

Test credentials are generated for the test environment at runtime. No real database credentials are committed to source control. A running Docker daemon is required; the tests create and dispose their own MySQL containers, so a separately managed MySQL container or membership in the `kukulcan-local` Docker network is not required for these Testcontainers tests.

## 2. Configuration contract

The Example application uses the .NET configuration keys:

| Configuration key | Canonical environment variable |
|---|---|
| `Kukulcan:Database:Provider` | `KUKULCAN__DATABASE__PROVIDER` |
| `Kukulcan:Database:ConnectionString` | `KUKULCAN__DATABASE__CONNECTIONSTRING` |

For a local MySQL application or migration connection, set the provider to `MySql` and supply a MySQL-compatible connection string through the protected environment or secret store:

```bash
export KUKULCAN__DATABASE__PROVIDER='MySql'
export KUKULCAN__DATABASE__CONNECTIONSTRING='Server=localhost;Port=3306;Database=Example;User Id=...;Password=...;'
```

The values above are a configuration example only. Do not commit credentials, place real passwords in documentation, or expose connection strings in logs.

The Testcontainers integration tests construct provider options directly using the disposable container's generated connection string. They validate the MySQL provider and database operations; they do not depend on the host shell's application environment variables.

## 3. MySQL schema/catalog semantics

The Example domain's logical business model includes these ten tables:

- `Clients`
- `Providers`
- `Products`
- `Warehouses`
- `WarehouseStocks`
- `Invoices`
- `InvoiceLines`
- `ProdInvoices`
- `ProdInvoiceLines`
- `DatabaseIntegrationRecords`

For PostgreSQL and SQL Server, the model maps Example-owned tables to the relational schema `Example`. MySQL treats schema and database/catalog as equivalent concepts and does not support a separately nested schema namespace inside a database. Accordingly, the MySQL model omits schema qualifiers and creates the Example tables in the database selected by the connection string.

The MySQL tests use isolated database names such as `example_mysql_integration` and `example_mysql_search`. They verify that the complete set of Example-owned tables is created in the selected test database. They do not assert that the physical MySQL database/catalog itself is literally named `Example`; deployment chooses that database name through the connection string.

Authentication-owned tables are not created by the Example migrations.

## 4. Provider-specific model configuration

The MySQL model configuration includes provider-specific relational metadata and store types:

| CLR type / metadata | MySQL mapping |
|---|---|
| `Guid` | `char(36)` |
| Bounded `string` | `varchar(n)` |
| Unbounded `string` | `longtext` |
| `bool` | `tinyint(1)` |
| `DateTimeOffset` | `datetime(6)` |
| Maximum identifier length | 64 |

Configured decimal precision and scale are retained by the model, including quantity and monetary columns. The provider-specific model configuration omits schema qualifiers for MySQL and does not apply PostgreSQL or SQL Server identity-column conventions.

The maximum identifier length is configured in EF Core model metadata as 64. The dedicated boundary integration test described in section 5.3 now exercises the MySQL engine at both 64 and 65 characters.

## 5. Provider-backed integration coverage

### 5.1 Previously completed MySQL integration run

The previously recorded MySQL integration run executed **11 tests: 11 passed, 0 failed, 0 skipped**, in approximately **109.8 seconds**.

| Test | Behavior validated |
|---|---|
| `MySqlConnectionFailure_IsNotReportedAsSuccessfulConnectionOrExposeCredentials` | Connection failures are not reported as successful and credentials are not exposed |
| `MySqlTcpConnection_WorksWithAndWithoutConfiguredPooling` | TCP connectivity through the mapped port, with pooling enabled and disabled |
| `MySqlMigrations_InitializeExampleTablesAndCanBeReappliedWithoutLosingData` | Initial migration, all ten Example tables, no pending migrations, idempotent reapplication and data preservation |
| `MySqlBusinessPersistence_PersistsCommercialRelationshipsAndPreservesHistoricalSnapshots` | Business entities, commercial relationships and historical snapshots |
| `MySqlTenantQueries_IsolateDataAndSoftDeletePreservesMasterRecords` | Tenant isolation and soft deletion while preserving master records |
| `MySqlTransactions_RollbackPartialChangesAndKeepPreviouslyCommittedData` | Transaction rollback and durability of previously committed data |
| `MySqlDecimalColumns_PreserveConfiguredPrecisionAndScale` | Configured decimal precision and scale |
| `MySqlConstraints_RejectDuplicateCommercialKeysAndMissingReferences` | Duplicate commercial keys and missing references are rejected |
| `MySqlBusinessSoftDelete_PreservesHistoricalSalesAndPurchaseDocuments` | Soft deletion preserves historical sales and purchase documents |
| `MySqlCompositeForeignKeys_RejectCrossTenantCommercialReferences` | Composite foreign keys reject cross-tenant commercial references |
| `MySqlBusinessSearches_FilterByCriteriaAndIsolateResultsByTenant` | Business search criteria, date-range boundaries, product/warehouse searches and tenant isolation |

### 5.2 Migration verification

The migration test applies the MySQL migration and verifies that all ten Example tables exist in the selected database and that no migrations remain pending. It then persists a `DatabaseIntegrationRecord`, reapplies migrations, and verifies:

- The applied migration list is unchanged.
- No migrations are pending.
- The table set is unchanged.
- The previously persisted record remains available.

This verifies fresh initialization and repeat-application behavior against the real MySQL engine.

### 5.3 Collation inheritance and case-normalized search

The additional integration test `MySqlColumnCollation_InheritsDatabaseDefaultAndCaseNormalizedSearchWorks` checks the collation metadata exposed by MySQL:

1. It reads the selected database's `INFORMATION_SCHEMA.SCHEMATA.DEFAULT_COLLATION_NAME`.
2. It reads the `Clients.Name` column collation from `INFORMATION_SCHEMA.COLUMNS.COLLATION_NAME`.
3. It asserts that the column inherits the selected database's default collation.
4. It exercises the case-normalized search path using `ToLower()` and `EF.Functions.Like`.

The test intentionally compares the column collation with the database default rather than imposing one fixed collation value. It does not establish identical ordering or comparison behavior across arbitrary MySQL collations.

**Execution status:** the test is present in the feature branch. Its final provider-backed result is pending completion of the current CI and Code Coverage workflow runs; it is not yet counted among the 11 previously confirmed passing tests.

### 5.4 Identifier length boundary

The additional integration test `MySqlIdentifiers_Accept64CharactersAndReject65Characters` checks both the EF Core model metadata and the MySQL server boundary:

- It asserts that the model's `Relational:MaxIdentifierLength` annotation is 64.
- It creates a table whose identifier is exactly 64 characters and expects the operation to succeed.
- It attempts to create a table whose identifier is 65 characters and expects MySQL error 1059 (identifier name too long).
- It removes the successfully created boundary-test table.

This test validates the configured limit against the actual MySQL engine, rather than relying solely on EF Core metadata.

**Execution status:** the test is present in the feature branch. Its final provider-backed result is pending completion of the current CI and Code Coverage workflow runs; it is not yet counted among the 11 previously confirmed passing tests.

### 5.5 Connectivity and readiness

The TCP test establishes connections through the Testcontainers mapped host port and exercises direct MySQL TCP connections with pooling both disabled and enabled.

During container startup, early readiness attempts can report connection refused while the MySQL server is initializing. The readiness probe can also display an authentication-denied message when `mysqladmin ping` checks the live server without a password; the successful exit status indicates that the server responded, not that passwordless root authentication was granted. Subsequent test connections use the container's generated credentials.

## 6. GitHub Actions evidence

The earlier baseline workflow runs were reported successful:

| Workflow | Run | Result |
|---|---:|---|
| CI | #449 | Success |
| Code Coverage | #300 | Success |
| Reference Client | #409 | Success |

The additional collation and identifier tests have triggered newer workflow executions. At the time this record was updated, the latest runs were:

| Workflow | Run | Commit | Result at record update |
|---|---:|---|---|
| CI | [#459](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/actions/runs/43322164660) | `8e10255` | In progress; build succeeded, test step running |
| Code Coverage | [#310](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/actions/runs/43322164817) | `8e10255` | In progress; build succeeded, test/coverage step running |
| Reference Client | [#419](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/actions/runs/43322164649) | `8e10255` | Success |

The CI and Code Coverage runs for the immediately preceding test-code commit `92c35fc` (#458 and #309) were also still in progress when checked. Consequently, this record does not claim that the two additional tests have passed in CI or that the latest coverage workflow is green. The run results must be checked again before treating the expanded validation as complete.

## 7. Issue #22 acceptance criteria

| Acceptance criterion | Evidence / status |
|---|---|
| Provider-backed tests run against a real MySQL instance | **Baseline verified** — MySQL 8.4 Testcontainers; the previously selected MySQL suite passed 11/11 locally. The two additional tests await final workflow results. |
| Fresh initialization of the Example business tables and subsequent migration behavior are verified | **Verified** — all ten Example tables are created in the selected MySQL database; repeat migration preserves data. |
| Required search, relationship and deletion semantics are validated against MySQL | **Verified for the recorded baseline** — business search, commercial relationships, foreign keys, tenant isolation and historical soft-delete behavior are covered. |
| Database-default collation inheritance and the 64/65-character identifier boundary are checked | **Test implementation present; execution pending** — dedicated provider-backed tests have been added; latest CI and Coverage runs have not completed. |
| CI prerequisites and secrets are documented without committing credentials | **Verified** — Docker/Testcontainers prerequisite, environment-variable contract and runtime-generated test credentials are documented. |
| MySQL support is declared only after the relevant checks pass | **Pending final confirmation** — do not treat the expanded Issue #22 validation as fully complete until CI and Code Coverage finish successfully. |

**Scope notes:** MySQL's physical database/catalog name is selected by the connection string; the test fixtures use isolated names rather than a catalog literally named `Example`. Search tests verify case-normalized matching using `ToLower()` and `LIKE`. The additional collation test checks inheritance from the selected database default but does not guarantee identical ordering/comparison across arbitrary collations.

## 8. Completion status

The original MySQL persistence baseline has recorded local and GitHub Actions success evidence. Two additional integration tests now extend that evidence to database-default collation inheritance and the 64/65-character identifier boundary.

Issue #22's expanded validation remains **pending final CI and Code Coverage results** for the current branch state. This document records the evidence and outstanding verification accurately; it does not itself close the GitHub issue, create a pull request, or merge the feature branch.

---

*Evidence snapshot: feature branch `FEATURES/M3-MySQL-Persistence-Validation`; MySQL 8.4 Testcontainers baseline run (11/11 passed, approximately 109.8 seconds); additional collation and identifier boundary tests added; CI #459 and Code Coverage #310 in progress; Reference Client #419 successful.*
