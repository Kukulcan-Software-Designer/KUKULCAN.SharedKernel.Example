# Descripción del proyecto

**Idioma:** [Inglés](Project-Overview)

## Propósito

`KUKULCAN.SharedKernel.Example` es una aplicación de referencia para demostrar cómo consumir las bibliotecas reutilizables de KUKULCAN desde una aplicación con una API HTTP común y varias interfaces web independientes.

El ejemplo previsto reúne:

- `KUKULCAN.SharedKernel`, para primitivas compartidas y contratos de aplicación.
- `KUKULCAN.SharedKernel.Auth`, para las capacidades de autenticación que admita su API pública.
- `KUKULCAN.SharedKernel.Database`, para persistencia configurable por proveedor mediante sus contratos públicos reales.
- `KUKULCAN.SharedKernel.JsonEngine`, para un caso de uso concreto de procesamiento JSON.
- `KUKULCAN.SharedKernel.I18N`, como servicio Web API desplegado por separado.
- Una Web API del Example consumida por clientes Razor Pages, Blazor, Angular y React.

## Demostración de negocio prevista

El modelo de datos se ubicará en el esquema de base de datos `Sample` e incluirá clientes, proveedores, productos, almacenes, ventas, compras y existencias de almacén. Se espera que la API demuestre operaciones habituales de creación, consulta y actualización, filtros de búsqueda, autenticación, integridad de datos y la política acordada de conservación histórica.

## Qué no es este proyecto

- No sustituye las responsabilidades reutilizables de las bibliotecas KUKULCAN.
- No son cuatro backends independientes: las cuatro interfaces son clientes de una misma API del Example.
- No es un producto ERP o CRM de producción; su finalidad principal es demostrar y verificar integraciones y patrones.
- No debe afirmar que un proveedor de base de datos o un cliente web está soportado hasta validar su compilación y su comportamiento relevante.

## Estado del repositorio a 13 de octubre de 2026

La rama `Develop` contiene archivos de gobernanza y documentación, una solución `.NET 10` y archivos de workflows de GitHub Actions. La solución contiene carpetas y elementos, pero todavía no declara un proyecto de producción ni de pruebas. Los directorios versionados `Source/` y `Tests/` aún no contienen proyectos.

La ejecución de CI observada el 13 de octubre de 2026 terminó correctamente, pero el paso de restauración registró `Unable to find a project to restore!`; la compilación finalizó sin realizar trabajo de compilación. Por tanto, el estado verde **no** demuestra que la aplicación compile ni que se hayan ejecutado tests contra código de producción.

La aplicación, la API, el modelo de persistencia, el conjunto de pruebas y las cuatro interfaces siguen siendo trabajo planificado, reflejado en los [Issues del proyecto](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues).

## Entregables principales previstos

1. Una solución `.slnx` de .NET 10 compilable, con proyectos de API y pruebas automatizadas.
2. Una arquitectura documentada con límites explícitos de integración.
3. Autenticación segura e inicialización idempotente del administrador mediante contratos de biblioteca soportados.
4. Modelo de dominio `Sample` e inicialización versionada del esquema.
5. Una API común con reglas de negocio, búsquedas y políticas de ciclo de vida.
6. Validación con proveedores reales para PostgreSQL, Microsoft SQL Server y MySQL, cuando los paquetes y la implementación lo permitan.
7. Cuatro clientes web independientes y autenticados.
8. Pruebas automatizadas significativas, informes reales de cobertura, comprobaciones de CI y documentación alineada con la implementación.

## Fuentes de referencia

- [README del repositorio](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/blob/Develop/README.md)
- [Gobernanza](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/blob/Develop/GOVERNANCE.md)
- [Contribución](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/blob/Develop/CONTRIBUTING.md)
- [Política de seguridad](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/blob/Develop/SECURITY.md)
