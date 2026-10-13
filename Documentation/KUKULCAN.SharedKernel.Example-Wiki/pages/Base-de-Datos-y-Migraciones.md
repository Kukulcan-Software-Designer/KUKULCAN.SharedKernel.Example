# Base de datos y migraciones

**Idioma:** [Inglés](Database-and-Migrations)

## Objetivo de diseño

El Example debe demostrar las capacidades reutilizables de persistencia de `KUKULCAN.SharedKernel.Database`, sin crear una bifurcación ni reproducir esa infraestructura. El esquema objetivo es `Sample`.

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
3. Generar migraciones versionadas para el esquema `Sample` y los proveedores seleccionados, respetando las convenciones del paquete.
4. Verificar que se puede inicializar una base de datos nueva desde cero.
5. Verificar que la aplicación repetida y las rutas de actualización no recrean ni corrompen datos.
6. Probar claves, claves foráneas, índices, precisión decimal, comparación de cadenas, borrado lógico y conservación del histórico de ventas/compras.
7. Hacer explícitas las diferencias entre proveedores, incluidas compatibilidad con esquemas, reglas de identificadores, intercalación de cadenas y tipos de columna soportados.
8. Documentar una configuración local reproducible de pruebas de integración sin incluir credenciales en el repositorio.

## Transacciones y unidad de trabajo

Utilizar las abstracciones reales `KukulcanDbContextBase`/`IUnitOfWork` cuando sean aplicables al contrato público del paquete. Definir límites transaccionales para operaciones que afecten a varios registros, en particular cabeceras y líneas de documentos o actualizaciones de existencias. Evitar operaciones parcialmente persistidas cuando falle la validación o la escritura en base de datos.

## Criterios de aceptación

- El Example guarda y recupera una entidad mediante el contrato establecido del paquete Database.
- La configuración es externa y está documentada.
- El esquema puede inicializarse y migrarse repetidamente de forma segura.
- El soporte de cada proveedor está respaldado por pruebas de integración específicas.
- Las operaciones normales de API no pueden borrar físicamente el histórico comercial protegido.
