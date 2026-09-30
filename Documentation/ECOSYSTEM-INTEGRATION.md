# KUKULCAN SharedKernel Ecosystem — Local Integration Guide

This repository is the executable reference client for the KUKULCAN SharedKernel ecosystem.

It demonstrates how one ASP.NET Core application can consume:

- `KUKULCAN.SharedKernel` through NuGet.
- `KUKULCAN.SharedKernel.Auth` through NuGet.
- `KUKULCAN.SharedKernel.Database` through NuGet.
- `KUKULCAN.SharedKernel.JsonEngine` through NuGet.
- `KUKULCAN.SharedKernel.i18n` as a local HTTP service running in Docker.

The important architectural distinction is that **I18n is not a NuGet library consumed in-process**. It is a standalone ASP.NET Core service. The Example application communicates with it over HTTP.

## 1. Repository roles

| Repository | Runtime role in the Example |
|---|---|
| `KUKULCAN.SharedKernel` | Common domain/result abstractions consumed as a NuGet package |
| `KUKULCAN.SharedKernel.Auth` | Authentication services and password/local-auth behavior consumed as a NuGet package |
| `KUKULCAN.SharedKernel.Database` | Database abstractions, DbContext base and Unit of Work consumed as a NuGet package |
| `KUKULCAN.SharedKernel.JsonEngine` | JSON processing capability consumed as a NuGet package |
| `KUKULCAN.SharedKernel.i18n` | Independent internationalization HTTP service, normally executed locally in Docker |
| `KUKULCAN.SharedKernel.Example` | Reference Web application that composes the previous components |

The Example application must not introduce `ProjectReference` links to the SharedKernel libraries. The reusable libraries are consumed through their published NuGet packages. I18n is consumed through HTTP.

## 2. Local topology

A simple local setup is:

```text
                         HTTP
┌─────────────────────────────────────────────┐
│ KUKULCAN.SharedKernel.Example.Web           │
│ http://localhost:5000                       │
│                                             │
│  ├── SharedKernel       ── NuGet            │
│  ├── Auth               ── NuGet            │
│  ├── Database            ── NuGet            │
│  ├── JsonEngine          ── NuGet            │
│  └── I18nServiceClient   ── HTTP ───────┐   │
└──────────────────────────────────────────│──┘
                                           │
                                           ▼
                              ┌─────────────────────────┐
                              │ KUKULCAN.SharedKernel   │
                              │ .i18n                   │
                              │ Docker                  │
                              │ http://localhost:8080   │
                              └────────────┬────────────┘
                                           │
                                           ▼
                              ┌─────────────────────────┐
                              │ PostgreSQL               │
                              │ local Docker container   │
                              └─────────────────────────┘
```

Redis is optional for the local I18n service; the service can operate with its in-memory cache when Redis is not configured.

## 3. Start the I18n service locally

Clone the I18n repository beside the Example repository:

```bash
mkdir -p ~/Proyectos/KUKULCAN
cd ~/Proyectos/KUKULCAN

git clone https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.i18n.git
git clone https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example.git
```

Enter the I18n repository:

```bash
cd KUKULCAN.SharedKernel.i18n
```

Create a dedicated Docker network:

```bash
docker network create kukulcan-local
```

Start PostgreSQL:

```bash
docker run --detach \
  --name kukulcan-i18n-postgres \
  --network kukulcan-local \
  --env POSTGRES_DB=kukulcan_i18n \
  --env POSTGRES_USER=kulculcan \
  --env POSTGRES_PASSWORD=kulculcan_pass \
  postgres:16
```

> If the container already exists, do not create it again. Use `docker start kukulcan-i18n-postgres`.

Build the I18n service image:

```bash
docker build --tag kukulcan-sharedkernel-i18n:local .
```

Run the service:

```bash
docker run --detach \
  --name kukulcan-sharedkernel-i18n \
  --network kukulcan-local \
  --publish 8080:8080 \
  --env ASPNETCORE_HTTP_PORTS=8080 \
  --env Kukulcan__Database__ConnectionString='Host=kukulcan-i18n-postgres;Port=5432;Database=kukulcan_i18n;Username=kulculcan;Password=kulculcan_pass' \
  --env Kukulcan__Database__Migration__AutoMigrateOnStartup=true \
  --env Kukulcan__Database__SeedDataOnStartup=true \
  --env ConnectionStrings__Redis='' \
  --env Jwt__SecretKey='local-i18n-secret-key-with-at-least-32-characters' \
  --env Jwt__Issuer='ITZAMNA' \
  --env Jwt__Audience='ITZAMNA.i18n' \
  kukulcan-sharedkernel-i18n:local
```

Check the container:

