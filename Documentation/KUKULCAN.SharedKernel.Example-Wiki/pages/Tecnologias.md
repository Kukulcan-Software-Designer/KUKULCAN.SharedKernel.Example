# Tecnologías

**Idioma:** [Inglés](Technology-Stack)

## Tecnologías visibles actualmente en el repositorio

| Tecnología | Uso observado | Estado actual |
|---|---|---|
| .NET SDK 10.0.x | Configuración de CI y solución objetivo | Configurado; todavía no existe un proyecto de aplicación |
| `.slnx` | `KUKULCAN.SharedKernel.Example.slnx` | Presente; por ahora solo carpetas y elementos de solución |
| GitHub Actions | Workflows de CI, cliente de referencia, cobertura y limpieza de ramas en `Develop` | Definiciones presentes; algunas comprobaciones no pueden validar proyectos ausentes |
| Markdown | Gobernanza y documentación del proyecto | Presente |

`Directory.Build.props` declara actualmente los metadatos de versión `1.0.0` y activa la auditoría de NuGet (`NuGetAudit=true`). Esto no implica que se haya generado un paquete de aplicación ni una publicación.

## Pila objetivo del backend

- ASP.NET Core Web API sobre .NET 10.
- Entity Framework Core y persistencia mediante los contratos públicos de `KUKULCAN.SharedKernel.Database`.
- Contratos compartidos de `KUKULCAN.SharedKernel`.
- Capacidades de autenticación de `KUKULCAN.SharedKernel.Auth` admitidas por su API publicada.
- `KUKULCAN.SharedKernel.JsonEngine` para un caso de uso JSON explícito de la aplicación.
- Integración HTTP con la API `KUKULCAN.SharedKernel.I18N`, desplegada por separado.

Las versiones exactas de los paquetes deben obtenerse de los archivos de proyecto y de las versiones publicadas compatibles cuando existan proyectos de aplicación; no se deben adivinar en esta wiki.

## Pila objetivo de los clientes

| Interfaz | Tecnología prevista | Estado |
|---|---|---|
| Razor Pages | ASP.NET Core Razor Pages | Planificada; el proyecto aún no existe |
| Blazor | Blazor | Planificada; todavía no se ha elegido el modelo de alojamiento |
| Angular | Angular + herramientas de Node.js | Planificada; no existe aún manifiesto de paquetes |
| React | React + herramientas de Node.js | Planificada; no existe aún manifiesto de paquetes |

Todos los clientes consumirán la misma Web API del Example. Las versiones exactas de frameworks, el gestor de paquetes, los comandos de compilación, el framework de pruebas de navegador y los navegadores soportados deben documentarse cuando los clientes estén implementados.

## Objetivos de base de datos

PostgreSQL, Microsoft SQL Server y MySQL son objetivos de validación. Solo deben declararse como soportados cuando existan el paquete/proveedor y la configuración pertinentes y pasen las pruebas de migración, persistencia, consultas y ciclo de vida contra instancias reales.

## Herramientas de pruebas e informes

El workflow actual de cobertura configura el recolector XPlat Code Coverage y ReportGenerator. Todavía no hay un proyecto de pruebas en la solución, por lo que no se ha verificado qué framework está activo en este repositorio. El framework, el adaptador y la configuración de cobertura deben quedar explícitos en el proyecto de pruebas y su workflow cuando se implemente.
