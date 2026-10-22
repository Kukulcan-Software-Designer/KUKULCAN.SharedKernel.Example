# Modelo de dominio

**Idioma:** [Inglés](Domain-Model)

## Estado y alcance

Los nueve tipos de entidad del dominio Sample y sus mapeos EF Core están implementados en los proyectos Domain e Infrastructure del Example. Las migraciones versionadas y las comprobaciones de persistencia con proveedores reales pertenecen a las Issues #17 y #20–#22; este modelo no afirma que los tres proveedores estén validados.

## Entidades

| Entidad | Propósito | Integridad e histórico |
|---|---|---|
| `Client` | Datos maestros del cliente | Aislado por tenant; admite borrado lógico compartido mediante `IsDeleted` y `DeletedOn` |
| `Provider` | Datos maestros del proveedor | Aislado por tenant; admite borrado lógico compartido mediante `IsDeleted` y `DeletedOn` |
| `Product` | Catálogo canónico de productos | `Code` es único por tenant; fuente del código/nombre actual del producto |
| `Warehouse` | Catálogo de almacenes/ubicaciones | Aislado por tenant y relacionado con las existencias |
| `WarehouseStock` | Cantidad por producto y almacén | Índice único `(WarehouseId, ProductId)`; no duplica código/nombre actuales del producto |
| `Invoice` | Documento de venta | Referencia un `Client`, conserva una copia histórica del nombre del cliente y no elimina en cascada las líneas |
| `InvoiceLine` | Producto, cantidad y precio unitario de una venta | Conserva copias históricas del código/nombre del producto y usa precisión decimal fija |
| `ProdInvoice` | Documento de compra | Referencia un `Provider`, conserva una copia histórica del nombre del proveedor y no elimina en cascada las líneas |
| `ProdInvoiceLine` | Producto, cantidad y precio unitario de una compra | Conserva copias históricas del código/nombre del producto y usa precisión decimal fija |

Todas las entidades utilizan un `Id` GUID y un `TenantId`. Las claves foráneas compuestas combinan `TenantId` con el identificador relacionado para impedir, también en la base de datos, que una fila de un tenant se relacione con registros de otro. El contexto compartido Database aplica su filtro de tenant. El filtrado de borrado lógico de clientes/proveedores usa el contrato compartido `ISoftDelete`.

## Resumen de relaciones

```text
Client 1 ─── * Invoice 1 ─── * InvoiceLine * ─── 1 Product
Provider 1 ─ * ProdInvoice 1 ─ * ProdInvoiceLine * ─ 1 Product
Warehouse 1 ─ * WarehouseStock * ─ 1 Product
```

Las líneas de venta y de compra son tipos separados porque pertenecen a documentos distintos. Sus campos de código/nombre de producto son copias históricas; las existencias, en cambio, contienen solo las referencias al producto y al almacén, junto con la cantidad.

## Mapeo y restricciones de base de datos

- Todas las entidades de negocio se mapean al esquema `Sample` con un nombre explícito de tabla.
- Las claves primarias son valores GUID `Id`. Las claves alternativas compuestas (`TenantId`, `Id`) permiten relaciones seguras entre tenants.
- `WarehouseStock` tiene un índice único sobre `(WarehouseId, ProductId)`.
- Los códigos de producto y los números de documentos de venta/compra son únicos por tenant.
- El borrado de clientes, proveedores, productos, almacenes, facturas de venta o compra se restringe cuando existen filas relacionadas; las líneas no se borran en cascada.
- Los precios unitarios usan `decimal(18,2)`; las cantidades y existencias usan `decimal(18,3)`.
- `Client` contiene los campos necesarios para la búsqueda futura: `Name`, `City`, `State` y `PostalCode`.

## Límites respecto a las siguientes Issues

- La Issue #17 es responsable de las migraciones versionadas y la inicialización repetible del esquema. Esta Issue establece el modelo CLR y sus mapeos EF Core.
- Las Issues #20–#22 validan la persistencia contra instancias reales de PostgreSQL, SQL Server y MySQL; instalar paquetes no demuestra soporte.
- La Issue #8 implementa las operaciones Web API y los filtros de búsqueda requeridos.
- La Issue #18 es responsable de la validación de solicitudes y las reglas de negocio detalladas.
- La Issue #19 es responsable del flujo de borrado de la API y de conservar los datos históricos. El modelo ya aporta campos de borrado lógico y relaciones restrictivas que respaldan esa política.

Véanse [Base de datos y migraciones](Base-de-Datos-y-Migraciones), [Autenticación y seguridad](Autenticacion-y-Seguridad) y las Issues [#7](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/7), [#17](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/17), [#18](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/18) y [#19](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/19).
