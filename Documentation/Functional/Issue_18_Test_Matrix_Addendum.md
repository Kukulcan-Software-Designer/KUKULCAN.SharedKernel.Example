# Issue #18 — HTTP Test Matrix Addendum

**Base document:** `Diseno_Funcional_WebAPI_Example.docx`, consolidated functional design v1.2, including the approved decisions D-01–D-14 and the approved Issue #18 scope.  
**Branch:** `FEATURES/M4-Issue-18-WebAPI-Business`  
**Traceability workbook:** `Issue_18_T16-T17_I18-001-I18-037_Traceability.xlsx`  
**Purpose:** Define the HTTP acceptance cases owned by Issue #18 and make their expected observable behavior explicit before implementation.

This addendum complements the consolidated functional design and the Issue #18 traceability workbook. It does not reopen or replace the approved functional decisions. T16 and T17 retain their existing identifiers and are included because they are the first implemented acceptance slice for Issue #18. I18-001–I18-037 are the remaining approved cases.

All applicable cases must be exercised through the Web API using `WebApplicationFactory` and a real provider-backed database. Provider parity is required for PostgreSQL, Microsoft SQL Server, and MySQL: the externally observable contract and persisted business result must be equivalent.

## Acceptance cases

| ID | Group | Case / stimulus | Expected result |
|---|---|---|---|
| T16 | Client creation | POST a valid Client payload (`name`, optional `city`, `state`, `postalCode`) with an authenticated tenant context | HTTP 201 Created; response identifies the created Client and returns the submitted values; `Location` is present and `tenantId` is not exposed. The persisted Client has the authenticated `TenantId`, preserves the submitted values, and starts active (`IsDeleted=false`). Verified GREEN on PostgreSQL, Microsoft SQL Server, and MySQL |
| T17 | Provider creation | POST a valid Provider payload (`name`, optional `city`, `state`, `postalCode`) with an authenticated tenant context | HTTP 201 Created; response identifies the created Provider and returns the submitted values; `Location` is present and `tenantId` is not exposed. The persisted Provider has the authenticated `TenantId`, preserves the submitted values, and starts active (`IsDeleted=false`). Verified GREEN on PostgreSQL, Microsoft SQL Server, and MySQL |
| I18-001 | Client creation validation | POST a Client with a missing or blank required field | HTTP 400; no Client is persisted |
| I18-002 | Provider creation validation | POST a Provider with a missing or blank required field | HTTP 400; no Provider is persisted |
| I18-003 | Client / Provider validation | POST Client or Provider data exceeding an approved field-length limit | HTTP 400; no invalid entity is persisted |
| I18-004 | Client / Provider creation | Create valid Client and Provider records under Tenant A | HTTP 201 for each operation; each record is persisted with Tenant A; no record is created for another tenant |
| I18-005 | Client update | PUT a valid update for a Client owned by the authenticated tenant | HTTP 200; editable fields are updated; Id and TenantId remain unchanged |
| I18-006 | Provider update | PUT a valid update for a Provider owned by the authenticated tenant | HTTP 200; editable fields are updated; Id and TenantId remain unchanged |
| I18-007 | Product update | PUT a valid update for a Product owned by the authenticated tenant | HTTP 200; only fields allowed by the approved update contract change; Id and TenantId remain unchanged |
| I18-008 | Warehouse update | PUT a valid update for a Warehouse owned by the authenticated tenant | HTTP 200; only fields allowed by the approved update contract change; Id and TenantId remain unchanged |
| I18-009 | Update not found | PUT an update for a resource identifier that does not exist | HTTP 404; no resource is created and no existing resource changes |
| I18-010 | Tenant isolation on update | PUT a resource that exists but belongs to another tenant | HTTP 404 Not Found (tenant-hiding response); no data or ownership changes |
| I18-011 | Update validation | PUT a payload that violates an approved required-field, length, or domain constraint | HTTP 400; the previously persisted state remains unchanged |

### I18-011 — approved update-field constraints

The following maximum lengths are the acceptance limits for the editable fields. Required fields must not be null, empty, or whitespace-only after trimming; optional fields may be null or empty. Any invalid PUT returns HTTP 400 and leaves the complete persisted record unchanged.

