# Issue #8 — HTTP Test Matrix Addendum

**Base document:** `Diseno_Funcional_WebAPI_Example.docx`, consolidated functional design v1.0.  
**Branch:** `FEATURES/M4-Issue-8-WebAPI-Endpoints`  
**Purpose:** Complete the individual-filter and exact-combination coverage missing from the original T01–T69 matrix.

This addendum is normative together with sections 17–19 of the base document. It does not change decisions D-01–D-14. All cases below run through the Web API with `WebApplicationFactory` and a real provider-backed database using Testcontainers. The same expected behavior is required for PostgreSQL, Microsoft SQL Server, and MySQL.

## Additional acceptance cases

| ID | Group | Case / stimulus | Expected result |
|---|---|---|---|
| T70 | Client filters | `city` only; exact match after trimming, insensitive to case and accents | Only records with the matching city; no partial match; same result on all providers |
| T71 | Client filters | `state` only; exact match after trimming, insensitive to case and accents | Only records with the matching state; no partial match; same result on all providers |
| T72 | Client filters | `postalCode` only, including a value with leading zeroes | Exact textual match; leading zeroes are preserved |
| T73 | Client filters | `name + city` | Both predicates apply with AND; name uses contains and city exact matching |
| T74 | Client filters | `name + state + postalCode` | All predicates apply with AND; text and geographic semantics follow D-02/D-03 |
| T75 | Client filters | `name + city + state + postalCode` | All four predicates apply with AND; only the complete matching set is returned |
| T76 | Provider filters | `city` only; exact match after trimming, insensitive to case and accents | Only records with the matching city; no partial match; same result on all providers |
| T77 | Provider filters | `state` only; exact match after trimming, insensitive to case and accents | Only records with the matching state; no partial match; same result on all providers |
| T78 | Provider filters | `postalCode` only, including a value with leading zeroes | Exact textual match; leading zeroes are preserved |
| T79 | Provider filters | `name + city` | Both predicates apply with AND; name uses contains and city exact matching |
| T80 | Provider filters | `state + postalCode` | Both predicates apply with AND; exact geographic matching |
| T81 | Provider filters | `name + city + state + postalCode` | All predicates apply with AND; only the complete matching set is returned |
| T82 | Warehouse filters | `city` only; exact match after trimming, insensitive to case and accents | Only records with the matching city; no partial match |
| T83 | Warehouse filters | `state` only; exact match after trimming, insensitive to case and accents | Only records with the matching state; no partial match |
| T84 | Warehouse filters | `postalCode` only, including a value with leading zeroes | Exact textual match; leading zeroes are preserved |
| T85 | Warehouse filters | `name + city` | Both predicates apply with AND; name uses contains and city exact matching |
| T86 | Warehouse filters | `city + state + postalCode` | All three geographic predicates apply with AND |
| T87 | Product filters | `code` only | startsWith semantics, insensitive to case and accents according to D-02 |
| T88 | Product filters | `name` only | contains semantics, insensitive to case and accents according to D-02 |
| T89 | WarehouseStock filters | `productCode` only | startsWith semantics; value is projected from the related Product |
| T90 | WarehouseStock filters | `productName` only | contains semantics; value is projected from the related Product |

**Updated matrix total: 90 acceptance cases (T01–T90).** T01–T69 remain unchanged; T70–T90 extend the missing individual and combination scenarios. Every added case is provider-parity coverage, not a separate provider-specific expected behavior.

## Issue ownership clarification

The base endpoint catalogue in section 4 is authoritative for implementation ownership. The HTTP matrix is a cross-issue acceptance catalogue and must not be interpreted as moving work assigned to #18 or #19 into #8.

- **Issue #8:** GET/list/detail endpoints and search filters for Clients, Providers, Products, Warehouses, WarehouseStocks, Invoices, and ProdInvoices; POST Products, Warehouses, and WarehouseStocks as defined by D-09; pagination, sorting, DTO projections, tenant isolation, and the explicit absence of prohibited operations.
- **Issue #18:** POST Clients and Providers and the create/update validation and business invariants assigned to #18; document creation and lines; updates; stock quantity changes; concurrency.
- **Issue #19:** Client/Provider soft deletion and any future authorized access to deleted records.

Consequently, T16–T17 and the create-specific portion of T08 are cross-issue acceptance cases for the routes owned by #18; they are not prerequisites for declaring the #8 implementation complete. T27, T31, and T35 remain relevant to #8 because they verify prohibited routes and the active-record query behavior required by D-06/D-08. No PUT/DELETE or soft-delete implementation is to be added to #8.

## Execution rule

For each Issue #8 group, implement the relevant HTTP tests first, demonstrate a genuine RED against the current branch, then make the minimum production change, obtain GREEN, and collect coverage. Run the same test behavior against PostgreSQL, Microsoft SQL Server, and MySQL. Do not implement #18/#19 behavior in this branch merely to make the cross-issue matrix green.
