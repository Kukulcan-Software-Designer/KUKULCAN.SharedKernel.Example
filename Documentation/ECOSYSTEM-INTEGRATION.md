# KUKULCAN SharedKernel Ecosystem — Local Integration Guide

This repository is the executable reference application for the reusable KUKULCAN platform components.

The organization is building applications such as **ATLAS**, composed of independent Web API modules (\`ATLAS.CRM\`, \`ATLAS.Inventory\`, \`ATLAS.Billing\`, etc.). Each module consumes only the KUKULCAN components required by its own responsibilities.

## 1. KUKULCAN ecosystem

| Component | Type | Consumption |
|---|---|---|
| \`KUKULCAN.SharedKernel\` | Class library | NuGet package |
| \`KUKULCAN.SharedKernel.Auth\` | Class library | NuGet package |
| \`KUKULCAN.SharedKernel.Database\` | Class library | NuGet package |
| \`KUKULCAN.SharedKernel.JsonEngine\` | Class library | NuGet package |
| \`KUKULCAN.SharedKernel.i18n\` | Web API service | HTTP |
| \`KUKULCAN.SharedKernel.Example\` | Reference Web API | Consumes the components above |

The four class libraries are generic KUKULCAN organization components and are consumed as **NuGet packages**. They are not coupled to ATLAS.

\`KUKULCAN.SharedKernel.i18n\` is also generic, but is deployed as an independent Web API service with its own runtime and database boundary. Consumers access it through HTTP.

A future module such as \`ATLAS.CRM\` follows this model:

\`\`\`text
ATLAS.CRM
   ├── NuGet → KUKULCAN.SharedKernel
   ├── NuGet → KUKULCAN.SharedKernel.Auth
   ├── NuGet → KUKULCAN.SharedKernel.Database
   ├── NuGet → KUKULCAN.SharedKernel.JsonEngine
   │
   └── HTTP  → KUKULCAN.SharedKernel.i18n
                         │
                         ▼
                    PostgreSQL / Atlas
\`\`\`

## 2. Local runtime topology

Before starting the Example Web API, the local infrastructure required by the i18n integration must already be running.

\`\`\`text
┌──────────────────────────────────────────────┐
│ KUKULCAN.SharedKernel.Example.Web            │
│ ASP.NET Core Web API                         │
│                                              │
│  SharedKernel       ← NuGet                  │
│  Auth               ← NuGet                  │
│  Database           ← NuGet                  │
│  JsonEngine         ← NuGet                  │
│                                              │
│              HTTP                            │
└─────────────────┬────────────────────────────┘
                  │
                  ▼
        ┌──────────────────────┐
        │ kukulcan-i18n        │
        │ Docker container     │
        │ host port 8080       │
        └──────────┬───────────┘
                   │
                   ▼
        ┌──────────────────────┐
        │ mypostgres           │
        │ PostgreSQL 16        │
        │ database: Atlas      │
        └──────────────────────┘
\`\`\`

The containers must share the Docker network \`kukulcan-local\`. The Example normally runs on the host and reaches i18n through \`http://localhost:8080/\`.

## 3. Prerequisites

Install:

- .NET SDK 10.0.
- Docker Engine.
- Git.

The expected local infrastructure is:

| Resource | Value |
|---|---|
| PostgreSQL image | \`postgres:16\` |
| PostgreSQL container | \`mypostgres\` |
| Database | \`Atlas\` |
| Database user | \`postgres\` |
| i18n container | \`kukulcan-i18n\` |
| i18n container port | \`8080\` |
| i18n host port | \`8080\` |
| Docker network | \`kukulcan-local\` |

The PostgreSQL password and JWT secret are local secrets. **Never commit the real values to this repository.**

## 4. Create the PostgreSQL Docker container

Create the network once:

\`\`\`bash
docker network create kukulcan-local
\`\`\`

If it already exists, continue.

Create PostgreSQL with the required database:

\`\`\`bash
docker run --detach \\
  --name mypostgres \\
  --network kukulcan-local \\
  --env POSTGRES_DB=Atlas \\
  --env POSTGRES_USER=postgres \\
  --env POSTGRES_PASSWORD='<LOCAL_POSTGRES_PASSWORD>' \\
  --publish 5432:5432 \\
  postgres:16
\`\`\`

Replace \`<LOCAL_POSTGRES_PASSWORD>\` with the local PostgreSQL password.

Verify:

\`\`\`bash
docker ps
docker exec mypostgres pg_isready --username postgres --dbname Atlas
\`\`\`

If \`mypostgres\` already exists, do not create another container:

\`\`\`bash
docker ps -a --filter name=mypostgres
docker start mypostgres
\`\`\`

Check the PostgreSQL version:

\`\`\`bash
docker exec mypostgres psql \\
  --username postgres \\
  --dbname Atlas \\
  --command "SHOW server_version;"
\`\`\`

## 5. Build KUKULCAN.SharedKernel.i18n

Clone the service repository:

\`\`\`bash
git clone https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.i18n.git
cd KUKULCAN.SharedKernel.i18n
\`\`\`

Build its root Dockerfile:

\`\`\`bash
docker build --tag kukulcan-i18n:local .
\`\`\`

The current Dockerfile uses the .NET 10 SDK for the build stage, the ASP.NET 10 runtime for execution, and exposes container port \`8080\`.

## 6. Create the kukulcan-i18n Docker container

The service must use the same network as PostgreSQL:

\`\`\`bash
docker run --detach \\
  --name kukulcan-i18n \\
  --network kukulcan-local \\
  --publish 8080:8080 \\
  --env ASPNETCORE_HTTP_PORTS=8080 \\
  --env KUKULCAN__Database__Provider=PostgresSql \\
  --env KUKULCAN__Database__ConnectionString='Host=mypostgres;Port=5432;Database=Atlas;Username=postgres;Password=<LOCAL_POSTGRES_PASSWORD>' \\
  --env KUKULCAN__Database__Migration__AutoMigrateOnStartup=true \\
  --env KUKULCAN__Database__Migration__SeedDataOnStartup=true \\
  --env ConnectionStrings__Redis='' \\
  --env Jwt__SecretKey='<LOCAL_I18N_JWT_SECRET_MINIMUM_32_CHARACTERS>' \\
  --env Jwt__Issuer='ATLAS' \\
  --env Jwt__Audience='ATLAS.i18n' \\
  kukulcan-i18n:local
\`\`\`

Inside the Docker network the database host is **\`mypostgres\`**, not \`localhost\`.

Verify:

\`\`\`bash
docker ps
docker logs kukulcan-i18n
curl --fail http://127.0.0.1:8080/health/live
\`\`\`

The service is then available from the host at:

\`\`\`text
http://localhost:8080/
\`\`\`

## 7. Atlas database and i18n schema

The i18n service uses the PostgreSQL database:

\`\`\`text
Atlas
\`\`\`

The current i18n tables belong to schema \`i18n\`:

- \`i18n.CurrencyFormat\`
- \`i18n.Languages\`
- \`i18n.LocaleConfigurations\`
- \`i18n.Translations\`

The Example does **not** access these tables directly. The i18n service owns this persistence boundary and exposes its API to consumers.

Check the schema:

\`\`\`bash
docker exec mypostgres psql \\
  --username postgres \\
  --dbname Atlas \\
  --command "\\\\dt i18n.*"
\`\`\`

With \`KUKULCAN__Database__Migration__AutoMigrateOnStartup=true\`, the i18n service applies its configured migrations at startup. With \`...SeedDataOnStartup=true\`, configured seed data is initialized.

## 8. Configure and run the Example

The Example points to the local i18n service:

\`\`\`json
"I18n": {
  "BaseUrl": "http://localhost:8080/",
  "Issuer": "ATLAS",
  "Audience": "ATLAS.i18n"
}
\`\`\`

The JWT secret must be the same secret configured for \`kukulcan-i18n\`:

\`\`\`bash
export I18n__JwtSecretKey='<LOCAL_I18N_JWT_SECRET_MINIMUM_32_CHARACTERS>'
\`\`\`

Run the application:

\`\`\`bash
dotnet run --project Source/KUKULCAN.SharedKernel.Example.Web
\`\`\`

Required startup order:

\`\`\`text
1. mypostgres
       ↓
2. kukulcan-i18n
       ↓
3. KUKULCAN.SharedKernel.Example.Web
\`\`\`

## 9. NuGet package consumption

The Example consumes these reusable class libraries as NuGet packages:

\`\`\`text
KUKULCAN.SharedKernel
KUKULCAN.SharedKernel.Auth
KUKULCAN.SharedKernel.Database
KUKULCAN.SharedKernel.JsonEngine
\`\`\`

A future ATLAS module uses the same model:

\`\`\`xml
<PackageReference Include="KUKULCAN.SharedKernel" Version="..." />
<PackageReference Include="KUKULCAN.SharedKernel.Auth" Version="..." />
<PackageReference Include="KUKULCAN.SharedKernel.Database" Version="..." />
<PackageReference Include="KUKULCAN.SharedKernel.JsonEngine" Version="..." />
\`\`\`

The exact package versions are determined by the released versions required by each application.

The consuming application should not replace these package dependencies with \`ProjectReference\` links to KUKULCAN source repositories.

## 10. HTTP consumption of KUKULCAN.SharedKernel.i18n

i18n is **not a NuGet dependency**. It is a separate Web API process.

The Example currently exposes:

\`\`\`text
GET /api/i18n/culture?culture=<language-code>
\`\`\`

The Example client calls the i18n API:

\`\`\`text
GET /api/v1/languages/<culture>
\`\`\`

The service-to-service request sends a short-lived JWT Bearer token.

The relevant settings must match:

| Setting | Example | kukulcan-i18n |
|---|---|---|
| Secret | \`I18n__JwtSecretKey\` | \`Jwt__SecretKey\` |
| Issuer | \`ATLAS\` | \`Jwt__Issuer\` |
| Audience | \`ATLAS.i18n\` | \`Jwt__Audience\` |

## 11. Existing Example integrations

### SharedKernel

Consumed as a NuGet package. Demonstrated through:

\`\`\`text
GET /api/result
\`\`\`

### Auth

Consumed as a NuGet package. Demonstrated through:

\`\`\`text
POST /api/auth/password/verify
POST /api/auth/local/authenticate
\`\`\`

### Database

Consumed as a NuGet package. Demonstrated through:

\`\`\`text
POST  /api/database/entities
GET   /api/database/entities/{id}
PATCH /api/database/entities/{id}
\`\`\`

The Example demonstrates the KUKULCAN database abstractions without introducing a production database provider into the application.

### JsonEngine

Consumed as a NuGet package. Its functional integration is independent from the i18n service.

### i18n

Consumed through HTTP. This demonstrates the architectural boundary between reusable in-process libraries and a reusable platform service.

## 12. Complete local startup

### PostgreSQL

\`\`\`bash
docker network create kukulcan-local

docker run --detach \\
  --name mypostgres \\
  --network kukulcan-local \\
  --env POSTGRES_DB=Atlas \\
  --env POSTGRES_USER=postgres \\
  --env POSTGRES_PASSWORD='<LOCAL_POSTGRES_PASSWORD>' \\
  --publish 5432:5432 \\
  postgres:16
\`\`\`

### i18n

From the i18n repository:

\`\`\`bash
docker build --tag kukulcan-i18n:local .

docker run --detach \\
  --name kukulcan-i18n \\
  --network kukulcan-local \\
  --publish 8080:8080 \\
  --env ASPNETCORE_HTTP_PORTS=8080 \\
  --env KUKULCAN__Database__Provider=PostgresSql \\
  --env KUKULCAN__Database__ConnectionString='Host=mypostgres;Port=5432;Database=Atlas;Username=postgres;Password=<LOCAL_POSTGRES_PASSWORD>' \\
  --env KUKULCAN__Database__Migration__AutoMigrateOnStartup=true \\
  --env KUKULCAN__Database__Migration__SeedDataOnStartup=true \\
  --env ConnectionStrings__Redis='' \\
  --env Jwt__SecretKey='<LOCAL_I18N_JWT_SECRET_MINIMUM_32_CHARACTERS>' \\
  --env Jwt__Issuer='ATLAS' \\
  --env Jwt__Audience='ATLAS.i18n' \\
  kukulcan-i18n:local
\`\`\`

### Example

\`\`\`bash
export I18n__JwtSecretKey='<LOCAL_I18N_JWT_SECRET_MINIMUM_32_CHARACTERS>'
dotnet run --project Source/KUKULCAN.SharedKernel.Example.Web
\`\`\`

## 13. Troubleshooting

### i18n cannot connect to PostgreSQL

Check:

\`\`\`bash
docker ps
docker network inspect kukulcan-local
docker exec mypostgres pg_isready --username postgres --dbname Atlas
docker logs kukulcan-i18n
\`\`\`

The i18n connection string must use \`Host=mypostgres\`.

### i18n returns 401

The Example and i18n service must use the same JWT secret, issuer and audience. The secret must contain at least 32 characters.

### Port 8080 is already in use

Publish another host port while keeping container port 8080:

\`\`\`bash
--publish 18080:8080
\`\`\`

Then configure:

\`\`\`text
I18n:BaseUrl=http://localhost:18080/
\`\`\`

### \`mypostgres\` already exists

Do not run another \`docker run --name mypostgres ...\`. Start the existing container:

\`\`\`bash
docker start mypostgres
\`\`\`

## 14. Local lifecycle

Stop:

\`\`\`bash
docker stop kukulcan-i18n
docker stop mypostgres
\`\`\`

Restart:

\`\`\`bash
docker start mypostgres
docker start kukulcan-i18n
\`\`\`

Remove the local containers when the environment is no longer required:

\`\`\`bash
docker rm kukulcan-i18n mypostgres
docker network rm kukulcan-local
\`\`\`

Removing the PostgreSQL container removes its container-local database storage unless a persistent Docker volume is configured.

## 15. Design rules for future ATLAS modules

Every ATLAS Web API module should preserve this dependency model:

\`\`\`text
ATLAS.<MODULE>
   │
   ├── NuGet → KUKULCAN.SharedKernel
   ├── NuGet → KUKULCAN.SharedKernel.Auth
   ├── NuGet → KUKULCAN.SharedKernel.Database
   ├── NuGet → KUKULCAN.SharedKernel.JsonEngine
   │
   └── HTTP  → KUKULCAN.SharedKernel.i18n
\`\`\`

The class libraries are reusable code dependencies.

The i18n component is a reusable platform service with its own process, API and database boundary.

The Example repository is the reference client demonstrating both consumption models.
