# Wiki de KUKULCAN.SharedKernel.Example

**Idioma:** [Inglés](Home)

Bienvenido a la wiki del proyecto **KUKULCAN.SharedKernel.Example**, una aplicación de referencia cuyo objetivo es demostrar cómo integrar el ecosistema de bibliotecas compartidas KUKULCAN en una aplicación web orientada a procesos de negocio.

> **Estado del proyecto — 13 de octubre de 2026:** el repositorio se encuentra en la fase de planificación y preparación de sus fundamentos. Existe una solución `.NET 10` y hay flujos de GitHub Actions configurados, pero todavía no están presentes como proyectos versionados la API, los proyectos de pruebas, la implementación del dominio ni las cuatro interfaces de usuario. Por tanto, la cobertura aún **no se puede medir**. Una ejecución verde de CI no debe interpretarse como prueba de que se esté ejecutando código de la aplicación o sus tests.

## Índice de la wiki

### Proyecto y arquitectura

- [Descripción del proyecto](Descripcion-del-Proyecto) — propósito, alcance, estado actual y exclusiones.
- [Arquitectura](Arquitectura) — responsabilidades previstas, límites y flujo de las solicitudes.
- [Tecnologías](Tecnologias) — tecnologías existentes y planificadas.
- [Modelo de dominio](Modelo-de-Dominio) — entidades del esquema `Sample`, relaciones, búsquedas y reglas de ciclo de vida.
- [Base de datos y migraciones](Base-de-Datos-y-Migraciones) — persistencia configurable y configuración de base de datos.

### Seguridad e integraciones

- [Autenticación y seguridad](Autenticacion-y-Seguridad) — inicio de sesión, creación inicial del administrador, autorización y secretos.
- [Integraciones KUKULCAN](Integraciones-KUKULCAN) — SharedKernel, Auth, Database, JsonEngine e I18N.
- [Interfaces web](Interfaces-Web) — Razor Pages, Blazor, Angular y React.

### Ingeniería y entrega

- [Estrategia de pruebas](Estrategia-de-Pruebas) — pruebas unitarias, de integración, de contratos y de extremo a extremo.
- [Cobertura de código](Cobertura-de-Codigo) — workflow de cobertura, estado de medición y criterios para interpretar resultados.
- [CI y criterios de calidad](CI-y-Criterios-de-Calidad) — workflows existentes y limitaciones conocidas.
- [Flujo de desarrollo](Flujo-de-Desarrollo) — secuencia TEST → RED → Source → GREEN → Coverage.
- [Hoja de ruta e hitos](Hoja-de-Ruta-y-Hitos) — los seis Milestones y los 22 Issues.
- [Resolución de problemas](Resolucion-de-Problemas) — dudas habituales sobre configuración, CI y cobertura.

## Enlaces del repositorio

- [Repositorio](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example)
- [Rama Develop](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/tree/Develop)
- [Issues de GitHub](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues)
- [GitHub Actions](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/actions)
- [Workflow CI](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/blob/Develop/.github/workflows/ci.yml)
- [Workflow de cobertura](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/blob/Develop/.github/workflows/coverage.yml)

## Convenciones de documentación

La wiki distingue tres estados:

- **Implementado y verificado:** confirmado en el código del repositorio o en una prueba/ejecución observada que realmente ejercita el comportamiento.
- **Configurado pero aún no validado:** hay configuración, pero faltan los proyectos necesarios o una ejecución satisfactoria que demuestre el resultado.
- **Planificado:** especificado en Issues y Milestones, pero todavía no implementado.

El estado debe actualizarse conforme evolucione el repositorio. No se debe documentar como disponible una funcionalidad planificada ni publicar un porcentaje de cobertura sin un informe generado de verdad.
