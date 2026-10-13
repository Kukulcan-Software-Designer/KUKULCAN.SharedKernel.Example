# Estrategia de pruebas

**Idioma:** [Inglés](Testing-Strategy)

## Estado actual

A 13 de octubre de 2026, la solución no declara proyectos de aplicación ni de pruebas y los directorios versionados `Source/` y `Tests/` aún no contienen proyectos. Por tanto, no existe actualmente una suite de pruebas unitarias o de integración del Example cuyos resultados se puedan afirmar. El workflow de CI está configurado para ejecutar `dotnet test`, pero la ejecución verde observada no restauró ningún proyecto y no demuestra que se pruebe el comportamiento de la aplicación.

El framework de pruebas y su adaptador para el Example todavía no se han confirmado en un proyecto. Deben elegirse y fijarse cuando se establezca la estructura de producción y pruebas.

## Capas de pruebas requeridas

### Pruebas unitarias

Probar comportamiento determinista sin base de datos ni servicios HTTP externos:

- Validación de solicitudes, transformación de respuestas y reglas de códigos HTTP.
- Política de borrado lógico de clientes/proveedores.
- Restricciones sobre el histórico de ventas y compras.
- Invariantes entre producto, almacén y existencias.
- Semántica de filtros combinables y paginación.
- Traducción de errores de autenticación e idempotencia del arranque del administrador usando abstracciones apropiadas.
- Gestión de errores cuando los límites externos devuelven respuestas inválidas o no están disponibles.

### Pruebas de integración de API

Utilizar un host de pruebas de la aplicación u otro mecanismo de integración compatible para verificar routing, serialización, inyección de dependencias, autorización, contratos API y coordinación de la persistencia. Los endpoints protegidos deben rechazar a usuarios no autenticados o no autorizados.

### Pruebas de integración de base de datos

Utilizar instancias reales de cada proveedor para comportamientos cuyo resultado dependa de EF Core o del motor. Validar esquema `Sample`, migraciones, relaciones, restricciones únicas, consultas, coincidencia de cadenas, límites transaccionales, borrado lógico y conservación del histórico. El soporte debe demostrarse por separado para PostgreSQL, SQL Server y MySQL.

### Pruebas de integración con servicios externos

Probar éxito HTTP de I18N, timeout, servicio no disponible, respuesta inválida y traducción segura de errores. Usar simulaciones deterministas para pruebas rápidas y un servicio configurado aparte para verificaciones explícitas de extremo a extremo.

### Pruebas de interfaz

Para cada cliente implementado, validar inicio de sesión, acceso a pantallas protegidas, búsqueda, mensajes de validación, fallos de API, sesiones caducadas y al menos un escenario de negocio de extremo a extremo cuando sea viable.

### Pruebas relevantes para seguridad

Cubrir gestión de contraseñas/secretos iniciales, límites de autenticación y autorización, contexto de tenant si existe, borrado lógico, intentos de borrar histórico comercial y ausencia de secretos del servidor en los artefactos de cliente.

## Flujo de desarrollo dirigido por pruebas

Usar la secuencia acordada:

1. **TEST:** añadir una prueba de comportamiento para el criterio de aceptación.
2. **RED:** ejecutarla y comprobar que falla por el comportamiento ausente previsto. Publicar/observar el RED de CI cuando lo exija el proceso del repositorio.
3. **Source:** implementar el cambio mínimo de producción.
4. **GREEN:** repetir las pruebas pertinentes y confirmar que el comportamiento esperado pasa.
5. **Coverage:** generar e inspeccionar el informe real de cobertura.

No se deben añadir pruebas únicamente para manipular el porcentaje de cobertura. Toda prueba debe demostrar un comportamiento o límite de regresión significativo.

## Issue transversal de pruebas

El Issue [#13 — pruebas centradas en comportamiento](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/13) es transversal. Las pruebas deben incorporarse con cada funcionalidad, no posponerse hasta que se haya completado todo.
