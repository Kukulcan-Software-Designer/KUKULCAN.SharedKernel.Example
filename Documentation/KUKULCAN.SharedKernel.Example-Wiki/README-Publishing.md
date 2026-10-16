# Example Wiki Publishing Package

This package contains the English and Spanish GitHub Wiki pages and a publishing script for `Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example`. Each of the 15 topics has two pages (30 topic pages), with a language-switch link at the top of every page. The package also includes a bilingual `_Sidebar.md` for GitHub Wiki navigation.

## Prerequisites

- Git installed.
- GitHub CLI (`gh`) installed and authenticated: `gh auth status`.
- Git configured to use the CLI credentials: `gh auth setup-git`.
- The repository Wiki enabled.
- At least one initial Wiki page created through GitHub's web interface.

## Publish

From this extracted folder, run:

```bash
chmod +x publish-example-wiki.sh
./publish-example-wiki.sh
```

The script clones the separate `.wiki.git` repository, copies the Markdown pages, commits, and pushes. It does not change the Example source repository or its `Develop` branch. Existing pages not included in this package are left untouched. Files with matching page names are updated.

If cloning fails because the wiki has never been initialized, create a first page named `Home` from the repository's **Wiki** tab in GitHub, save a placeholder, and rerun the script. The script does not print or request your password or personal access token.

## Accuracy note

The wiki intentionally distinguishes current repository state from planned implementation. In particular, the current solution has no application or test projects, the latest green CI run did not restore a project, and no real coverage percentage is yet available. Update these pages as implementation and verified Actions results change.

## Bilingual navigation

English pages provide a **Spanish** link at the top; Spanish pages provide an **Inglés** link at the top. Each link opens the matching page in the other language. The Home/Inicio pages contain separate navigation indexes in their respective languages. The global `_Sidebar.md` lists all topics under separate English and Español sections.

Publish the pages together. The script copies every Markdown file under `pages/`, including `_Sidebar.md`, preserving existing wiki pages not included in the package. GitHub Wiki uses `_Sidebar.md` as the shared sidebar for all pages.
