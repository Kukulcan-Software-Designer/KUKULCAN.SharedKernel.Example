# Web Interfaces
**Language:** [Spanish](Interfaces-Web)


## Shared backend

The four interfaces are distinct clients of one Example Web API. The API owns application validation, authorization, domain behavior, persistence, and external-service access. No web interface may connect directly to the database.

## Required interfaces

| Interface | Expected responsibilities | Current status |
|---|---|---|
| Razor Pages | Server-rendered web screens, starting with login; core Client, Provider, Product, Warehouse/stock, sales and purchase workflows | Planned; project not present |
| Blazor | Equivalent workflows using a documented Blazor hosting model | Planned; hosting model not selected |
| Angular | Browser client, login, API consumption, validation/error handling | Planned; package manifest/build not present |
| React | Browser client, login, API consumption, validation/error handling | Planned; package manifest/build not present |

## Consistent behavior

All clients should use the same API request/response contracts, authentication rules, search semantics, data-validation behavior, and error meanings. User interfaces may differ in presentation but must not independently implement business rules.

Required search experience includes:

- Clients: partial name, city, state/region, and postal code filters, combinable as appropriate.
- Warehouse stock: product code and partial product-name filters.
- Sales: client and date filters.

## Authentication

Login must be the first screen each time a user enters an unauthenticated interface. Session handling must match the hosting model. Protected operations must be enforced by the API even when the UI hides or disables controls.

## Client build and CI requirements

- Each client must have reproducible setup, production-build, and test commands.
- Node.js and package-manager versions must be pinned/documented for Angular and React.
- The reference-client workflow currently points to `Source/KUKULCAN.SharedKernel.Example.Web/KUKULCAN.SharedKernel.Example.Web.csproj`, but that project is not present yet. The workflow cannot provide meaningful client validation until the expected project exists.
- Add separate front-end CI jobs when Angular and React projects are created; do not imply the current .NET-only client workflow validates those browser bundles.
- Check produced client bundles for server-side secrets and validate expired/invalid session handling.

Issues: [#9](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/9), [#10](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/10), [#11](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/11), [#12](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/12).
