# Domain Model

**Language:** [Spanish](Modelo-de-Dominio)

## Status and scope

The nine Sample domain entity types and their EF Core mappings are implemented in the Example Domain and Infrastructure projects. Versioned migrations and real-provider persistence checks remain in Issues #17 and #20–#22; this model does not claim all three providers have been validated.

## Entities

| Entity | Purpose | Integrity and history |
|---|---|---|
| `Client` | Customer master data | Tenant-scoped; supports shared logical deletion using `IsDeleted` and `DeletedOn` |
| `Provider` | Supplier master data | Tenant-scoped; supports shared logical deletion using `IsDeleted` and `DeletedOn` |
| `Product` | Canonical product catalogue | `Code` is unique within a tenant; authoritative source for current product code/name |
| `Warehouse` | Warehouse/location catalogue | Tenant-scoped and related to stock rows |
| `WarehouseStock` | Quantity per product and warehouse | Unique `(WarehouseId, ProductId)`; never duplicates the current product code/name |
| `Invoice` | Sales document | References a `Client`, captures a client-name snapshot and cannot cascade-delete line history |
| `InvoiceLine` | Product, quantity and unit price on a sale | Captures product code/name snapshots and uses fixed decimal precision |
| `ProdInvoice` | Purchase document | References a `Provider`, captures a provider-name snapshot and cannot cascade-delete line history |
| `ProdInvoiceLine` | Product, quantity and unit price on a purchase | Captures product code/name snapshots and uses fixed decimal precision |

All entities use a GUID `Id` and a `TenantId`. Composite foreign keys pair `TenantId` with the referenced identifier, preventing rows from one tenant from linking to another tenant's records at the database level. The shared Database context applies its tenant query filter. Client/provider logical-delete filtering uses the shared `ISoftDelete` contract.

## Relationship overview

```text
Client 1 ─── * Invoice 1 ─── * InvoiceLine * ─── 1 Product
Provider 1 ─ * ProdInvoice 1 ─ * ProdInvoiceLine * ─ 1 Product
Warehouse 1 ─ * WarehouseStock * ─ 1 Product
```

Sales and purchase lines are separate types because they belong to different document types. Their product code/name fields are historical snapshots; stock rows deliberately contain only product and warehouse references plus quantity.

## Database mapping and constraints

- Every business entity maps to the `Sample` schema with an explicit table name.
- Primary keys are GUID `Id` values. Composite alternate keys (`TenantId`, `Id`) support tenant-safe foreign keys.
- `WarehouseStock` has a unique index over `(WarehouseId, ProductId)`.
- Product codes and sales/purchase document numbers are unique per tenant.
- Deleting a client, provider, product, warehouse, invoice or purchase document is restricted when referenced rows exist; sale/purchase lines are not cascade-deleted.
- Unit prices use `decimal(18,2)`; quantities and stock amounts use `decimal(18,3)`.
- `Client` has the fields used by the planned client search: `Name`, `City`, `State`, and `PostalCode`.

## Boundaries with later Issues

- Issue #17 owns versioned migrations and repeatable schema initialization. This Issue only establishes the CLR domain model and EF Core mappings.
- Issues #20–#22 validate provider-specific persistence against real PostgreSQL, SQL Server and MySQL instances; installed packages alone do not prove support.
- Issue #8 owns Web API operations and required search filters.
- Issue #18 owns request-level validation and detailed business rules.
- Issue #19 owns the API deletion workflow and historical-data retention policy. The domain model already provides logical-delete fields and restrictive relationships that support that policy.

See [Database and Migrations](Database-and-Migrations), [Authentication and Security](Authentication-and-Security), and Issues [#7](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/7), [#17](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/17), [#18](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/18), and [#19](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/19).
