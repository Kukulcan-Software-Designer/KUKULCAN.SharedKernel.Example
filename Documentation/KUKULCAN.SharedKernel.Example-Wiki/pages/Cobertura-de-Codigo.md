# Cobertura de código

**Idioma:** [Inglés](Code-Coverage)

## Porcentaje medido actual

**Cobertura de líneas verificada: N/A. Cobertura de ramas verificada: N/A.** A fecha de 13 de octubre de 2026 no se ha observado un informe de cobertura satisfactorio del Example. Esto no equivale a informar de una cobertura del 0 %: el porcentaje no tiene sentido hasta que se compilen los ensamblados de producción, se ejecuten pruebas contra ellos y se genere un informe.

## Qué está configurado

El workflow [`coverage.yml`](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/blob/Develop/.github/workflows/coverage.yml):

- utiliza .NET SDK `10.0.x`;
- restaura y compila `KUKULCAN.SharedKernel.Example.slnx`;
- ejecuta `dotnet test` con el recolector XPlat Code Coverage;
- solicita salida de cobertura en formato Cobertura;
- limita la recogida a `[KUKULCAN.SharedKernel.Example.Web]*` y excluye por atributo el código generado por el compilador;
- usa ReportGenerator para crear informes HTML, Cobertura y resumen de texto;
- publica los informes como artefacto `example-code-coverage` durante 14 días.

El workflow está configurado para pushes a `main`, pull requests dirigidas a `main` y ejecución manual. No aparece en las ejecuciones recientes de Actions observadas; las últimas enumeradas fueron CI y limpieza de ramas.

## Por qué todavía no hay porcentaje

El archivo de solución actual contiene carpetas/elementos y no declara un proyecto Web API ni uno de pruebas. El log de restauración de la última ejecución satisfactoria de CI informa explícitamente `Unable to find a project to restore!`. Después, la compilación termina sin trabajo de compilación. No existen ensamblados de producción y prueba que Coverlet pueda medir, y el filtro configurado espera un ensamblado llamado `KUKULCAN.SharedKernel.Example.Web`.

Por tanto, el estado correcto es **todavía no medible**, no «la cobertura está limitada al X %» ni «el proyecto tiene cero cobertura». Configurar el recolector y los informes no produce por sí solo un resultado válido cuando no existen ensamblados de producción comprobables ni pruebas.

## Por qué la cobertura puede dejar de crecer más adelante

Cuando existan proyectos y pruebas reales, el porcentaje puede estabilizarse por motivos legítimos:

- parte del código es defensivo, específico de plataforma o solo se alcanza ante fallos externos concretos;
- puede haber mucha cobertura de líneas y seguir sin probarse ramas, combinaciones de autorización o comportamientos específicos de proveedores;
- el filtro actual puede dejar fuera código de integración o de interfaz;
- el código generado y las propiedades automáticas se excluyen intencionadamente;
- las herramientas miden ejecución, no si las aserciones son significativas.

Un estancamiento debe investigarse revisando líneas/ramas sin cubrir y riesgos reales. No deben añadirse pruebas artificiales solo para elevar la cifra. Añádanse cuando demuestren comportamiento de negocio, seguridad, persistencia, integración o prevención de regresiones. Debe acordarse y configurarse explícitamente cualquier umbral objetivo; el workflow actual no define ninguno.

## Proceso para publicar un informe fiable

1. Añadir los proyectos reales de API y pruebas al `.slnx`.
2. Confirmar que el proyecto de pruebas referencia el framework, runner/adaptador y `coverlet.collector` correctos.
3. Ejecutar localmente las pruebas con los mismos runsettings y configuración que CI.
4. Inspeccionar el XML de Cobertura y el resumen de ReportGenerator, verificando ensamblados no vacíos y líneas/ramas ejecutadas.
5. Comprobar que el filtro de inclusión coincide con el nombre real de los ensamblados de producción.
6. Ejecutar Code Coverage desde GitHub Actions y descargar `example-code-coverage`.
7. Registrar porcentajes de líneas y ramas junto con URL de ejecución, SHA del commit y fecha. La ausencia de informe nunca debe interpretarse como 0 %.

## Enlaces

- [Workflow de cobertura](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/blob/Develop/.github/workflows/coverage.yml)
- [Ejecuciones de GitHub Actions](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/actions)
- [Estrategia de pruebas](Estrategia-de-Pruebas)
