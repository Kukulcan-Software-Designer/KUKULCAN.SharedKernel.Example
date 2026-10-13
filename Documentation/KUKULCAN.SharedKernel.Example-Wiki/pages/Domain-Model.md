# Domain Model
**Language:** [Spanish](Modelo-de-Dominio)


## Scope

The Example domain is planned under database schema `Sample`. The following model is specified by the project Issues; it is not yet implemented in tracked EF Core entities or migrations.

## Required entities

| Entity | Purpose | Key relationships and rules |
|---|---|---|
| `Client` | Customer buying goods | Related to sales invoices (`Invoice`); uses logical deletion |
| `Provider` | Supplier selling goods to the business | Related to purchase documents (`ProdInvoice`); uses logical deletion |
| `Product` | Product catalogue | Authoritative source for product code and name |
| `Warehouse` | Warehouse/location catalogue | Related to `WarehouseStock` |
| `Invoice` | Sales transaction | References a `Client`; commercial history must not be physically deleted by normal API operations |
| `ProdInvoice` | Purchase transaction | References a `Provider`; commercial history must not be physically deleted by normal API operations |
| `WarehouseStock` | Quantity of a product in a warehouse | References both `Warehouse` and `Product`; does not duplicate product code/name |
| Invoice line(s) | Product, quantity, and price per sales/purchase document | Exact names and attributes to be finalized during model design |

## Relationship overview

```text
Client 1 ─── * Invoice 1 ─── * InvoiceLine * ─── 1 Product
Provider 1 ─ * ProdInvoice 1 ─ * PurchaseLine * ─ 1 Product
Warehouse 1 ─ * WarehouseStock * ─ 1 Product
```

`InvoiceLine` and `PurchaseLine` are explanatory names in this diagram, not accepted entity names. The implementation must settle the real names and document whether sales and purchase lines share an abstraction or use separate types.

## Integrity rules

- Map all required entities into schema `Sample`.
- Define primary keys, foreign keys, required fields, sensible column lengths/types, and appropriate indexes.
- `WarehouseStock` obtains product code and name through its `Product` relationship; do not copy those values into each stock row.
- Enforce uniqueness of `(WarehouseId, ProductId)` unless there is an explicit requirement for multiple lots, bins, or stock locations. If that requirement appears, model it explicitly rather than silently removing the unique constraint.
- Quantities and prices must have appropriate numeric precision and validation.
- Historical sale/purchase data must remain readable after a related client/provider is soft-deleted.
- Apply application-level validation and database-level constraints where each is appropriate.

## Required search behavior

### Clients

Support a partial-name match and filters for city, state/region, and postal code. Filters should be combinable. Define whether matching is case-insensitive based on provider-supported semantics and test that behavior.

### Warehouse stock

Support filtering by product code and by partial product-name match through `WarehouseStock.Product`. Return the product's canonical code/name from the product relationship, not redundant copies stored in stock.

### Sales

Support queries filtered by client and date range. Specify inclusive/exclusive boundaries, timezone interpretation, sorting, and pagination during API design.

## Lifecycle policies

- `Client` and `Provider`: logical deletion; normal queries exclude inactive/deleted entries by default.
- `Invoice` and `ProdInvoice`: preserve commercial history; no ordinary physical-delete endpoint.
- Inventory updates: validate product/warehouse references and reject invalid quantities or duplicate stock rows according to the approved model.

See [Database and Migrations](Database-and-Migrations), [Authentication and Security](Authentication-and-Security), and Issues [#7](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/7), [#8](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/8), [#18](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/18), and [#19](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/19).
