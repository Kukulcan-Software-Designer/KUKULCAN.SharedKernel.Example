# Autenticación y seguridad

**Idioma:** [Inglés](Authentication-and-Security)

## Estado de seguridad

Las capacidades de autenticación y sesión deben basarse en la API pública real de `KUKULCAN.SharedKernel.Auth`. Los endpoints planificados no deben considerarse implementados solo por aparecer en un Issue.

## Comportamiento de autenticación previsto

El alcance actual requiere:

- Una pantalla de inicio de sesión como primera pantalla de cada una de las cuatro interfaces web.
- `POST /api/auth/login` y `GET /api/auth/me` si pueden implementarse con los contratos admitidos por Auth.
- Renovación, cierre o revocación de sesión solo cuando lo admita el diseño de sesión/token elegido.
- Inicialización idempotente de la cuenta inicial `Admin` (nombre visible `Administrador`) cuando no exista ningún administrador.
- Autorización en el servidor para las operaciones protegidas de la API.

Los modelos exactos de solicitud/respuesta, el método de verificación de contraseña, el comportamiento de token/sesión y los contratos de persistencia de usuario deben confirmarse antes de implementar.

## Requisitos del administrador inicial

- No incluir una contraseña inicial codificada en el código fuente ni guardarla en `appsettings`, documentación o scripts versionados.
- Inyectar las credenciales de arranque de forma segura mediante configuración local de entorno o un almacén de secretos aprobado.
- Crear el administrador de forma idempotente; los arranques repetidos no deben duplicar usuarios ni restablecer inesperadamente sus credenciales.
- Exigir la sustitución de cualquier credencial inicial exclusiva de desarrollo antes de utilizar producción.
- Evitar imprimir secretos, hashes de contraseña, tokens de acceso, claves de firma o cadenas de conexión en los registros.

## Seguridad del navegador

Razor Pages, Blazor, Angular y React tienen modelos de alojamiento distintos; la autenticación debe ser apropiada para cada uno. Evaluar sesiones seguras administradas en servidor/BFF y cookies `HttpOnly`, `Secure` y correctamente configuradas cuando proceda. Si se eligen tokens almacenados en el navegador, documentar el modelo de amenazas y las mitigaciones frente a XSS/CSRF. Nunca incluir claves de firma del servidor ni credenciales de base de datos en JavaScript entregado al cliente.

## Autorización y tenants

- Aplicar autorización en el servidor, no limitarse a ocultar controles de interfaz.
- Resolver la identidad del tenant a partir de un contexto de autenticación validado.
- No confiar únicamente en un `TenantId` proporcionado por el cliente.
- Usar `Guid.Empty` como tenant neutral solo si el contrato real de Auth admite expresamente esa interpretación.
- Evitar la exposición de información entre tenants y probar los límites de tenant si se habilita aislamiento en el Example.

## Seguridad de los datos

- Aplicar borrado lógico a los registros `Client` y `Provider`.
- Conservar `Invoice`, `ProdInvoice`, sus líneas y relaciones/valores relevantes para el histórico.
- Validar todos los identificadores y valores proporcionados por el usuario antes de las operaciones protegidas.
- Devolver errores externos genéricos y conservar en el servidor los detalles de diagnóstico seguros.
- Aplicar privilegios mínimos a las credenciales de base de datos y los permisos de GitHub Actions.

## Alertas de seguridad frente a revisión de seguridad

La autenticación, las sesiones de navegador, la gestión de secretos, los límites entre tenants y los permisos de workflows requieren revisión, pero su existencia no demuestra por sí misma una vulnerabilidad. Utilizar el canal privado indicado en [SECURITY.md](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/blob/Develop/SECURITY.md); no publicar detalles sensibles de explotación en Issues públicos.
