#!/usr/bin/env bash
set -Eeuo pipefail

readonly REPOSITORY="Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example"
readonly WIKI_URL="https://github.com/${REPOSITORY}.wiki.git"
readonly WIKI_HOME_URL="https://github.com/${REPOSITORY}/wiki"
readonly SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
readonly PAGES_DIR="${SCRIPT_DIR}/pages"

fail() { printf 'ERROR: %s\n' "$*" >&2; exit 1; }

command -v git >/dev/null 2>&1 || fail "Git is required."
command -v gh >/dev/null 2>&1 || fail "GitHub CLI (gh) is required. Install it first."
[[ -d "$PAGES_DIR" ]] || fail "Cannot find the pages directory: ${PAGES_DIR}"

if ! gh auth status --hostname github.com >/dev/null 2>&1; then
  fail "GitHub CLI is not authenticated. Run: gh auth login"
fi

# Reuse GitHub CLI authentication for HTTPS Git operations; no PAT is requested or displayed.
gh auth setup-git

wiki_enabled="$(gh api "repos/${REPOSITORY}" --jq '.has_wiki')"
[[ "$wiki_enabled" == "true" ]] || fail "The repository Wiki is disabled. Enable it in Settings, then rerun this script."

work_dir="$(mktemp -d -t kukulcan-example-wiki-XXXXXX)"
trap 'rm -rf "$work_dir"' EXIT
wiki_dir="${work_dir}/wiki"

printf 'Cloning wiki repository...\n'
if ! git clone "$WIKI_URL" "$wiki_dir"; then
  cat >&2 <<EOF

The Wiki repository could not be cloned. A GitHub Wiki normally needs one initial page
created through the web interface before its .wiki.git repository can be cloned.

1. Open: ${WIKI_HOME_URL}
2. Choose to create the first page; name it Home and save a short placeholder.
3. Rerun this script.

If a Home page already exists, check that your gh account has write access and that
git is using your GitHub CLI credentials. No repository source files were changed.
EOF
  exit 2
fi

# Preserve unknown wiki pages. Only pages shipped in this package are added/updated.
shopt -s nullglob
page_files=("${PAGES_DIR}"/*.md)
((${#page_files[@]} > 0)) || fail "No Markdown pages found in ${PAGES_DIR}."
for page in "${page_files[@]}"; do
  cp -- "$page" "$wiki_dir/$(basename -- "$page")"
done

# Set a local commit identity derived from the authenticated account if needed.
gh_login="$(gh api user --jq '.login')"
gh_id="$(gh api user --jq '.id')"
gh_name="$(gh api user --jq '.name // .login')"
git -C "$wiki_dir" config user.name "$gh_name"
git -C "$wiki_dir" config user.email "${gh_id}+${gh_login}@users.noreply.github.com"

# Add/update only packaged pages; other existing pages remain untouched.
for page in "${page_files[@]}"; do
  git -C "$wiki_dir" add -- "$(basename -- "$page")"
done

if git -C "$wiki_dir" diff --cached --quiet; then
  printf 'Wiki is already up to date. Nothing to commit.\n'
  printf 'Wiki: %s\n' "$WIKI_HOME_URL"
  exit 0
fi

git -C "$wiki_dir" commit -m "docs: publish complete Example project wiki"
printf 'Pushing wiki pages...\n'
git -C "$wiki_dir" push origin HEAD
printf '\nWiki published successfully: %s\n' "$WIKI_HOME_URL"
printf 'Published %d Markdown pages.\n' "${#page_files[@]}"
