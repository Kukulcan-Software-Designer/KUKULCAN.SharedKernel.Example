# JsonEngine Integration

## Purpose

Issue #5 demonstrates consuming the published `KUKULCAN.SharedKernel.JsonEngine` 1.0.0 package through an Example application use case. The Example uses the package's public `IJsonSqlEngine` contract and `JsonSqlEngine` implementation to query a caller-supplied JSON document in memory.

This endpoint does not query the Sample database, execute SQL against a database, persist the submitted document, or replace ASP.NET Core's JSON serialization.

## Endpoint

`POST /api/examples/json-engine/query`

The endpoint is protected by the Web API's default/fallback authorization policies. Callers must present a valid bearer token from the Example authentication flow and satisfy the account-readiness policy.

### Request

The request contains a JSON object or array in `document` and a SQL-like JsonEngine statement in `query`.

```json
{
  "document": {
    "items": [
      { "id": "A", "name": "Motor", "price": { "value": 120 } },
      { "id": "B", "name": "Bolt", "price": { "value": 5 } },
      { "id": "C", "name": "Shaft", "price": { "value": 40 } },
      { "id": "D", "name": "Filter", "price": { "value": 25 } }
    ]
  },
  "query": "SELECT id, name, price.value FROM $.items[*] WHERE price.value > 20 ORDER BY price.value DESC LIMIT 2"
}
```

Example request (assuming `ACCESS_TOKEN` contains a valid Example access token):

```bash
curl --request POST \
  --header "Authorization: Bearer $ACCESS_TOKEN" \
  --header "Content-Type: application/json" \
  --data @json-engine-query.json \
  http://localhost:5000/api/examples/json-engine/query
```

### Response

A successful request returns HTTP 200. Projected field names are preserved, including nested paths such as `price.value`.

```json
{
  "resultCount": 2,
  "rows": [
    {
      "id": "A",
      "name": "Motor",
      "price.value": 120
    },
    {
      "id": "C",
      "name": "Shaft",
      "price.value": 40
    }
  ]
}
```

## Query behavior and limits

The package supports a compact SQL-like dialect over JSON arrays:

- `SELECT` projects named fields or `*`.
- `FROM` selects a JSONPath-like array path, such as `$.items[*]`.
- `WHERE` accepts the package's supported simple comparison condition.
- `ORDER BY` sorts by one field in ascending or descending order.
- `LIMIT` restricts the result count.

The Example requires exactly one `LIMIT` clause at the end of every query and restricts it to 1–100 rows. This bounds the result size even if a caller submits a large source document. The Example also rejects documents larger than 64 KiB and queries longer than 2,048 characters.

HTTP 400 with a validation-problem response is returned for an unsupported document root, a blank or malformed query shape, a missing/multiple/out-of-range `LIMIT`, or inputs that exceed the configured size limits. Malformed HTTP JSON is rejected by ASP.NET Core model binding. Authentication and account-readiness failures are handled by the existing Web API authorization policies.

This example intentionally does not add joins, aggregation, database access, or features that are not part of the published JsonEngine query contract. The Example validates the request boundary and translates package rows into its own response DTO rather than exposing JsonEngine's `JsonNode` types in the public HTTP contract.

## Implementation boundary

- `Application/JsonEngine/JsonEngineQueryService.cs` validates bounded input, invokes `IJsonSqlEngine`, and maps result values to detached `JsonElement` values.
- `WebApi/JsonEngine/JsonEngineQueryEndpoints.cs` maps the endpoint and its validation errors.
- `Program.cs` registers the package implementation and application use case.
- `JsonEngineQueryEndpointTests.cs` verifies projection, filtering, ordering, limiting, invalid queries, scalar documents, and malformed request JSON.
