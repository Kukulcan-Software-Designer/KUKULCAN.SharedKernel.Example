# Authentication and Security
**Language:** [Spanish](Autenticacion-y-Seguridad)


## Security posture

Authentication and session capabilities must be based on the actual public API of `KUKULCAN.SharedKernel.Auth`. Planned endpoints must not be treated as implemented simply because they appear in an Issue.

## Intended authentication behavior

The current scope calls for:

- A login screen as the first screen in each of the four web interfaces.
- `POST /api/auth/login` and `GET /api/auth/me` if they can be implemented using the supported Auth contracts.
- Renewal, logout, or revocation behavior only where supported by the selected session/token design.
- Idempotent initialization of the initial `Admin` account (display name `Administrador`) when no administrator exists.
- Server-side authorization for protected API operations.

The exact request/response models, password verification method, token/session behavior, and user persistence contracts must be confirmed before implementation.

## Bootstrap requirements

- Never hard-code an initial password in source code or commit it in appsettings, documentation, or scripts.
- Inject bootstrap credentials securely from local environment configuration or an approved secret store.
- Create the administrator idempotently; repeated startup must not duplicate users or reset an existing administrator's credentials unexpectedly.
- Require replacement of any development-only initial credential before production use.
- Avoid printing secrets, password hashes, access tokens, signing keys, or connection strings to logs.

## Browser security

Razor Pages, Blazor, Angular, and React have different hosting models; authentication should be appropriate for each one. Evaluate secure server-managed sessions/BFF and HttpOnly, Secure, appropriately configured cookies where applicable. If browser-held tokens are selected, document the threat model and XSS/CSRF mitigations. Never bundle server-side signing keys or database credentials in client-delivered JavaScript.

## Authorization and tenants

- Enforce authorization on the server, not only by hiding UI controls.
- Resolve tenant identity from validated authentication context.
- Never trust a client-supplied `TenantId` by itself.
- Use `Guid.Empty` as a neutral tenant only if the actual Auth contract explicitly supports that interpretation.
- Prevent cross-tenant information disclosure and test tenant-boundary behavior if tenant isolation is enabled in the Example.

## Data security

- Soft-delete `Client` and `Provider` records.
- Preserve `Invoice`, `ProdInvoice`, their lines, and historically significant relationships/values.
- Validate all user-supplied identifiers and values before performing protected operations.
- Use generic external error messages while retaining safe diagnostic details server-side.
- Apply least privilege to database credentials and GitHub Actions permissions.

## Security alerts vs security review

Authentication, browser sessions, secret management, tenant boundaries, and workflow permissions require review, but their existence does not itself demonstrate a vulnerability. Use private vulnerability reporting as described in the repository's [SECURITY.md](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/blob/Develop/SECURITY.md); do not publish sensitive exploit details in public Issues.
