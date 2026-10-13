# Interfaces web

**Idioma:** [Inglés](Web-Interfaces)

## Backend común

Las cuatro interfaces son clientes distintos de una única Web API del Example. La API es responsable de validación de aplicación, autorización, reglas de dominio, persistencia y acceso a servicios externos. Ninguna interfaz puede conectarse directamente a la base de datos.

## Interfaces requeridas

| Interfaz | Responsabilidades previstas | Estado actual |
|---|---|---|
| Razor Pages | Pantallas renderizadas en servidor, comenzando por el inicio de sesión; flujos de Cliente, Proveedor, Producto, Almacén/existencias, ventas y compras | Planificada; el proyecto no existe aún |
| Blazor | Flujos equivalentes mediante un modelo de alojamiento Blazor documentado | Planificada; modelo de alojamiento pendiente |
| Angular | Cliente de navegador, inicio de sesión, consumo de API y gestión de errores/validación | Planificada; no existe manifiesto de paquetes ni build |
| React | Cliente de navegador, inicio de sesión, consumo de API y gestión de errores/validación | Planificada; no existe manifiesto de paquetes ni build |

## Comportamiento coherente

Todos los clientes deben utilizar los mismos contratos de solicitud/respuesta de la API, reglas de autenticación, semántica de búsqueda, validación de datos y significado de errores. La presentación puede diferir, pero las interfaces no deben implementar reglas de negocio independientes.

La experiencia de búsqueda prevista incluye:

- Clientes: coincidencia parcial por nombre y filtros por ciudad, estado/región y código postal, combinables cuando proceda.
- Existencias de almacén: filtros por código de producto y coincidencia parcial por nombre del producto.
- Ventas: filtros por cliente y fecha.

## Autenticación

El inicio de sesión debe ser la primera pantalla de una interfaz cuando el usuario no está autenticado. La gestión de sesión debe ajustarse al modelo de alojamiento. La API debe proteger las operaciones incluso si la interfaz oculta o deshabilita los controles.

## Requisitos de compilación y CI de los clientes

- Cada cliente debe tener instrucciones reproducibles de instalación, compilación de producción y pruebas.
- Las versiones de Node.js y del gestor de paquetes deben fijarse/documentarse para Angular y React.
- El workflow de cliente actual apunta a `Source/KUKULCAN.SharedKernel.Example.Web/KUKULCAN.SharedKernel.Example.Web.csproj`, pero ese proyecto aún no existe. El workflow no puede validar significativamente el cliente hasta que se cree el proyecto esperado.
- Añadir jobs de CI independientes para los frontend cuando existan los proyectos Angular y React; no afirmar que el workflow .NET actual comprueba esos bundles de navegador.
- Comprobar los bundles generados para detectar secretos del servidor y validar el comportamiento ante sesiones caducadas o inválidas.

Issues: [#9](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/9), [#10](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/10), [#11](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/11) y [#12](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/12).