```bash
docker ps
docker logs kukulcan-sharedkernel-i18n
```

Check liveness:

```bash
curl --fail http://127.0.0.1:8080/health/live
```

The I18n API is now reachable by the host at:

```text
http://localhost:8080
```

Its API documentation is exposed by the I18n application in Development mode.

## 4. Configure the Example application

The Example application uses the following configuration:

```json
"I18n": {
  "BaseUrl": "http://localhost:8080/",
  "Issuer": "ITZAMNA",
  "Audience": "ITZAMNA.i18n"
}
```

The signing key must not be committed to source control. Supply it as an environment variable:

```bash
export I18n__JwtSecretKey='local-i18n-secret-key-with-at-least-32-characters'
```

Run the Example:

```bash
cd ../KUKULCAN.SharedKernel.Example
dotnet run --project Source/KUKULCAN.SharedKernel.Example.Web
```

The Example's I18n client generates a short-lived JWT signed with the same local development key and sends it as a Bearer token to the I18n service.

This is deliberately a service-to-service authentication example. It does not make the I18n service an in-process dependency.

## 5. I18n consumption flow

The Example exposes:

```text
GET /api/i18n/culture?culture=<language-code>
```

The request flow is:

```text
Client
  │
  │ GET /api/i18n/culture?culture=...
  ▼
Example.Web
  │
  │ creates short-lived JWT
  │ Authorization: Bearer <token>
  │
  │ GET /api/v1/languages/<culture>
  ▼
KUKULCAN.SharedKernel.i18n
  │
  │ JWT validation
  │ language lookup
  ▼
Example.Web
  │
  │ maps the service response
  ▼
HTTP 200 / 404
```

The Example therefore demonstrates the real architectural boundary: SharedKernel libraries are local NuGet dependencies, while I18n is a network dependency.

## 6. Other SharedKernel integrations

### SharedKernel

The Example consumes the published `KUKULCAN.SharedKernel` package and demonstrates its `Result` abstraction through:

```text
GET /api/result
```

### Auth

The Example consumes `KUKULCAN.SharedKernel.Auth` and demonstrates:

```text
POST /api/auth/password/verify
POST /api/auth/local/authenticate
```

The local authentication example also demonstrates tenant membership behavior.

### Database

The Example consumes `KUKULCAN.SharedKernel.Database` and demonstrates persistence through:

```text
POST  /api/database/entities
GET   /api/database/entities/{id}
PATCH /api/database/entities/{id}
```

The persistence path uses `KukulcanDbContextBase` and `IUnitOfWork` rather than introducing a production database provider into the Example itself.

### JsonEngine

The JsonEngine integration follows the same rule as the other reusable libraries: the Example consumes its published NuGet package. Its functional HTTP behavior will be added in the dedicated JsonEngine TDD cycle.

JsonEngine is therefore not coupled to the I18n Docker container. They are independent components.

## 7. Local lifecycle

Start the dependencies:

```bash
docker start kukulcan-i18n-postgres
docker start kukulcan-sharedkernel-i18n
```

Run the Example:

```bash
dotnet run --project Source/KUKULCAN.SharedKernel.Example.Web
```

Stop the Example with `Ctrl+C`.

Stop the I18n service when finished:

```bash
docker stop kukulcan-sharedkernel-i18n
docker stop kukulcan-i18n-postgres
```

Remove the containers when the local environment is no longer required:

```bash
docker rm kukulcan-sharedkernel-i18n kukulcan-i18n-postgres
docker network rm kukulcan-local
```

## 8. Troubleshooting

### I18n returns 401

Check that the Example and I18n service use exactly the same:

- JWT secret.
- Issuer.
- Audience.

The local secret must contain at least 32 characters.

### I18n does not start

Inspect the logs:

```bash
docker logs kukulcan-sharedkernel-i18n
```

The most common local dependency is PostgreSQL. Check:

```bash
docker ps
docker logs kukulcan-i18n-postgres
```

### The database is not ready

The I18n service applies migrations only when:

```text
Kukulcan__Database__Migration__AutoMigrateOnStartup=true
```

is supplied.

### Port 8080 is already in use

Change the host-side port, for example:

```bash
--publish 18080:8080
```

and configure the Example with:

```text
I18n:BaseUrl=http://localhost:18080/
```

The container continues to listen on port 8080.

## 9. Design rule

The Example repository is intentionally a **reference client**, not a monolithic composition of all KUKULCAN repositories.

The intended dependency model is:

```text
Example
  ├── NuGet → SharedKernel
  ├── NuGet → Auth
  ├── NuGet → Database
  ├── NuGet → JsonEngine
  └── HTTP   → I18n Docker service
```

This distinction should be preserved as additional functional behaviors are added.
