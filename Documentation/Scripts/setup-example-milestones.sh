#!/usr/bin/env bash
set -Eeuo pipefail

# Creates/updates the six milestones for KUKULCAN.SharedKernel.Example,
# then assigns all 22 existing issues to their planned milestone.
# Requires GitHub CLI (gh) authenticated with Issues/Milestones write access.

REPO="Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example"

if ! command -v gh >/dev/null 2>&1; then
  echo "ERROR: GitHub CLI (gh) is not installed." >&2
  exit 1
fi
if ! gh auth status >/dev/null 2>&1; then
  echo "ERROR: GitHub CLI is not authenticated. Run: gh auth login" >&2
  exit 1
fi

upsert_milestone() {
  local title="$1"
  local description="$2"
  local due_date="$3"
  local due_on="${due_date}T12:00:00Z"
  local number=""

  number="$(gh api --paginate "repos/${REPO}/milestones?state=all&per_page=100" \
    --jq ".[] | select(.title == \"${title}\") | .number" | head -n 1 || true)"

  if [[ -n "$number" ]]; then
    gh api --method PATCH "repos/${REPO}/milestones/${number}" \
      -f "title=${title}" \
      -f "description=${description}" \
      -f "due_on=${due_on}" >/dev/null
    echo "Updated milestone #${number}: ${title} (due ${due_date})" >&2
  else
    number="$(gh api --method POST "repos/${REPO}/milestones" \
      -f "title=${title}" \
      -f "description=${description}" \
      -f "due_on=${due_on}" --jq '.number')"
    echo "Created milestone #${number}: ${title} (due ${due_date})" >&2
  fi
  printf '%s' "$number"
}

assign_issues() {
  local title="$1"
  local milestone_number="$2"
  local issue_numbers="$3"
  local issue_number

  for issue_number in $issue_numbers; do
    gh api --method PATCH "repos/${REPO}/issues/${issue_number}" \
      -F "milestone=${milestone_number}" --jq '.number' >/dev/null
    echo "Assigned issue #${issue_number} to '${title}' (#${milestone_number})."
  done
}

declare -A MILESTONE_IDS

MILESTONE_IDS["Foundation and Architecture"]="$(upsert_milestone \
  "Foundation and Architecture" \
  "Establish a buildable .NET 10 solution and define the architecture, project responsibilities, dependency direction, and integration boundaries. Completion means the .slnx contains the API and test projects, restore/build/test work, and the architecture is documented." \
  "2026-10-16")"

MILESTONE_IDS["KUKULCAN Integrations and Authentication"]="$(upsert_milestone \
  "KUKULCAN Integrations and Authentication" \
  "Integrate the verified public contracts of KUKULCAN.SharedKernel, KUKULCAN.SharedKernel.Auth, KUKULCAN.SharedKernel.Database, and KUKULCAN.SharedKernel.JsonEngine, and consume the separate KUKULCAN.SharedKernel.I18N Web API. Keep configuration and secrets externalized, implement the supported authentication/bootstrap behavior, and cover integrations and failure handling with behavior-focused tests." \
  "2026-10-30")"

MILESTONE_IDS["Sample Domain and Database Providers"]="$(upsert_milestone \
  "Sample Domain and Database Providers" \
  "Define and document the Sample domain entities, relationships, constraints, and repeatable database migrations. Validate persistence against PostgreSQL, Microsoft SQL Server, and MySQL using provider-backed tests. A provider must only be declared supported after its integration checks pass; database credentials must not be committed." \
  "2026-11-13")"

MILESTONE_IDS["Web API and Business Rules"]="$(upsert_milestone \
  "Web API and Business Rules" \
  "Implement the common Example Web API, required client/inventory/sales search filters, request validation, update rules, server-side authorization, soft deletion for Client and Provider, and retention of invoice and purchase history. Verify consistent behavior with integration tests and preserve domain and referential integrity." \
  "2026-11-27")"

MILESTONE_IDS["Four Web User Interfaces"]="$(upsert_milestone \
  "Four Web User Interfaces" \
  "Deliver independent Razor Pages, Blazor, Angular, and React web clients. Each interface starts with login, consumes the same Example Web API, supports the agreed core workflows and search filters, handles errors clearly, and never accesses the database directly or exposes server-side secrets. Include build and client behavior checks." \
  "2026-12-11")"

MILESTONE_IDS["Quality, CI, and Documentation"]="$(upsert_milestone \
  "Quality, CI, and Documentation" \
  "Consolidate behavior-focused tests, CI and coverage reporting, validation of implemented client/provider workflows, safe branch-cleanup automation, and repository documentation. Testing and CI must evolve throughout development; this milestone completes the cross-cutting acceptance checks and ensures documentation reflects implemented behavior rather than planned features." \
  "2026-12-18")"

assign_issues "Foundation and Architecture" "${MILESTONE_IDS["Foundation and Architecture"]}" "1 16"
assign_issues "KUKULCAN Integrations and Authentication" "${MILESTONE_IDS["KUKULCAN Integrations and Authentication"]}" "2 3 4 5 6"
assign_issues "Sample Domain and Database Providers" "${MILESTONE_IDS["Sample Domain and Database Providers"]}" "7 17 20 21 22"
assign_issues "Web API and Business Rules" "${MILESTONE_IDS["Web API and Business Rules"]}" "8 18 19"
assign_issues "Four Web User Interfaces" "${MILESTONE_IDS["Four Web User Interfaces"]}" "9 10 11 12"
assign_issues "Quality, CI, and Documentation" "${MILESTONE_IDS["Quality, CI, and Documentation"]}" "13 14 15"

echo
echo "All six milestones have been prepared and all 22 issues have been assigned."
echo "Review the milestone list at: https://github.com/${REPO}/milestones"
