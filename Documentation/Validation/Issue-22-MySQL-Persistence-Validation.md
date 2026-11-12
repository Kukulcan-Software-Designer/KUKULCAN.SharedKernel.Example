# Issue #22 — MySQL Persistence Validation

## Completion record

- **Issue:** [#22 — Validate MySQL persistence with provider-backed integration tests](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/22)
- **Target branch:** `Develop`
- **Feature branch:** `FEATURES/M3-MySQL-Persistence-Validation`
- **Provider:** MySQL 8.4
- **Test framework:** NUnit 5
- **Container integration:** Testcontainers.MySql 4.16.0
- **Target framework:** .NET 10

This record documents the provider-backed checks performed for Issue #22. It distinguishes the logical Example business model from MySQL's database/catalog semantics and records limitations where a provider-specific behavior was not asserted independently.

## 1. Implementation and test environment

The integration-test project references `Testcontainers.MySql` version `4.16.0` and starts disposable containers using the `mysql:8.4` image. Tests connect through the mapped host port and use the MySQL EF Core provider against the running server.

The test fixtures use isolated databases, including:

- `example_mysql_integration` for persistence, migration and relational behavior.
- `example_mysql_search` for business search and filtering behavior.

These are disposable test database names, not production database names.

The test credentials are generated for the test environment at runtime. No real database credentials are committed to source control. A running Docker daemon is required; the tests create and dispose their own MySQL containers, so a separately managed MySQL container or membership in the `kukulcan-local` Docker network is not required for these Testcontainers tests.

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

The integration tests verify decimal precision/scale behavior against MySQL. The 64-character identifier limit is configured in EF Core model metadata; this completion record does not claim a separate boundary test for an identifier of exactly 64 characters.

## 5. Provider-backed integration coverage

The selected MySQL integration run executed **11 tests: 11 passed, 0 failed, 0 skipped**, in approximately **109.8 seconds**.

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

### Migration verification

The migration test applies the MySQL migration and verifies that all ten Example tables exist in the selected database and that no migrations remain pending. It then persists a `DatabaseIntegrationRecord`, reapplies migrations, and verifies:

- The applied migration list is unchanged.
- No migrations are pending.
- The table set is unchanged.
- The previously persisted record remains available.

This verifies fresh initialization and repeat-application behavior against the real MySQL engine.

### Search and collation behavior

The business-search test runs provider-translated queries against MySQL for client names, location criteria, product codes, product names, warehouse relationships, invoice client identifiers and date ranges. It uses explicit `ToLower()` normalization together with `EF.Functions.Like` for case-normalized text matching.

This verifies the tested query behavior on MySQL. It does **not** independently assert the server/database default collation, nor compare behavior across multiple collations. No claim is made here that a particular MySQL collation is configured by the application.

### Connectivity and readiness

The TCP test establishes connections through the Testcontainers mapped host port and exercises direct MySQL TCP connections with pooling both disabled and enabled.

During container startup, early readiness attempts can report connection refused while the MySQL server is initializing. Later attempts succeed. The readiness probe can also display an authentication-denied message when `mysqladmin ping` checks the live server without a password; the successful exit status indicates that the server responded, not that passwordless root authentication was granted. The subsequent test connections use the container's generated credentials.

## 6. GitHub Actions

The following workflow runs were reported as completed successfully for the MySQL validation branch:

| Workflow | Run | Result |
|---|---:|---|
| CI | #449 | Success |
| Code Coverage | #300 | Success |
| Reference Client | #409 | Success |

These results complement the local 11/11 MySQL integration-test run. They establish that the corresponding CI, coverage and reference-client workflows completed successfully for the reviewed branch state.

## 7. Issue #22 acceptance criteria

| Acceptance criterion | Evidence / status |
|---|---|
| Provider-backed tests run against a real MySQL instance and pass | **Verified** — MySQL 8.4 Testcontainers; 11/11 selected MySQL tests passed locally; CI #449 succeeded |
| Fresh initialization of the Example business tables and subsequent migration behavior are verified | **Verified with MySQL semantics** — all ten Example tables are created in the selected MySQL database; repeat migration preserves data |
| Required search, relationship and deletion semantics are validated against MySQL | **Verified** — business search, commercial relationships, foreign keys, tenant isolation and historical soft-delete behavior are covered |
| CI prerequisites and secrets are documented without committing credentials | **Verified** — Docker/Testcontainers prerequisite, environment-variable contract and runtime-generated test credentials are documented |
| MySQL is described as supported only after these checks pass | **Validated** — MySQL 8.4 provider-backed checks and the three reported GitHub Actions workflows are successful |

**Scope notes:** MySQL's physical database/catalog name is selected by the connection string; the test fixtures use isolated names rather than a catalog literally named `Example`. Search tests verify case-normalized matching using `ToLower()` and `LIKE`, but do not assert a specific server default collation. The configured 64-character identifier limit is not accompanied by a dedicated boundary-length integration test. These boundaries are stated explicitly rather than inferred as additional test coverage.

## 8. Completion status

The implementation and provider-backed verification for Issue #22 have passed the reported local and GitHub Actions checks. This document records the evidence and provider-specific semantics; it does not itself close the GitHub issue or merge the feature branch.

---

*Evidence snapshot: feature branch `FEATURES/M3-MySQL-Persistence-Validation`; MySQL 8.4 Testcontainers integration run (11/11 passed, approximately 109.8 seconds); GitHub Actions CI #449, Code Coverage #300 and Reference Client #409.*
