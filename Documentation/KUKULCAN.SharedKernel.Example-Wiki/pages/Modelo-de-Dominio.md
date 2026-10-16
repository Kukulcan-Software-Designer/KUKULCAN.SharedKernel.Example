# Modelo de dominio

**Idioma:** [Inglés](Domain-Model)

## Alcance

El dominio del Example está planificado bajo el esquema de base de datos `Sample`. El modelo siguiente está especificado en los Issues del proyecto, pero todavía no está implementado en entidades EF Core ni en migraciones versionadas.

## Entidades requeridas

| Entidad | Propósito | Relaciones y reglas principales |
|---|---|---|
| `Client` | Cliente que compra mercancías | Relacionado con facturas de venta (`Invoice`); usa borrado lógico |
| `Provider` | Proveedor que vende mercancías a la empresa | Relacionado con documentos de compra (`ProdInvoice`); usa borrado lógico |
| `Product` | Catálogo de productos | Fuente autoritativa del código y nombre del producto |
| `Warehouse` | Catálogo de almacenes/ubicaciones | Relacionado con `WarehouseStock` |
| `Invoice` | Operación de venta | Referencia a un `Client`; las operaciones normales de API no deben borrar físicamente el histórico comercial |
| `ProdInvoice` | Operación de compra | Referencia a un `Provider`; las operaciones normales de API no deben borrar físicamente el histórico comercial |
| `WarehouseStock` | Cantidad de un producto en un almacén | Referencia a `Warehouse` y `Product`; no duplica código/nombre del producto |
| Línea(s) de factura | Producto, cantidad y precio por documento de venta/compra | Los nombres y atributos definitivos se concretarán al diseñar el modelo |

## Resumen de relaciones

```text
Client 1 ─── * Invoice 1 ─── * InvoiceLine * ─── 1 Product
Provider 1 ─ * ProdInvoice 1 ─ * PurchaseLine * ─ 1 Product
Warehouse 1 ─ * WarehouseStock * ─ 1 Product
```

`InvoiceLine` y `PurchaseLine` son nombres explicativos del diagrama, no nombres de entidades aprobados. La implementación debe definir los nombres reales y documentar si las líneas de venta y compra comparten una abstracción o utilizan tipos separados.

## Reglas de integridad

- Mapear todas las entidades requeridas al esquema `Sample`.
- Definir claves primarias, claves foráneas, campos obligatorios, longitudes/tipos de columna razonables e índices apropiados.
- Obtener el código y nombre de producto a través de la relación con `Product`; no copiarlos en cada fila de stock.
- Aplicar unicidad a `(WarehouseId, ProductId)`, salvo que exista un requisito explícito de múltiples lotes, ubicaciones o compartimentos. Si aparece ese requisito, modelarlo expresamente en lugar de eliminar silenciosamente la restricción.
- Las cantidades y los precios deben tener precisión numérica y validación adecuadas.
- El histórico de ventas/compras debe seguir siendo legible tras el borrado lógico de un cliente o proveedor relacionado.
- Aplicar validaciones en la aplicación y restricciones en la base de datos donde corresponda.

## Comportamiento de búsqueda requerido

### Clientes

Permitir coincidencia parcial por nombre y filtros por ciudad, estado/región y código postal. Los filtros deben poder combinarse. Definir la sensibilidad a mayúsculas/minúsculas según la semántica que admita el proveedor y probarla.

### Existencias de almacén

Permitir filtrar por código de producto y por coincidencia parcial del nombre a través de `WarehouseStock.Product`. Devolver el código/nombre canónico de la relación con `Product`, no copias redundantes almacenadas en stock.

### Ventas

Permitir consultas filtradas por cliente y rango de fechas. Durante el diseño de API, especificar límites inclusivos/exclusivos, interpretación de zona horaria, ordenación y paginación.

## Políticas de ciclo de vida

- `Client` y `Provider`: borrado lógico; las consultas normales excluyen por defecto los registros inactivos/eliminados.
- `Invoice` y `ProdInvoice`: conservar el histórico comercial; no ofrecer endpoints normales de borrado físico.
- Actualización de existencias: validar referencias a producto/almacén y rechazar cantidades inválidas o filas de stock duplicadas según el modelo aprobado.

Véanse [Base de datos y migraciones](Base-de-Datos-y-Migraciones), [Autenticación y seguridad](Autenticacion-y-Seguridad) y los Issues [#7](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/7), [#8](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/8), [#18](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/18) y [#19](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/19).
