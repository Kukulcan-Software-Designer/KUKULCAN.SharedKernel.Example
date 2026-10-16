# Flujo de desarrollo

**Idioma:** [Inglés](Development-Workflow)

## Acuerdo de trabajo

El desarrollo debe estar dirigido por pruebas y centrado en el comportamiento. El flujo acordado actualmente para el Example es:

**TEST → RED → Source → GREEN → Coverage**

La instrucción actual del propietario es trabajar directamente en `Develop` para este repositorio, salvo que se cambie expresamente el acuerdo. No se debe dar por hecho que el flujo de ramas de funcionalidad de otros repositorios KUKULCAN se aplique aquí automáticamente.

## Definición de las fases

### 1. TEST

Seleccionar un criterio de aceptación de un Issue y añadir una prueba que exprese un comportamiento observable. Debe cubrir un requisito real, error, regla, contrato de integración o riesgo de regresión.

### 2. RED

Ejecutar la prueba y confirmar que falla por la ausencia del comportamiento esperado. Si el proceso acordado requiere publicar un RED en GitHub Actions antes de implementar, esperar a que ese resultado exista realmente e inspeccionar la causa. Un fallo de compilación no relacionado con el comportamiento solicitado no sirve como sustituto.

### 3. Source

Implementar el cambio mínimo de producción necesario para satisfacer el comportamiento. Reutilizar los contratos públicos de KUKULCAN y evitar duplicar infraestructura.

### 4. GREEN

Ejecutar las pruebas enfocadas pertinentes y después la suite de la solución. Confirmar que pasa el comportamiento nuevo y que el existente sigue siendo válido. Un workflow que no encuentra proyectos no demuestra un GREEN significativo de comportamiento.

### 5. Coverage

Generar un informe real de cobertura e inspeccionar líneas y ramas sin cubrir. Añadir pruebas porque demuestran comportamiento útil, no solo para alcanzar una cifra objetivo. Si la cobertura no mide los ensamblados de producción esperados, corregir la configuración antes de interpretar el porcentaje.

## Integración y documentación

Para trabajos de base de datos o servicios externos, registrar los requisitos del proveedor/servicio, comandos locales, variables de entorno y tratamiento esperado de errores. No confirmar secretos. Si se cambia un contrato público o una integración soportada, actualizar la documentación pertinente de la wiki y del repositorio.

## Criterios de finalización de un Issue

- Se satisfacen y pueden reproducirse los criterios de aceptación.
- Las pruebas pertinentes demuestran los casos correctos y los fallos requeridos.
- Restore/build/test procesan realmente los proyectos previstos.
- Las afirmaciones sobre proveedores y clientes tienen validación real correspondiente.
- La cobertura solo se registra si el informe existe, es válido y no está vacío.
- La documentación refleja el comportamiento entregado.

## Estado actual

El proceso está definido, pero todavía hay que incorporar los proyectos de código fuente y pruebas del Example. El workflow por sí solo no demuestra que exista el comportamiento de la aplicación.
