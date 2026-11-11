# I18N Web API Integration

## Purpose and boundary

The Example Web API consumes the separately hosted `KUKULCAN.SharedKernel.i18n` service through an outbound HTTP client implemented in `KUKULCAN.SharedKernel.Example.Infrastructure`. The Example does not reference the I18N implementation project and does not access its database.

The Infrastructure adapter calls the published lookup contract:

```http
GET /api/v1/translations/{code}/{languageCode}
Authorization: Bearer <short-lived service token>
```

The successful response contract contains `code`, `languageCode`, `text`, `isFallback`, and `actualLanguageCode`.

The Example exposes an authenticated pass-through boundary for clients:

```http
GET /api/examples/i18n/translations/{code}/{languageCode}
```

Only the safe translation fields are returned to the Example client. Upstream error bodies and internal exception details are not forwarded.

## Configuration

Configure these settings through a secure environment-specific source, deployment secret store, or local user secrets. Do not commit signing keys or real credentials.

| .NET configuration key | Environment variable | Required / default | Description |
|---|---|---|---|
| `Kukulcan:I18N:BaseUrl` | `KUKULCAN__I18N__BASEURL` | Required | Absolute HTTP(S) base URL of the I18N service, including port when necessary |
| `Kukulcan:I18N:TimeoutSeconds` | `KUKULCAN__I18N__TIMEOUTSECONDS` | Default `5`; allowed `1–120` | Timeout for an outbound translation request |
| `Kukulcan:I18N:Jwt:SigningKey` | `KUKULCAN__I18N__JWT__SIGNINGKEY` | Required | Shared signing secret, at least 32 UTF-8 bytes |
| `Kukulcan:I18N:Jwt:Issuer` | `KUKULCAN__I18N__JWT__ISSUER` | Default `ATLAS` | JWT issuer accepted by the I18N service |
| `Kukulcan:I18N:Jwt:Audience` | `KUKULCAN__I18N__JWT__AUDIENCE` | Default `ATLAS.i18n` | JWT audience accepted by the I18N service |

The signing key must match the I18N service's `Jwt:SecretKey` configuration (`Jwt__SecretKey` as an environment variable). The I18N service verifies the token's signature, issuer, audience and lifetime; a missing/invalid key will prevent a successful request.

The Example creates a short-lived HMAC-SHA256 service token for outbound lookups. The key is read from runtime configuration and is never placed in API responses or logs.

## Local development

The I18N Docker documentation and deployment scripts live in the [KUKULCAN.SharedKernel.i18n repository](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.i18n/tree/main/Documentation).

When the I18N container publishes port 8080 to the host, a local Example API process can use:

```bash
export KUKULCAN__I18N__BASEURL="http://127.0.0.1:8080/"
export KUKULCAN__I18N__TIMEOUTSECONDS="5"
export KUKULCAN__I18N__JWT__ISSUER="ATLAS"
export KUKULCAN__I18N__JWT__AUDIENCE="ATLAS.i18n"
read -rsp "I18N JWT signing key: " KUKULCAN__I18N__JWT__SIGNINGKEY
export KUKULCAN__I18N__JWT__SIGNINGKEY
echo
```

Run the Web API using the repository's normal .NET 10 development workflow. Do not reuse a production signing secret in a local environment.

When both applications run as containers on the same Docker network, set `KUKULCAN__I18N__BASEURL` to the I18N container's network DNS name and internal port, for example `http://kukulcan-i18n:8080/`. A container must not use `localhost:8080` to reach a different container; `localhost` refers to itself.

Confirm that the I18N service is healthy before investigating a translation lookup. A liveness response alone does not prove that the authenticated lookup endpoint or its database-backed translation is available.

## Failure behavior

The adapter has a finite request timeout and respects request cancellation. The Example endpoint maps outbound errors to stable public responses:

| Situation | HTTP status | Public response |
|---|---:|---|
| Translation returned | `200` | Translation DTO |
| Outbound request timeout | `504` | Generic gateway-timeout Problem Details |
| I18N host unreachable / transport failure | `503` | Generic service-unavailable Problem Details |
| I18N returns a non-success HTTP status | `502` | Generic bad-gateway Problem Details |
| I18N returns invalid JSON or an invalid payload | `502` | Generic bad-gateway Problem Details |

Cancellation initiated by the incoming client is not intentionally translated into a timeout response. Raw upstream response bodies, exception messages, stack traces, signing keys, and internal host details are not returned to callers.

## Verification

The Web API tests replace the named `KukulcanI18N` HTTP client's primary handler with a deterministic fake. They assert that the actual I18N client request reaches the fake handler, uses the real `/api/v1/translations/{code}/{languageCode}` path, and handles success, timeout, service unavailability, upstream HTTP failure and invalid JSON. Separate endpoint tests verify HTTP mapping and diagnostic-data protection.

Run the dedicated Web API test project from the repository root:

```bash
dotnet test Tests/KUKULCAN.SharedKernel.Example.WebApi.Tests/KUKULCAN.SharedKernel.Example.WebApi.Tests.csproj --configuration Release
```

The fake-handler tests are deterministic contract/failure-path tests; they do not replace a real-service integration smoke test when validating a deployment or I18N contract upgrade.
