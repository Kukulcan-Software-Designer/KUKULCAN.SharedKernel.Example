# Base de datos y migraciones

**Idioma:** [Inglés](Database-and-Migrations)

## Objetivo de diseño

El Example debe demostrar las capacidades reutilizables de persistencia de `KUKULCAN.SharedKernel.Database`, sin crear una bifurcación ni reproducir esa infraestructura. Todas las tablas propias, incluido `DatabaseIntegrationRecord`, usan el esquema `Example`.

## Contrato de configuración

Los nombres de variables de entorno acordados son:

```text
Kukulcan__Database__Provider
Kukulcan__Database__ConnectionString
```

El doble guion bajo representa claves de configuración anidadas en el proveedor estándar de configuración de variables de entorno de .NET. Los valores deben suministrarse mediante el entorno de despliegue o un almacén local de secretos; nunca se deben confirmar credenciales en la configuración versionada.

Los identificadores de proveedor aceptados y los formatos exactos de las cadenas de conexión deben seguir la implementación real del paquete Database y los requisitos de EF Core de cada proveedor. Esta wiki no debe inventar alias.

## Proveedores previstos

- PostgreSQL
- Microsoft SQL Server
- MySQL

Son objetivos de validación, no una afirmación de que el Example funcione actualmente con los tres. Un proveedor solo se considera soportado cuando se validan contra una instancia real su registro de paquete, migraciones, restricciones de esquema, consultas y pruebas de integración.

## Requisitos de migración

1. Definir las relaciones y restricciones de las entidades en el Issue del modelo.
2. Usar las capacidades de migración e inicialización que realmente exponga el paquete Database.
3. Generar migraciones versionadas para las entidades del esquema `Example` y los proveedores seleccionados, respetando las convenciones del paquete.
4. Verificar que se puede inicializar una base de datos nueva desde cero.
5. Verificar que la aplicación repetida y las rutas de actualización no recrean ni corrompen datos.
6. Probar claves, claves foráneas, índices, precisión decimal, comparación de cadenas, borrado lógico y conservación del histórico de ventas/compras.
7. Hacer explícitas las diferencias entre proveedores, incluidas compatibilidad con esquemas, reglas de identificadores, intercalación de cadenas y tipos de columna soportados.
8. Documentar una configuración local reproducible de pruebas de integración sin incluir credenciales en el repositorio.

## Ejecución de las migraciones versionadas

Las migraciones se mantienen en tres proyectos independientes por proveedor:

- `KUKULCAN.SharedKernel.Example.Migrations.PostgreSQL`
- `KUKULCAN.SharedKernel.Example.Migrations.SQLServer`
- `KUKULCAN.SharedKernel.Example.Migrations.MySQL`

Cada proyecto contiene `InitialExampleSchema`, una instantánea del modelo y una `ExampleDbContextFactory`. La fábrica comprueba que el proveedor de las variables de entorno coincide con el proyecto y exige la cadena de conexión. Todas las tablas propias del Example, incluido `DatabaseIntegrationRecords`, pertenecen al esquema `Example`.

Si es necesario, instala la herramienta de EF Core de la línea .NET 10:

```bash
dotnet tool install --global dotnet-ef --version 10.*
dotnet ef --version
```

Define el proveedor y la cadena de conexión en la sesión de shell. Utiliza una base de datos desechable para desarrollo/pruebas y no guardes secretos reales en archivos versionados:

```bash
export KUKULCAN__DATABASE__PROVIDER='PostgresSql'
export KUKULCAN__DATABASE__CONNECTIONSTRING='Host=localhost;Database=Example;Username=...;Password=...;'
```

Para listar y aplicar las migraciones de PostgreSQL:

```bash
dotnet ef migrations list \
  --project Source/WebAPI/KUKULCAN.SharedKernel.Example.Migrations.PostgreSQL/KUKULCAN.SharedKernel.Example.Migrations.PostgreSQL.csproj \
  --context ExampleDbContext

dotnet ef database update \
  --project Source/WebAPI/KUKULCAN.SharedKernel.Example.Migrations.PostgreSQL/KUKULCAN.SharedKernel.Example.Migrations.PostgreSQL.csproj \
  --context ExampleDbContext
```