| Entity | Field | Requirement | Maximum length |
|---|---|---|---:|
| Client / Provider | Name | Required | 200 |
| Client / Provider | City | Optional | 120 |
| Client / Provider | State | Optional | 120 |
| Client / Provider | PostalCode | Optional | 32 |
| Product | Code | Required | 64 |
| Product | Name | Required | 200 |
| Product | Description | Optional | 1000 |
| Warehouse | Name | Required | 160 |
| Warehouse | City | Optional | 120 |
| Warehouse | State | Optional | 120 |
| Warehouse | PostalCode | Optional | 32 |

`Warehouse.Name` is limited to 160 characters to match the shared persistence mapping (`ExampleDbContextModelConfiguration`). The boundary acceptance test must accept exactly 160 characters; 161 characters must be rejected. `Product.Code` uniqueness remains a separate HTTP 409 conflict rule, not an I18-011 HTTP 400 length/required-field failure.
| I18-012 | Stock quantity | Submit a valid controlled stock-quantity change for a WarehouseStock owned by the authenticated tenant | HTTP 200; the requested quantity is persisted for the intended stock record only |
| I18-013 | Stock quantity validation | Submit an invalid stock quantity, including a value outside the approved domain constraints | HTTP 400; the original quantity remains unchanged |
| I18-014 | Stock tenant isolation | Attempt to change stock belonging to another tenant | HTTP 404 Not Found (tenant-hiding response); no stock quantity or ownership changes |
| I18-015 | Invoice creation | POST a valid Invoice header with its complete set of valid lines | HTTP 201; header and all lines are persisted atomically and associated with the correct tenant |
| I18-016 | ProdInvoice creation | POST a valid ProdInvoice header with its complete set of valid lines | HTTP 201; header and all lines are persisted atomically and associated with the correct tenant |
| I18-017 | Invoice line validation | POST an Invoice containing an invalid line | HTTP 400; neither the Invoice header nor any of its lines is persisted |
| I18-018 | ProdInvoice line validation | POST a ProdInvoice containing an invalid line | HTTP 400; neither the ProdInvoice header nor any of its lines is persisted |
| I18-019 | Document references | POST an Invoice or ProdInvoice with a nonexistent or otherwise invalid referenced entity | The request is rejected with the contractually applicable 400/404 response; no document or lines are persisted |
| I18-020 | Transaction rollback | Force a line-persistence failure during Invoice or ProdInvoice creation | A controlled failure is returned; the transaction rolls back the header and every line |
| I18-021 | Document snapshots | Create a document and then change the referenced master data | The document's stored snapshots continue to reflect the values captured when the document was created |
| I18-022 | Update concurrency | Submit two updates based on the same resource version; let the first succeed and then submit the stale update | First update returns HTTP 200; stale update returns HTTP 409 and does not overwrite the first update |
| I18-023 | Stock concurrency | Submit two stock changes based on the same stock version; let the first succeed and then submit the stale change | First change returns HTTP 200; stale change returns HTTP 409 and does not overwrite the current stock state |
| I18-024 | Tenant isolation on references | Attempt to create or update a document using a reference or tenant context belonging to another tenant | A foreign-tenant resource reference returns HTTP 404 Not Found to preserve tenant hiding; a mismatched tenant context is rejected by the approved authentication/tenant contract. No cross-tenant relationship or partial data is persisted |
| I18-025 | Deleted master references | Attempt to associate a document with a Client or Provider that is deleted/inactive under the approved rule | The association is rejected according to the approved active-reference rule; no invalid association is persisted |
| I18-026 | Decimal quantity | Persist and retrieve a valid quantity with supported decimal precision | The exact supported decimal value is preserved; no unintended rounding or truncation occurs |
| I18-027 | Decimal price | Persist and retrieve a valid price with supported decimal precision | The exact supported decimal value is preserved; no unintended rounding or truncation occurs |
| I18-028 | Decimal precision validation | Submit a quantity or price with more fractional digits than the approved precision permits | The request is rejected; the value is not silently rounded or altered and no invalid data is persisted |
| I18-029 | Numeric domain boundaries | Submit quantities and prices at, below, and above the approved bounds, including prohibited signs | Exercise values immediately below, exactly at, and immediately above each approved boundary, plus prohibited signs. Approved boundary values are accepted; prohibited values are rejected consistently without persistence. Do not introduce numeric limits not stated in the approved functional design |
| I18-030 | Line numbering | Create an Invoice or ProdInvoice with valid line numbers | Every line is associated with the correct document and its line number is persisted unchanged |
| I18-031 | Line numbering validation | Create a document containing duplicate line numbers where uniqueness is required by the approved rule | The whole operation is rejected; no header or partial line set is persisted |
| I18-032 | Repeated product lines | Create a document containing the same Product on more than one line | Behavior follows the approved repeated-product rule; the test must not impose an additional uniqueness restriction |
| I18-033 | Product reference | Create a document line referencing a Product belonging to the same tenant | The line is persisted with the correct Product relationship and document association |
| I18-034 | Foreign Product reference | Create a document line referencing a Product belonging to another tenant | The request is rejected; no document or partial line set is persisted |
| I18-035 | Issue date validation | POST a document with an issue date that violates the approved creation-date rules | The request is rejected according to the approved date rule; no document or lines are persisted |
| I18-036 | Header and line snapshots | Create a document with valid header and line data | The approved header and line snapshot fields are persisted with the values present at creation time |
| I18-037 | Complete transaction rollback | Cause a failure after document creation has begun but before all lines complete | The transaction leaves no document header and no lines persisted |

