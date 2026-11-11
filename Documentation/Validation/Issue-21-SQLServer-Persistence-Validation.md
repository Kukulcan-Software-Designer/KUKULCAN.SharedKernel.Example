# Issue #21 — SQL Server Persistence Validation

## Completion record

- **Issue:** [#21 — Validate Microsoft SQL Server persistence with provider-backed integration tests](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/21)
- **Implementation PR:** [#44 — Validate SQL Server business persistence with provider-backed integration tests](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/pull/44)
- **Target branch:** `Develop`
- **Merge commit:** `5917c5d4963eb83b0afecb5a45b8c640b9e81bdc`
- **PR head commit:** `8ebf851b25b7045e1eb43976f745178d25d46883`
- **Merged:** 2026-11-11 18:57:26 UTC

This record distinguishes checks performed on the PR head from the workflow run triggered by the merge commit. It records observed results only.

## 1. Implementation

PR #44 adds provider-backed SQL Server integration coverage for the Example business persistence layer. The integration tests use Testcontainers with `mcr.microsoft.com/mssql/server:2025-latest`; they exercise a real SQL Server engine in disposable containers rather than an in-memory database.

The test setup generates temporary database credentials at runtime. No usable database password or username is committed to source control.

## 2. CI, Reference Client and Code Coverage

The following workflows completed successfully for PR head commit `8ebf851b25b7045e1eb43976f745178d25d46883`:

| Workflow | Run | Event | Result |
|---|---:|---|---|
| CI | [43079394055](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/actions/runs/43079394055) | pull_request | Success |
| CI | [43078150854](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/actions/runs/43078150854) | push | Success |
| Reference Client | [43079394003](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/actions/runs/43079394003) | pull_request | Success |
| Reference Client | [43078150607](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/actions/runs/43078150607) | push | Success |
| Code Coverage | [43078150823](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/actions/runs/43078150823) | push | Success |

The Code Coverage artifact reported:

| Metric | Result |
|---|---:|
| Line coverage | 83.2% (2,276 / 2,735) |
| Branch coverage | 67.9% (193 / 284) |
| Method coverage | 77.7% (77 / 99) |

The PR validation record also reports a successful Release restore/build, **210 passing tests**, and a clean `git diff --check`.

## 3. SQL Server instance validation

The provider-backed integration suite starts SQL Server 2025 containers through Testcontainers and connects through the SQL Server EF Core provider. The tests execute database migrations and persistence operations against that running SQL Server instance.

The migration and business persistence tests therefore validate SQL Server behavior, not only provider configuration or design-time context construction.

## 4. Migrations and Example schema initialization

The test `SqlServerDatabaseMigrationIntegrationTests.SqlServerMigrations_InitializeExampleSchemaAndCanBeReappliedWithoutLosingData` verifies that:

- Migration `20261022200000_InitialExampleSchema` is registered and applied.
- The `Example` schema contains the ten expected tables: `Clients`, `Providers`, `Products`, `Warehouses`, `WarehouseStocks`, `Invoices`, `InvoiceLines`, `ProdInvoices`, `ProdInvoiceLines`, and `DatabaseIntegrationRecords`.
- No tables are created in the `Auth` schema by the Example migration.
- No migrations remain pending after initialization.
- Reapplying migrations does not add duplicate history entries or lose an existing persisted record.

## 5. Constraints, relationships and persistence operations

The SQL Server integration coverage exercises the business relationships among clients, providers, products, warehouses, warehouse stock, sales invoices and purchase invoices. It verifies inserts, reads, updates and deletes, including:

- Primary-key and unique-key enforcement, including tenant-scoped commercial identifiers.
- Foreign-key rejection for missing referenced entities and protection of referenced historical records.
- Product-code uniqueness, warehouse/product stock uniqueness, invoice-number uniqueness and line constraints.
- Tenant isolation in queries.
- Soft deletion of clients and providers, while retaining sales/purchase documents and their historical snapshots.
- Decimal precision and scale for stock quantities, invoice quantities and prices.
- Transaction rollback and durability of committed data after recreating the DbContext.

## 6. Search and filtering results

The SQL Server business-search integration tests exercise provider-translated queries for:

- Case-insensitive client-name matching.
- Combined city, state and postal-code filtering.
- Warehouse lookup through product code and product name.
- Invoice filtering by client and date range, with an inclusive start and exclusive end.
- Tenant isolation across client, warehouse and invoice query results.

These checks run against SQL Server and use SQL Server-compatible `LIKE` translation; the tests do not assume PostgreSQL's `ILIKE` behavior.

## 7. Actions after merge

The merge commit is `5917c5d4963eb83b0afecb5a45b8c640b9e81bdc`.

The post-merge run list for that exact commit returned one workflow execution:

| Workflow | Run | Event | Result |
|---|---:|---|---|
| CI | [43082644658](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/actions/runs/43082644658) | push | Success |

The Reference Client and Code Coverage successes listed in section 2 belong to the PR head commit, not to the merge commit. They are not represented as post-merge runs.

The remote branch `FEATURES/M3-SQLServer-Persistence-Validation` was checked with `git ls-remote --heads origin FEATURES/M3-SQLServer-Persistence-Validation`; it returned no matching reference, confirming that the feature branch had been deleted from the remote.

## 8. Issue status

**At the time this completion record was prepared, GitHub reported Issue #21 as OPEN.**

Although PR #44 contains the text `Closes #21`, it was merged into `Develop`, not the repository's default branch. GitHub therefore did not automatically close the issue on that merge. The issue must be closed explicitly after this documentation has been reviewed and merged. This record must be updated with the confirmed closure timestamp and final state; it must not be treated as evidence that the issue is already closed.

---

*Evidence snapshot: GitHub PR #44, its workflow run records, the merge commit, the SQL Server integration test sources, and the local command outputs recorded during the completion review on 2026-11-11.*
