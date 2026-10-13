# Integraciones KUKULCAN

**Idioma:** [Inglés](KUKULCAN-Integrations)

## Principio de integración

Cada dependencia debe consumirse mediante su contrato público verificado y una versión documentada del paquete o servicio. El Example debe mostrar cómo se utiliza el ecosistema en conjunto, sin duplicar la implementación reutilizable de una biblioteca.

## `KUKULCAN.SharedKernel`

**Finalidad prevista:** demostrar tipos compartidos seleccionados y contratos de resultados o respuestas en un caso de uso significativo de la API.

**Validación requerida:** referenciar el paquete publicado, verificar la API pública de la versión fijada y probar tanto los casos correctos como los fallos relevantes. No copiar código fuente de la biblioteca ni acceder a tipos internos.

Issue: [#2](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/2).

## `KUKULCAN.SharedKernel.Auth`

**Finalidad prevista:** autenticación, comportamiento de acceso de usuario admitido y creación inicial del administrador.

**Validación requerida:** revisar los contratos reales de inicio de sesión, usuarios, verificación de contraseñas, roles/tenants y validación de credenciales; utilizar únicamente las capacidades expuestas por el paquete. No inventar renovación de tokens ni cierre de sesión si la dependencia no los soporta.

Issue: [#3](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/3).

## `KUKULCAN.SharedKernel.Database`

**Finalidad prevista:** persistencia mediante los contratos de base de datos/contexto/unidad de trabajo admitidos por el paquete, con selección de proveedor procedente de la configuración.

**Configuración:** `Kukulcan__Database__Provider` y `Kukulcan__Database__ConnectionString`.

**Validación requerida:** guardar y recuperar una entidad real de `Sample`; verificar migraciones, transacciones, restricciones, borrado lógico y comportamiento del proveedor. PostgreSQL, SQL Server y MySQL necesitan evidencias independientes de pruebas sobre una instancia del proveedor antes de afirmar que están soportados.

Issues: [#4](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/4), [#17](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/17), [#20](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/20), [#21](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/21) y [#22](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/22).

## `KUKULCAN.SharedKernel.JsonEngine`

**Finalidad prevista:** demostrar una capacidad real del motor JSON en la capa de aplicación, separada de la persistencia y de las reglas del dominio.

**Validación requerida:** inspeccionar la API publicada y seleccionar un caso de uso concreto y bien definido. Probar entradas válidas, situaciones de error relevantes y documentar el resultado.

Issue: [#5](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/5).

## `KUKULCAN.SharedKernel.I18N`

**Finalidad prevista:** consumir por HTTP la Web API independiente de I18N. No se debe reimplementar ese servicio dentro del Example.

**Validación requerida:** utilizar el contrato HTTP real, configurar externamente la URL base y las credenciales cuando sean necesarias, definir tiempos límite explícitos, gestionar de forma predecible el servicio no disponible y las respuestas inválidas, y no filtrar secretos ni diagnósticos internos.

Issue: [#6](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/6).

## Política de dependencias y versiones

- Fijar las versiones de paquetes mediante los archivos de proyecto y las convenciones de publicación del repositorio.
- Actualizar dependencias con una justificación y pruebas relevantes para el contrato modificado.
- Mantener separado el estado de publicación de paquetes del estado de las funcionalidades del Example.
- Informar de defectos al repositorio upstream correspondiente cuando el comportamiento pertenezca a esa biblioteca y no al Example.
