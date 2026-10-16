# Arquitectura

**Idioma:** [Inglés](Architecture)

> La descripción detallada de la arquitectura, la estructura propuesta de proyectos, las reglas de dependencias, las decisiones de autenticación/sesión, los límites de seguridad y la estrategia de verificación se mantienen en el [documento de arquitectura de la aplicación Example](../../Architecture/ARCHITECTURE.md). Describe el diseño objetivo; no implica que los proyectos ejecutables ya existan.

## Objetivo arquitectónico

El Example debe ilustrar la integración y el comportamiento de una aplicación sin reimplementar capacidades que pertenecen a las bibliotecas KUKULCAN. La arquitectura descrita aquí es la **arquitectura objetivo**; no implica que todas sus capas existan ya en el código fuente.

## Componentes lógicos

```text
Razor Pages ─┐
Blazor ───────┤
Angular ──────┼── HTTP/HTTPS ──> Web API del Example
React ───────┘                    │
                                   ├── Casos de uso y validación
                                   ├── Modelo de dominio y reglas de negocio
                                   ├── Contratos de KUKULCAN.SharedKernel
                                   ├── KUKULCAN.SharedKernel.Auth
                                   ├── KUKULCAN.SharedKernel.Database ──> proveedor configurado
                                   ├── KUKULCAN.SharedKernel.JsonEngine
                                   └── Cliente HTTP ──> API KUKULCAN.SharedKernel.I18N
```

## Límites de responsabilidad

### Interfaces web

Razor Pages, Blazor, Angular y React presentan información y recogen las entradas del usuario. Se autentican mediante el modelo de API/sesión acordado y consumen contratos HTTP comunes. No deben conectarse directamente a la base de datos, duplicar reglas de negocio ni contener credenciales de base de datos o secretos de firma.

### Web API del Example

La API es el límite único para los casos de uso de la aplicación. Valida solicitudes, aplica autorización, coordina el comportamiento, invoca la persistencia mediante las abstracciones del paquete Database y devuelve códigos HTTP y contratos de error predecibles.

### Aplicación y dominio

Las reglas de negocio deben poder probarse sin necesitar un navegador. Las reglas de ciclo de vida de las entidades, el borrado lógico, la conservación del histórico comercial, las relaciones entre productos y almacenes y la semántica de las consultas pertenecen al servidor y deben reforzarse con restricciones de base de datos cuando corresponda.

### Persistencia

El Example debe utilizar la API pública de `KUKULCAN.SharedKernel.Database`, incluidas las abstracciones `KukulcanDbContextBase` e `IUnitOfWork` cuando encajen con el contrato real del paquete. La configuración específica del proveedor debe ser explícita. No se debe copiar en este repositorio la implementación del paquete compartido.

### Servicios externos y bibliotecas

`KUKULCAN.SharedKernel.I18N` es un servicio HTTP independiente; el Example no debe duplicar su implementación. Los demás paquetes NuGet KUKULCAN deben consumirse mediante sus APIs públicas publicadas y versiones documentadas.

## Dirección de las dependencias

- Las interfaces dependen de contratos HTTP, no de infraestructura de acceso a datos.
- La lógica de aplicación depende de abstracciones y contratos verificados, no de frameworks de interfaz.
- Los detalles de persistencia se seleccionan y configuran en el servidor.
- Los servicios externos se invocan mediante clientes HTTP configurados, con límites de tiempo y gestión explícita de fallos.
- Cuando sea posible, las pruebas sustituyen los límites externos; los comportamientos dependientes del motor de base de datos se validan también con pruebas de integración reales.

## Decisiones arquitectónicas

El documento detallado establece la dirección inicial para alojamiento/sesiones y la estructura propuesta de proyectos. Las firmas exactas de los paquetes, los DTO, las convenciones de endpoints y el comportamiento específico de cada proveedor deben confirmarse con los contratos públicos reales y probarse durante los Issues de implementación.

## Validación arquitectónica

La arquitectura solo se considera aceptada cuando la solución compila, los límites de dependencias son visibles en el código, las integraciones utilizan contratos públicos verificados y las pruebas automatizadas demuestran el comportamiento documentado. Una solución vacía o un workflow verde que no restaura ningún proyecto no constituye una arquitectura completada.
