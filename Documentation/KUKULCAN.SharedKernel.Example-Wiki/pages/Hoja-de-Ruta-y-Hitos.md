# Hoja de ruta e hitos

**Idioma:** [Inglés](Roadmap-and-Milestones)

Esta hoja de ruta refleja los seis Milestones acordados para el backlog de 22 Issues. Las fechas son objetivos de planificación, no garantías. Los Milestones y las asociaciones de Issues se mantienen en GitHub; esta página debe actualizarse si cambian las fechas o el alcance.

## M1 — Fundamentos y arquitectura
**Fecha límite:** 16 de octubre de 2026

**Objetivo:** establecer una solución .NET 10 compilable y definir responsabilidades de proyectos, dependencias y límites de integración.

**Issues:**
- [#1 — Rehacer los fundamentos y la estructura de la solución .NET 10](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/1)
- [#16 — Definir la arquitectura y los límites de integración del Example](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/16)

## M2 — Integraciones KUKULCAN y autenticación
**Fecha límite:** 30 de octubre de 2026

**Objetivo:** integrar los contratos públicos verificados de SharedKernel, Auth, Database y JsonEngine, y consumir la Web API independiente de I18N con configuración segura y gestión de errores probada.

**Issues:**
- [#2 — Demostrar la integración con KUKULCAN.SharedKernel](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/2)
- [#3 — Implementar autenticación y creación segura del administrador](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/3)
- [#4 — Integrar Database con proveedores configurables](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/4)
- [#5 — Demostrar la integración con JsonEngine](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/5)
- [#6 — Integrar la Web API de I18N](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/6)

## M3 — Dominio Sample y proveedores de base de datos
**Fecha límite:** 13 de noviembre de 2026

**Objetivo:** definir el modelo de dominio y el esquema `Sample`, hacer repetibles las migraciones y validar PostgreSQL, SQL Server y MySQL de forma independiente cuando estén soportados.

**Issues:**
- [#7 — Definir el modelo de dominio y esquema Sample](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/7)
- [#17 — Implementar migraciones e inicialización del esquema Sample](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/17)
- [#20 — Validar persistencia con PostgreSQL](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/20)
- [#21 — Validar persistencia con Microsoft SQL Server](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/21)
- [#22 — Validar persistencia con MySQL](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/22)

## M4 — Web API y reglas de negocio
**Fecha límite:** 27 de noviembre de 2026

**Objetivo:** implementar endpoints y filtros de búsqueda, validación, actualizaciones seguras, borrado lógico de clientes/proveedores y conservación del histórico comercial.

**Issues:**
- [#8 — Implementar endpoints de la API y filtros de búsqueda](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/8)
- [#18 — Implementar actualizaciones, validación y reglas de negocio](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/18)
- [#19 — Aplicar borrado lógico y conservación histórica](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/19)

## M5 — Cuatro interfaces web
**Fecha límite:** 11 de diciembre de 2026

**Objetivo:** entregar clientes Razor Pages, Blazor, Angular y React compilables de forma independiente, todos consumiendo la misma API, con inicio de sesión como pantalla inicial y gestión segura de sesión.

**Issues:**
- [#9 — Interfaz web Razor](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/9)
- [#10 — Interfaz web Blazor](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/10)
- [#11 — Interfaz web Angular](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/11)
- [#12 — Interfaz web React](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/12)

## M6 — Calidad, CI y documentación
**Fecha límite:** 18 de diciembre de 2026

**Objetivo:** completar pruebas centradas en comportamiento y documentación alineada con la implementación, garantizando que CI, cobertura y comprobaciones de proveedores y clientes ejecuten proyectos reales.

**Issues:**
- [#13 — Añadir pruebas de API, autenticación y persistencia centradas en comportamiento](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/13)
- [#14 — Adaptar y validar CI, cliente de referencia, cobertura y limpieza de ramas](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/14)
- [#15 — Alinear la documentación del repositorio con el Example implementado](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/15)

## Realismo del calendario

Estas fechas establecen puntos de control a lo largo de unas nueve semanas y media desde mediados de octubre hasta mediados de diciembre de 2026. La estimación se basa en ventanas de actividad observadas en repositorios KUKULCAN relacionados, no en horas de trabajo medidas. En esos repositorios hubo tareas en paralelo y las fechas de PR no reflejan todas las sesiones de planificación, pruebas o trabajo fuera de línea. Revaluar el calendario cuando existan la solución y el alcance inicial de API/persistencia.
