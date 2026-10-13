# Troubleshooting
**Language:** [Spanish](Resolucion-de-Problemas)


## `dotnet restore` succeeds but says no project was found

The current `KUKULCAN.SharedKernel.Example.slnx` has solution folders/items but no actual application or test project declarations. The observed CI log reports `Unable to find a project to restore!`. Add the planned project files to the solution before interpreting build/test status as meaningful.

## CI says GREEN but there are no test results

Check the restore, build, and test logs, not only the overall workflow badge. A green command against a solution with no projects does not verify implementation behavior. See [CI and Quality Gates](CI-and-Quality-Gates).

## The Code Coverage workflow does not show a percentage

Coverage is currently N/A because there are no production/test projects to measure and no verified coverage run/report. The workflow is configured to collect coverage from `[KUKULCAN.SharedKernel.Example.Web]*`; confirm that the eventual production assembly matches the filter and that test runs generated non-empty `coverage.cobertura.xml` files. See [Code Coverage](Code-Coverage).

## Reference Client workflow cannot find its project

The current workflow expects `Source/KUKULCAN.SharedKernel.Example.Web/KUKULCAN.SharedKernel.Example.Web.csproj`. This project is not yet present in the repository tree. Create the actual project at the agreed path or update the workflow when the layout is deliberately changed.

## How should database provider support be reported?

Do not infer support from an environment variable or a package being referenced. Require provider-backed tests against a real database instance, including migrations, constraints, queries, soft deletion, and history retention. Use test credentials and keep them outside source control.

## GitHub wiki publish fails because the wiki repository cannot be cloned

GitHub's wiki Git repository becomes clonable after an initial page exists. Open the [repository Wiki](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/wiki), create a first page named `Home`, and save a brief placeholder. Then rerun the provided publishing script; it will replace `Home.md` with the full contents and add the remaining pages. See [GitHub's official guide to adding/editing wiki pages](https://docs.github.com/en/communities/documenting-your-project-with-wikis/adding-or-editing-wiki-pages).

## Authentication/secret problems

Never paste temporary device codes, access tokens, passwords, or signing keys into Issues, wiki pages, or chat. Use `gh auth status` to inspect the local CLI session and `gh auth setup-git` to configure Git to use the existing CLI credentials. Re-authenticate through GitHub's device-flow page when required.
