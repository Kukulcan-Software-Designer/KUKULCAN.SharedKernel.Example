# Integración continua y criterios de calidad

**Idioma:** [Inglés](CI-and-Quality-Gates)

## Workflows observados en `Develop`

| Workflow | Configuración | Limitaciones actuales |
|---|---|---|
| [CI](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/blob/Develop/.github/workflows/ci.yml) | Restauración, compilación Release y pruebas .NET 10 para `.slnx` | La última ejecución quedó en verde, pero la restauración no encontró proyectos; la solución no contiene proyectos reales. |
| [Reference Client](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/blob/Develop/.github/workflows/client.yml) | Restaura y compila el `.csproj` previsto para la API del Example | El `.csproj` indicado no existe en el árbol actual de `Develop`; no puede validar el código hasta que exista. |
| [Code Coverage](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/blob/Develop/.github/workflows/coverage.yml) | XPlat Code Coverage, ReportGenerator, informes Cobertura/HTML y publicación de artefactos | Aún no hay proyectos comprobables ni un informe verificado. |
| [Delete merged branches](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/blob/Develop/.github/workflows/delete-merged-branches.yml) | Pretende borrar ramas origen fusionadas del mismo repositorio, preservando `main` y `Develop` | La última ejecución observada falló. Debe revisarse el archivo antes de confiar en la limpieza automática; contiene una clave `env` repetida en un paso. |

## Observación de CI verificada

La última ejecución de CI observada es la [ejecución #1, del 13 de octubre de 2026](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/actions/runs/38419497568). Finalizó correctamente, pero el paso de restauración incluyó:

```text
warning : Unable to find a project to restore!
```

La compilación informó cero advertencias y errores; eso no significa que se haya compilado código de aplicación: no había proyectos que compilar. El comando de pruebas tampoco tenía un proyecto de tests declarado que ejecutar. Esta ejecución solo demuestra que los comandos del workflow terminan sobre una solución vacía, no que pase una suite de regresión de la aplicación.

## Criterios de calidad previstos

Cuando existan proyectos, CI debe validar:

- restauración determinista y compilación Release en .NET 10;
- todas las pruebas unitarias y de integración configuradas, con dependencias externas preparadas explícitamente;
- compilación de producción de la API y de cada cliente web;
- salida de cobertura con ensamblados de producción reales y pruebas ejecutadas;
- pruebas de integración de cada proveedor de base de datos declarado como soportado;
- permisos seguros de workflows, reglas de limpieza y tratamiento de forks;
- ausencia de secretos en el repositorio o en los artefactos emitidos.

## Requisitos de limpieza de ramas

El workflow debe ejecutarse solo tras una pull request fusionada, borrar únicamente ramas de origen del mismo repositorio y no borrar nunca `main` ni `Develop`. Deben validarse la sintaxis y los permisos. No debe considerarse un control de calidad obligatorio hasta corregir el fallo observado y superar un escenario real de cierre de PR.

## Política de calidad

Un estado verde solo es significativo cuando existen y se ejecutan los proyectos, jobs y aserciones previstos. No debe afirmarse que pasan las pruebas, la cobertura, la compatibilidad de proveedores o las compilaciones de cliente basándose únicamente en el resultado `success` de un workflow que no ejecutó esas comprobaciones sobre proyectos reales.