## I18-012 — warehouse-stock quantity update HTTP matrix

I18-012 is verified as a provider-independent HTTP contract through the Example Web API and a real provider-backed database. The update uses an absolute destination quantity, not a delta. A successful update requires the current strong ETag in `If-Match`, returns HTTP 200 with the next strong ETag, and changes only the stock quantity. The application-managed `Version` is persisted as an integer, starts at 1, and increases exactly once for each successful update. Every response ETag must be strong and represent that persisted version. Rejected requests must leave the persisted quantity, version, tenant ownership, stock identity, and warehouse/product relationships unchanged. HTTP 400 validation responses must use `application/problem+json` and the ProblemDetails shape defined in section 3.2 of the functional design.

| Test method | HTTP stimulus | Expected HTTP result | Required persisted-state assertion |
|---|---|---|---|
| `PutWarehouseStock_WithValidAbsoluteQuantityAndCurrentVersion_UpdatesOnlyQuantity` | PUT a positive quantity with `If-Match: "1"` | 200; strong ETag `"2"`; response identifies the stock, warehouse, product, and absolute quantity | Persisted Quantity is replaced; Version advances from 1 to 2; Id, TenantId, WarehouseId, and ProductId remain unchanged |
| `PutWarehouseStock_WithZeroQuantityAndCurrentVersion_AcceptsZero` | PUT quantity zero with the current ETag | 200; strong ETag `"2"` | Quantity is persisted as zero; Version advances from 1 to 2; all stock identity, tenant, and relationship fields remain unchanged |
| `PutWarehouseStock_WithNegativeQuantity_ReturnsBadRequestAndPreservesQuantity` | PUT a negative quantity | 400 with ProblemDetails | Quantity and Version remain unchanged; Id, TenantId, WarehouseId, and ProductId remain unchanged |
| `PutWarehouseStock_WithMoreThanThreeFractionalDigits_ReturnsBadRequestAndPreservesQuantity` | PUT a quantity with more than three fractional digits | 400 with ProblemDetails; no silent rounding | Quantity and Version remain unchanged; Id, TenantId, WarehouseId, and ProductId remain unchanged |
| `PutWarehouseStock_WithoutIfMatch_ReturnsPreconditionRequiredAndPreservesQuantity` | PUT without `If-Match` | 428 | Quantity and Version remain unchanged; Id, TenantId, WarehouseId, and ProductId remain unchanged |
| `PutWarehouseStock_WithMalformedIfMatch_ReturnsBadRequestAndPreservesQuantity` | PUT with a malformed `If-Match` value | 400 with ProblemDetails | Quantity and Version remain unchanged; Id, TenantId, WarehouseId, and ProductId remain unchanged |
| `PutWarehouseStock_WithStaleVersion_ReturnsConflictAndPreservesCurrentQuantity` | PUT with a syntactically valid but stale version | 409 | Quantity and Version remain unchanged; Id, TenantId, WarehouseId, and ProductId remain unchanged |
| `PutWarehouseStock_WithUnknownId_ReturnsNotFoundWithoutCreatingStock` | PUT an unknown stock identifier | 404 | No stock is created; existing stock Quantity, Version, Id, TenantId, WarehouseId, and ProductId remain unchanged |
| `PutWarehouseStock_BelongingToAnotherTenant_ReturnsNotFoundAndPreservesForeignStock` | Authenticated Tenant A attempts to PUT Tenant B's stock | 404 tenant-hiding response | Foreign stock Quantity, Version, Id, TenantId, WarehouseId, and ProductId remain unchanged |
| `PutWarehouseStock_TwiceWithSameVersion_RejectsSecondUpdateAsConflict` | Submit two sequential PUT requests using the same original ETag | First request 200 with strong ETag `"2"`; second request 409 | First quantity remains persisted and Version is 2; the stale second request does not overwrite quantity or version |
| `GetWarehouseStockById_ReturnsCurrentRepresentationAndStrongVersionEtag` | GET the stock detail by identifier | 200 with current strong ETag `"1"` | Response contains the stock identifier, warehouse/product identifiers, and current quantity; persisted Version is 1 and stock identity, tenant, and relationships are verified |

