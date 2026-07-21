#!/usr/bin/env bash
set -euo pipefail

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
actionlint_module=github.com/rhysd/actionlint/cmd/actionlint@v1.7.12

if ! command -v go >/dev/null 2>&1; then
  echo 'required tool is unavailable: go' >&2
  exit 1
fi

workflow_paths=()
while IFS= read -r -d '' workflow_path; do
  workflow_paths+=("$repo_root/$workflow_path")
done < <(
  git -C "$repo_root" ls-files -z -- \
    '.github/workflows/*.yml' '.github/workflows/*.yaml'
)

if (( ${#workflow_paths[@]} == 0 )); then
  echo 'no committed workflow YAML files were found' >&2
  exit 1
fi

go run "$actionlint_module" -shellcheck= -pyflakes= "${workflow_paths[@]}"