Para SQL Server, utiliza `KUKULCAN__DATABASE__PROVIDER='SqlServer'` y el proyecto `KUKULCAN.SharedKernel.Example.Migrations.SQLServer.csproj`. Para MySQL, utiliza `KUKULCAN__DATABASE__PROVIDER='MySql'` y el proyecto `KUKULCAN.SharedKernel.Example.Migrations.MySQL.csproj`. En ambos casos, suministra la cadena correspondiente al proveedor mediante `KUKULCAN__DATABASE__CONNECTIONSTRING`; no reutilices una cadena de conexión de otro proveedor.

Cuando cambie el modelo, genera una migración nueva en cada proyecto de proveedor que se vaya a mantener soportado, usando el mismo nombre lógico, por ejemplo:

```bash
dotnet ef migrations add <MigrationName> \
  --project Source/WebAPI/KUKULCAN.SharedKernel.Example.Migrations.PostgreSQL/KUKULCAN.SharedKernel.Example.Migrations.PostgreSQL.csproj \
  --context ExampleDbContext \
  --output-dir Migrations
```

Repite el procedimiento para los proyectos de SQL Server y MySQL. Revisa la migración y la instantánea generadas antes de aplicarlas. Nunca edites una migración que ya se haya aplicado. Las instantáneas de las migraciones iniciales se comparan automáticamente con el modelo EF actual para las tres configuraciones; la prueba con un contenedor real y reaplicación de migraciones se ejecuta actualmente contra PostgreSQL. SQL Server y MySQL no se consideran validados hasta que pasen las pruebas de sus Issues #21 y #22. MySQL utiliza semántica de base de datos/catálogo para los nombres de esquema; el catálogo `Example` debe aprovisionarse y validarse según el proveedor MySQL antes de afirmar que está soportado.

El paquete Database ofrece `Kukulcan:Database:Migration:AutoMigrateOnStartup`, cuyo valor predeterminado es `false`. En producción aplica las migraciones como un paso explícito del despliegue tras revisar la migración y el proveedor objetivo. `EnsureCreatedAsync` no sustituye a las migraciones versionadas.

## Seguridad de las migraciones y privilegios de base de datos

Utiliza identidades de base de datos separadas para desplegar migraciones y para el tráfico normal de la aplicación:

- La identidad de migración debe disponer únicamente de los privilegios DDL necesarios para aplicar las migraciones revisadas en la base de datos y el esquema previstos. No utilices una cuenta administradora de todo el servidor ni la cuenta root de MySQL para los despliegues habituales.
- La identidad de ejecución de la aplicación debe tener solo los privilegios de acceso a datos que necesite sobre el esquema `Example`; normalmente no debe poder crear, alterar ni eliminar objetos del esquema.
- Guarda las credenciales de ambas identidades en el almacén de secretos del despliegue o en configuración de entorno protegida. No incluyas credenciales en el repositorio, scripts, registros ni archivos fuente de migración.
- Ejecuta las migraciones como un paso explícito del despliegue, confirma el entorno de destino y dispone de un plan de recuperación/copia de seguridad adecuado. Mantén desactivado `Kukulcan:Database:Migration:AutoMigrateOnStartup` en producción.
- La sintaxis de concesión y los permisos de esquema/catálogo varían según el proveedor. Verifica los permisos mínimos exactos para el proveedor de destino antes del despliegue; no deduzcas los permisos de SQL Server o MySQL a partir de la prueba de integración de PostgreSQL.

## Transacciones y unidad de trabajo

Utilizar las abstracciones reales `KukulcanDbContextBase`/`IUnitOfWork` cuando sean aplicables al contrato público del paquete. Definir límites transaccionales para operaciones que afecten a varios registros, en particular cabeceras y líneas de documentos o actualizaciones de existencias. Evitar operaciones parcialmente persistidas cuando falle la validación o la escritura en base de datos.

## Criterios de aceptación

- El Example guarda y recupera una entidad mediante el contrato establecido del paquete Database.
- La configuración es externa y está documentada.
- El esquema puede inicializarse y migrarse repetidamente de forma segura.
- El soporte de cada proveedor está respaldado por pruebas de integración específicas.
- Las operaciones normales de API no pueden borrar físicamente el histórico comercial protegido.