All rows above are required on PostgreSQL, Microsoft SQL Server, and MySQL. The test fixture must use an isolated tenant and provider-backed database. For every PUT, query the persisted row after the HTTP response and assert Quantity, Version, Id, TenantId, WarehouseId, and ProductId. Successful PUTs must increment Version exactly once; every rejected PUT must preserve it. GET and successful PUT responses must return a strong ETag whose quoted integer matches the persisted Version. HTTP 400 responses must be ProblemDetails with media type `application/problem+json` and non-empty `type` and `title` plus the matching `status`. These tests do not authorize any production implementation by themselves: keep the production code unchanged until the test suite demonstrates RED against the current behavior.

## Common execution and evidence rules

1. **Test-first sequence:** add or extend the HTTP integration test, demonstrate a genuine RED against the current implementation, and only then make the minimum production change required by that failing test. Do not modify production code before the RED evidence exists.
2. **Provider parity:** run every applicable case against PostgreSQL, Microsoft SQL Server, and MySQL. A case is marked `CUBIERTO` in the workbook only after it passes on all three providers and its evidence is recorded.
3. **HTTP contract:** assert the exact expected status code and, for error responses, the required problem-details content type and response shape where defined by the API contract.
4. **Persistence:** for successful writes, verify the resulting database state, tenant ownership, relationships, and immutable identifiers as applicable. For rejected writes, verify that no forbidden or partial state was persisted.
5. **Atomicity:** for document creation and line failures, verify rollback of the entire aggregate, not merely the HTTP response.
6. **Concurrency:** verify that a stale write cannot overwrite a newer committed state and that the conflict response is HTTP 409 on every provider.
7. **No nominal coverage:** a status-code assertion alone is insufficient when the requirement concerns persisted values, tenant isolation, snapshots, relationships, rollback, or concurrency.
8. **Traceability:** record the actual test file and method, the contract effectively asserted (including limitations), the per-provider result, commit, and execution evidence in the Issue #18 Excel workbook. Do not mark a case covered based only on a successful build or aggregate coverage percentage.

## Scope boundaries

Issue #18 covers the Client and Provider creation operations, the approved entity updates, controlled stock quantity changes, and transactional creation of Invoice and ProdInvoice documents with their lines, including validation, tenant isolation, snapshots, rollback, decimal rules, and concurrency.

The following remain outside this Issue #18 implementation scope:

- Client and Provider soft deletion and deleted-record lifecycle behavior assigned to Issue #19.
- Generic PUT or DELETE operations for Invoice and ProdInvoice.
- Independent CRUD endpoints for Invoice or ProdInvoice lines.
- Inventory movement functionality.
- Accounting, posting, payment, or other document workflow not explicitly included in the approved Issue #18 design.

The addendum is an acceptance specification, not authorization to implement every case in one change. Continue in small TDD slices, preserving the approved branch workflow and obtaining explicit authorization before any pull request merge.
