#!/usr/bin/env bash
set -euo pipefail

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)
verifier="$repo_root/tools/publication/verify-workflows.sh"
csharp_ci="$repo_root/.github/workflows/csharp-ci.yml"

if [[ ! -f "$verifier" ]]; then
  echo "verify-workflows verifier is missing: $verifier" >&2
  exit 1
fi

if rg -q 'ACTION_PIN_VERIFY_TARGETS|WORKFLOW_VERIFY_TARGETS' "$csharp_ci"; then
  echo "C# CI must verify the complete tracked workflow set" >&2
  exit 1
fi

fixture=$(mktemp -d)
trap 'rm -rf "$fixture"' EXIT
mkdir -p "$fixture/.github/workflows"

git -C "$fixture" init -q
git -C "$fixture" config user.email fixture@example.com
git -C "$fixture" config user.name Fixture
git -C "$fixture" config commit.gpgSign false

sha=9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0

write_clean_workflow() {
  local path=${1:-"$fixture/.github/workflows/verify.yml"}
  cat > "$path" <<WORKFLOW
name: Verify
on: pull_request
permissions:
  contents: read
jobs:
  verify:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@$sha
      - name: Restore from the public dependency source
        run: dotnet restore --source https://api.nuget.org/v3/index.json
      - name: Upload coverage only
        uses: actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a
        with:
          path: TestResults/**/coverage.cobertura.xml
WORKFLOW
}

run_expect_fail() {
  if WORKFLOW_VERIFY_REPO_ROOT="$fixture" \
    WORKFLOW_VERIFY_TARGETS="${1-verify.yml}" \
    bash "$verifier" >/dev/null 2>&1; then
    echo "expected workflow verifier to fail${2:+: $2}" >&2
    exit 1
  fi
}

run_expect_pass() {
  WORKFLOW_VERIFY_REPO_ROOT="$fixture" \
    WORKFLOW_VERIFY_TARGETS="${1-verify.yml}" \
    bash "$verifier" >/dev/null
}

write_clean_workflow
git -C "$fixture" add .github/workflows/verify.yml
git -C "$fixture" commit -qm fixture
run_expect_pass verify.yml

cat > "$fixture/.github/workflows/verify.yml" <<WORKFLOW
name: Verify
permissions:
  packages: read
jobs:
  verify:
    steps:
      - uses: actions/checkout@$sha
WORKFLOW
run_expect_fail verify.yml "packages permission"

cat > "$fixture/.github/workflows/verify.yml" <<WORKFLOW
name: Verify
permissions:
  "packages": "write"
jobs:
  verify:
    steps:
      - uses: actions/checkout@$sha
WORKFLOW
run_expect_fail verify.yml "quoted packages permission"

cat > "$fixture/.github/workflows/verify.yml" <<WORKFLOW
name: Verify
permissions: read-all
jobs:
  verify:
    steps:
      - uses: actions/checkout@$sha
WORKFLOW
run_expect_fail verify.yml "read-all permission scalar"

cat > "$fixture/.github/workflows/verify.yml" <<WORKFLOW
name: Verify
permissions: write-all
jobs:
  verify:
    steps:
      - uses: actions/checkout@$sha
WORKFLOW
run_expect_fail verify.yml "write-all permission scalar"

cat > "$fixture/.github/workflows/verify.yml" <<WORKFLOW
name: Verify
permissions:
  contents: read
  actions: read
jobs:
  verify:
    steps:
      - uses: actions/checkout@$sha
WORKFLOW
run_expect_fail verify.yml "extra workflow permission"

cat > "$fixture/.github/workflows/verify.yml" <<WORKFLOW
name: Verify
permissions:
  contents: read
jobs:
  verify:
    permissions:
      contents: write
    steps:
      - uses: actions/checkout@$sha
WORKFLOW
run_expect_fail verify.yml "job-level permission escalation"

cat > "$fixture/.github/workflows/verify.yml" <<WORKFLOW
name: Verify
jobs:
  verify:
    steps:
      - uses: actions/checkout@$sha
WORKFLOW
run_expect_fail verify.yml "missing workflow permission boundary"

cat > "$fixture/.github/workflows/verify.yml" <<WORKFLOW
name: Verify
permissions:
  contents: read
env:
  GITHUB_TOKEN: >-
    \${{ secrets.GITHUB_TOKEN }}
jobs:
  verify:
    steps:
      - uses: actions/checkout@$sha
WORKFLOW
run_expect_fail verify.yml "multiline secret interpolation"

cat > "$fixture/.github/workflows/verify.yml" <<'WORKFLOW'
name: Verify
permissions:
  contents: read
env:
  TOKEN: ${{ github['token'] }}
jobs: {}
WORKFLOW
run_expect_fail verify.yml "single-quoted github token bracket expression"

cat > "$fixture/.github/workflows/verify.yml" <<'WORKFLOW'
name: Verify
permissions:
  contents: read
env:
  TOKEN: ${{ github["token"] }}
jobs: {}
WORKFLOW
run_expect_fail verify.yml "double-quoted github token bracket expression"

cat > "$fixture/.github/workflows/verify.yml" <<'WORKFLOW'
name: Verify
permissions:
  contents: read
jobs:
  verify:
    steps:
      - run: |
          dotnet nuget update source github \
            --password "$GITHUB_TOKEN"
          curl -H "Authorization: Bearer $GITHUB_TOKEN" \
            https://nuget.pkg.github.com/KyuzanInc/index.json
WORKFLOW
run_expect_fail verify.yml "package credential argument and probe"

cat > "$fixture/.github/workflows/verify.yml" <<'WORKFLOW'
name: Verify
permissions:
  contents: read
jobs:
  verify:
    steps:
      # A comment cannot hide the active mutable reference below.
      - uses: "actions/checkout@v7" # mutable
WORKFLOW
run_expect_fail verify.yml "mutable Action reference"

cat > "$fixture/.github/workflows/verify.yml" <<'WORKFLOW'
name: Verify
permissions:
  contents: read
jobs:
  verify:
    steps:
      - uses: actions/checkout@not-a-sha
WORKFLOW
run_expect_fail verify.yml "malformed Action reference"

cat > "$fixture/.github/workflows/verify.yml" <<'WORKFLOW'
name: Verify
permissions:
  contents: read
jobs:
  verify:
    steps:
      - uses: ./../outside
WORKFLOW
run_expect_fail verify.yml "local Action parent traversal"

for unsafe_path in \
  '*.nupkg' \
  '*.snupkg' \
  '.artifacts/' \
  'local-feed/' \
  '${{ env.ARTIFACT_PATH }}'; do
  cat > "$fixture/.github/workflows/verify.yml" <<WORKFLOW
name: Verify
permissions:
  contents: read
jobs:
  verify:
    steps:
      - uses: actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a
        with:
          path: >-
            $unsafe_path
WORKFLOW
  run_expect_fail verify.yml "unsafe artifact path $unsafe_path"
done

cat > "$fixture/.github/workflows/verify.yml" <<'WORKFLOW'
name: Verify
permissions:
  contents: read
jobs:
  verify:
    steps:
      - uses: Actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a
        with:
          path: '*.nupkg'
WORKFLOW
run_expect_fail verify.yml "case-varied upload-artifact identity"

cat > "$fixture/.github/workflows/verify.yml" <<'WORKFLOW'
name: Verify
permissions:
  contents: read
jobs:
  verify:
    steps:
      - run: >
          dotnet nuget push package.nupkg
          --source https://api.nuget.org/v3/index.json
WORKFLOW
run_expect_fail verify.yml "nuget.org publication"

cat > "$fixture/.github/workflows/verify.yml" <<'WORKFLOW'
name: Verify
permissions:
  contents: read
jobs:
  verify:
    steps:
      - run: dotnet nuget "push" package.nupkg
WORKFLOW
run_expect_fail verify.yml "quoted shell push token"

cat > "$fixture/.github/workflows/verify.yml" <<'WORKFLOW'
name: Verify
permissions:
  contents: read
jobs:
  verify:
    steps:
      - run: dotnet nuget p"u"sh package.nupkg
WORKFLOW
run_expect_fail verify.yml "mixed quoted and unquoted shell push token"

cat > "$fixture/.github/workflows/verify.yml" <<'WORKFLOW'
name: Verify
permissions:
  contents: read
jobs:
  verify:
    steps:
      - run: dotnet nuget p\ush package.nupkg
WORKFLOW
run_expect_fail verify.yml "backslash-escaped shell push token"

cat > "$fixture/.github/workflows/verify.yml" <<'WORKFLOW'
name: Verify
permissions:
  contents: read
jobs:
  verify:
    steps:
      - run: |
          dotnet nuget \
            push package.nupkg
WORKFLOW
run_expect_fail verify.yml "backslash-newline shell push"

cat > "$fixture/.github/workflows/verify.yml" <<'WORKFLOW'
name: Verify
permissions:
  contents: read
jobs:
  verify:
    steps:
      - run: |
          dotnet nuget update \
          source github --pass\
          word literal
WORKFLOW
run_expect_fail verify.yml "backslash-newline credential token fragments"

cat > "$fixture/.github/workflows/verify.yml" <<'WORKFLOW'
name: Verify
permissions:
  contents: read
jobs:
  verify:
    steps:
      - run: |
          credential-helper --pass\
          word literal
WORKFLOW
run_expect_fail verify.yml "focused backslash-newline password token"

cat > "$fixture/.github/workflows/verify.yml" <<'WORKFLOW'
name: Verify
permissions:
  contents: read
jobs:
  verify:
    steps:
      - run: |2
          dotnet nuget push package.nupkg
WORKFLOW
run_expect_fail verify.yml "unsupported explicit-indent block scalar"

cat > "$fixture/.github/workflows/verify.yml" <<'WORKFLOW'
name: Verify
permissions:
  contents: read
jobs:
  verify:
    steps:
      - run: >2-
          dotnet nuget push package.nupkg
WORKFLOW
run_expect_fail verify.yml "unsupported reordered block scalar indicators"

cat > "$fixture/.github/workflows/verify.yml" <<'WORKFLOW'
name: Verify
permissions:
  contents: read
jobs:
  verify:
    steps:
      - run: "dotnet nuget \u0070ush package.nupkg"
WORKFLOW
run_expect_fail verify.yml "double-quoted YAML unicode escape"

cat > "$fixture/.github/workflows/verify.yml" <<'WORKFLOW'
name: Verify
permissions:
  contents: read
jobs:
  verify:
    steps:
      - run: "dotnet nuget \U00000070ush package.nupkg"
WORKFLOW
run_expect_fail verify.yml "double-quoted YAML long unicode escape"

cat > "$fixture/.github/workflows/verify.yml" <<'WORKFLOW'
name: Verify
permissions:
  contents: read
jobs:
  verify:
    steps:
      - run: 'dotnet nuget p''u''sh package.nupkg'
WORKFLOW
run_expect_fail verify.yml "single-quoted YAML doubling"

cat > "$fixture/.github/workflows/verify.yml" <<'WORKFLOW'
name: Verify
permissions:
  contents: read
jobs:
  verify:
    steps:
      - run: "dotnet restore \q"
WORKFLOW
run_expect_fail verify.yml "unsupported double-quoted YAML escape"

cat > "$fixture/.github/workflows/verify.yml" <<'WORKFLOW'
name: Verify
permissions:
  contents: read
x-publish: &publish |
  dotnet nuget push package.nupkg
jobs:
  verify:
    steps:
      - run: *publish
WORKFLOW
run_expect_fail verify.yml "run alias resolving to forbidden publication"

cat > "$fixture/.github/workflows/verify.yml" <<'WORKFLOW'
name: Verify
permissions:
  contents: read
jobs:
  verify:
    steps:
      - run: &publish dotnet nuget push package.nupkg
WORKFLOW
run_expect_fail verify.yml "anchored run scalar"

cat > "$fixture/.github/workflows/verify.yml" <<'WORKFLOW'
name: Verify
permissions:
  contents: read
jobs:
  verify:
    steps:
      - run: !publish dotnet nuget push package.nupkg
WORKFLOW
run_expect_fail verify.yml "tagged run scalar"

cat > "$fixture/.github/workflows/verify.yml" <<'WORKFLOW'
name: Verify
permissions:
  contents: read
jobs:
  verify:
    steps:
      - run: dotnet nuget
          push package.nupkg
WORKFLOW
run_expect_fail verify.yml "multiline plain run scalar continuation"

cat > "$fixture/.github/workflows/verify.yml" <<WORKFLOW
name: Verify
permissions:
  contents: read
jobs:
  verify:
    steps:
      - run: dotnet restore --source https://api.nuget.org/v3/index.json
        shell: bash
        env:
          MODE: public
        with:
          fixture: sibling
      - uses: actions/checkout@$sha
WORKFLOW
run_expect_pass verify.yml

cat > "$fixture/.github/workflows/verify.yml" <<'WORKFLOW'
name: Verify
permissions:
  contents: read
jobs:
  verify:
    "\u0070ermissions":
      packages: write
    steps: []
WORKFLOW
run_expect_fail verify.yml "escaped job-level permissions key"

cat > "$fixture/.github/workflows/verify.yml" <<'WORKFLOW'
name: Verify
permissions:
  contents: read
jobs:
  verify:
    steps:
      - "\u0072un": dotnet nuget push package.nupkg
WORKFLOW
run_expect_fail verify.yml "escaped run key"

cat > "$fixture/.github/workflows/verify.yml" <<'WORKFLOW'
name: Verify
permissions:
  contents: read
jobs:
  verify:
    steps:
      - "\u0075ses": actions/upload-artifact@v7
WORKFLOW
run_expect_fail verify.yml "escaped mutable uses key"

cat > "$fixture/.github/workflows/verify.yml" <<WORKFLOW
name: Verify
"permissions":
  'contents': 'read'
jobs:
  verify:
    steps:
      - "run": dotnet restore --source https://api.nuget.org/v3/index.json
      - 'uses': actions/checkout@$sha
WORKFLOW
run_expect_pass verify.yml

write_clean_workflow
run_expect_pass verify.yml

cat > "$fixture/.github/workflows/second.yml" <<WORKFLOW
name: Second
permissions:
  contents: read
jobs:
  verify:
    steps:
      - uses: actions/checkout@$sha
WORKFLOW
git -C "$fixture" add .github/workflows/verify.yml .github/workflows/second.yml
WORKFLOW_VERIFY_REPO_ROOT="$fixture" WORKFLOW_VERIFY_TARGETS= \
  bash "$verifier" >/dev/null

cat > "$fixture/.github/workflows/second.yml" <<'WORKFLOW'
name: Second
permissions:
  packages: read
jobs: {}
WORKFLOW
run_expect_fail "" "default target set includes every tracked .yml workflow"
run_expect_fail $'verify.yml\nsecond.yml' "newline-delimited bad target was ignored"

write_clean_workflow "$fixture/.github/workflows/second.yml"
git -C "$fixture" add .github/workflows/second.yml

cat > "$fixture/.github/workflows/bypass.yaml" <<'WORKFLOW'
name: Bypass
permissions:
  packages: write
jobs: {}
WORKFLOW
git -C "$fixture" add .github/workflows/bypass.yaml
if WORKFLOW_VERIFY_REPO_ROOT="$fixture" WORKFLOW_VERIFY_TARGETS= \
  bash "$verifier" >/dev/null 2>&1; then
  echo "default target set ignored a tracked .yaml workflow" >&2
  exit 1
fi
git -C "$fixture" rm --cached -q .github/workflows/bypass.yaml
rm "$fixture/.github/workflows/bypass.yaml"

cat > "$fixture/.github/workflows/untracked.yml" <<WORKFLOW
name: Untracked
jobs:
  verify:
    steps:
      - uses: actions/checkout@$sha
WORKFLOW

for invalid_targets in \
  '/''tmp/verify.yml' \
  '../verify.yml' \
  'nested/verify.yml' \
  'verify.yaml' \
  'missing.yml' \
  'untracked.yml' \
  'verify.yml verify.yml'; do
  run_expect_fail "$invalid_targets" "invalid focused target: $invalid_targets"
done

run_expect_pass "verify.yml second.yml"

extract_workflow_run_from() {
  local workflow_source=$1
  local step_id=$2
  local destination=$3
  python3 - "$workflow_source" "$step_id" "$destination" <<'PY'
import pathlib
import sys

workflow = pathlib.Path(sys.argv[1]).read_text(encoding="utf-8").splitlines()
step_id = sys.argv[2]
destination = pathlib.Path(sys.argv[3])
id_line = f"        id: {step_id}"
matches = [index for index, line in enumerate(workflow) if line == id_line]
if len(matches) != 1:
    raise SystemExit(f"expected one workflow step id {step_id!r}, found {len(matches)}")

start = matches[0]
end = len(workflow)
for index in range(start + 1, len(workflow)):
    if workflow[index].startswith("      - "):
        end = index
        break

run_headers = [
    index for index in range(start, end)
    if workflow[index] == "        run: |"
]
if len(run_headers) != 1:
    raise SystemExit(f"step {step_id!r} must have one literal run block")
run_start = run_headers[0] + 1
body = []
for line in workflow[run_start:end]:
    if line and not line.startswith("          "):
        raise SystemExit(f"step {step_id!r} has an invalid run indentation")
    body.append(line[10:] if line else "")
destination.write_text("\n".join(body) + "\n", encoding="utf-8")
PY
}

extract_workflow_inline_run_from() {
  local workflow_source=$1
  local step_id=$2
  local destination=$3
  python3 - "$workflow_source" "$step_id" "$destination" <<'PY'
import pathlib
import sys

workflow = pathlib.Path(sys.argv[1]).read_text(encoding="utf-8").splitlines()
step_id = sys.argv[2]
destination = pathlib.Path(sys.argv[3])
id_line = f"        id: {step_id}"
matches = [index for index, line in enumerate(workflow) if line == id_line]
if len(matches) != 1:
    raise SystemExit(f"expected one workflow step id {step_id!r}, found {len(matches)}")

start = matches[0]
end = len(workflow)
for index in range(start + 1, len(workflow)):
    if workflow[index].startswith("      - "):
        end = index
        break
run_lines = [line for line in workflow[start:end] if line.startswith("        run: ")]
if len(run_lines) != 1 or run_lines[0] == "        run: |":
    raise SystemExit(f"step {step_id!r} must have one inline run command")
destination.write_text(run_lines[0][13:] + "\n", encoding="utf-8")
PY
}

consumer_source="$repo_root/.github/workflows/consumer-smoke.yml"
if [[ ! -f "$consumer_source" ]]; then
  echo "consumer workflow is missing: $consumer_source" >&2
  exit 1
fi
if rg -Fq '${{ secrets.GITHUB_TOKEN }}' "$consumer_source"; then
  echo "consumer workflow must not use GITHUB_TOKEN without packages permission" >&2
  exit 1
fi
if rg -q '^[[:space:]]+packages:[[:space:]]' "$consumer_source"; then
  echo "consumer workflow permissions must not request packages access" >&2
  exit 1
fi

reset_consumer_fixture() {
  cp "$consumer_source" "$fixture/.github/workflows/consumer-smoke.yml"
  git -C "$fixture" add .github/workflows/consumer-smoke.yml
}

replace_consumer_once() {
  local old=$1
  local new=$2
  python3 - "$fixture/.github/workflows/consumer-smoke.yml" "$old" "$new" <<'PY'
import pathlib
import sys

path = pathlib.Path(sys.argv[1])
old = sys.argv[2]
new = sys.argv[3]
text = path.read_text(encoding="utf-8")
if text.count(old) != 1:
    raise SystemExit(
        f"consumer fixture mutation expected one match, found {text.count(old)}: {old!r}"
    )
path.write_text(text.replace(old, new), encoding="utf-8")
PY
}

insert_consumer_once() {
  local marker=$1
  local insertion=$2
  replace_consumer_once "$marker" "$marker$insertion"
}

reset_consumer_fixture
run_expect_pass consumer-smoke.yml

reset_consumer_fixture
replace_consumer_once $'jobs:\n  consumer:' \
  $'jobs:
  shadow:
    runs-on: ubuntu-latest
    steps:
      - name: Arbitrary executable step
        run: printf \'shadow ran\
\'
  consumer:'
run_expect_fail consumer-smoke.yml "extra executable consumer job"

reset_consumer_fixture
insert_consumer_once $'    steps:\n' \
  $'      - name: Unknown consumer step
        id: unknown-consumer-step
        run: printf \'unknown step ran\
\'
'
run_expect_fail consumer-smoke.yml "unknown consumer step"

reset_consumer_fixture
replace_consumer_once '    workflows: [Release]' '    workflows: [Other]'
run_expect_fail consumer-smoke.yml "workflow_run source other than Release"

reset_consumer_fixture
replace_consumer_once '    types: [completed]' '    types: [requested]'
run_expect_fail consumer-smoke.yml "workflow_run event other than completed"

reset_consumer_fixture
insert_consumer_once $'  workflow_dispatch:\n' $'  push:\n'
run_expect_fail consumer-smoke.yml "extra consumer trigger"

reset_consumer_fixture
replace_consumer_once '        default: 1.0.0' '        default: 1.0.1'
run_expect_fail consumer-smoke.yml "manual version other than exact 1.0.0"

reset_consumer_fixture
replace_consumer_once '  contents: read' $'  contents: read\n  packages: read'
run_expect_fail consumer-smoke.yml "consumer package permission"

reset_consumer_fixture
replace_consumer_once '    environment: github-packages-read' \
  '    environment: unprotected'
run_expect_fail consumer-smoke.yml "missing protected read environment"

reset_consumer_fixture
replace_consumer_once \
  "github.event.workflow_run.conclusion == 'success'" \
  "github.event.workflow_run.conclusion != 'success'"
run_expect_fail consumer-smoke.yml "non-successful workflow_run guard"

reset_consumer_fixture
replace_consumer_once \
  '          visibility="$(gh api "repos/$REPOSITORY" --jq '\''.visibility'\'')"' \
  '          visibility=private'
run_expect_fail consumer-smoke.yml "missing live visibility query"

reset_consumer_fixture
insert_consumer_once \
  $'        id: configure-dedicated\n        shell: bash\n' \
  $'        env:\n          UNCONDITIONAL_TOKEN: ${{ secrets.GITHUB_TOKEN }}\n'
run_expect_fail consumer-smoke.yml "GITHUB_TOKEN in contents-only consumer"

reset_consumer_fixture
replace_consumer_once \
  '<PackageReference Include="KyuzanInc.Peak.Sdk" Version="[1.0.0]" />' \
  '<PackageReference Include="KyuzanInc.Peak.Sdk" Version="1.*" />'
run_expect_fail consumer-smoke.yml "floating Peak consumer version"

reset_consumer_fixture
replace_consumer_once \
  '${{ runner.temp }}/peak-consumer/PeakSdkConsumer' \
  '/''tmp/consumer/PeakSdkConsumer'
run_expect_fail consumer-smoke.yml "ambient tmp consumer root"

reset_consumer_fixture
replace_consumer_once \
  '${{ runner.temp }}/peak-consumer/.nuget/packages' \
  '~''/.nuget/packages'
run_expect_fail consumer-smoke.yml "ambient NuGet package cache"

reset_consumer_fixture
replace_consumer_once \
  '([.libraries | keys[] | select((split("/")[0] | ascii_downcase) == "kyuzaninc.turnkey.sdk")] | length) == 1' \
  'true'
run_expect_fail consumer-smoke.yml "missing exact Turnkey identity assertion"

reset_consumer_fixture
replace_consumer_once \
  '.source == "https://nuget.pkg.github.com/KyuzanInc/index.json"' \
  '.source != null'
run_expect_fail consumer-smoke.yml "loose Peak package source assertion"

reset_consumer_fixture
replace_consumer_once \
  $'        id: cleanup-config\n        if: always()' \
  $'        id: cleanup-config'
run_expect_fail consumer-smoke.yml "credential cleanup without always guard"

reset_consumer_fixture
replace_consumer_once ' -c Release --no-restore' ' -c Release'
run_expect_fail consumer-smoke.yml "build may implicitly restore"

reset_consumer_fixture
replace_consumer_once ' -c Release --no-build --no-restore' ' -c Release --no-build'
run_expect_fail consumer-smoke.yml "run may implicitly restore"

reset_consumer_fixture
insert_consumer_once $'          set -euo pipefail\n          assets=' \
  $'          dotnet nuget push unexpected.nupkg\n'
run_expect_fail consumer-smoke.yml "consumer package publication"

reset_consumer_fixture
replace_consumer_once \
  'actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0' \
  'actions/checkout@v7'
run_expect_fail consumer-smoke.yml "mutable consumer Action"

reset_consumer_fixture
replace_consumer_once \
  $'        id: prepare-consumer\n        shell: bash\n        run: |\n          set -euo pipefail\n' \
  $'        id: prepare-consumer
        shell: bash
        run: |
          set -euo pipefail
          printf \'arbitrary mutation\
\'
'
run_expect_fail consumer-smoke.yml "unreviewed consumer run-body mutation"

consumer_test_dir="$fixture/consumer-script-tests"
consumer_stub_bin="$consumer_test_dir/bin"
mkdir -p "$consumer_stub_bin"
consumer_preflight_script="$consumer_test_dir/preflight.sh"
consumer_credential_check_script="$consumer_test_dir/dedicated-credential-check.sh"
consumer_prepare_script="$consumer_test_dir/prepare-consumer.sh"
consumer_config_script="$consumer_test_dir/configure-dedicated.sh"
consumer_restore_script="$consumer_test_dir/restore.sh"
consumer_assert_script="$consumer_test_dir/assert-package-graph.sh"
consumer_cleanup_script="$consumer_test_dir/cleanup-config.sh"
consumer_build_script="$consumer_test_dir/build.sh"
consumer_run_script="$consumer_test_dir/run.sh"

extract_workflow_run_from "$consumer_source" preflight "$consumer_preflight_script"
extract_workflow_run_from \
  "$consumer_source" dedicated-credential-check "$consumer_credential_check_script"
extract_workflow_run_from "$consumer_source" prepare-consumer "$consumer_prepare_script"
extract_workflow_run_from \
  "$consumer_source" configure-dedicated "$consumer_config_script"
extract_workflow_run_from "$consumer_source" restore "$consumer_restore_script"
extract_workflow_run_from \
  "$consumer_source" assert-package-graph "$consumer_assert_script"
extract_workflow_inline_run_from \
  "$consumer_source" cleanup-config "$consumer_cleanup_script"
extract_workflow_inline_run_from "$consumer_source" build "$consumer_build_script"
extract_workflow_inline_run_from "$consumer_source" run "$consumer_run_script"

for consumer_script in \
  "$consumer_preflight_script" \
  "$consumer_credential_check_script" \
  "$consumer_prepare_script" \
  "$consumer_config_script" \
  "$consumer_restore_script" \
  "$consumer_assert_script" \
  "$consumer_cleanup_script" \
  "$consumer_build_script" \
  "$consumer_run_script"; do
  bash -n "$consumer_script"
done

cat > "$consumer_stub_bin/gh" <<'STUB'
#!/usr/bin/env bash
set -euo pipefail
[[ "$*" == "api repos/KyuzanInc/peak-sdk-csharp --jq .visibility" ]]
printf '%s\n' "${STUB_VISIBILITY:?}"
STUB
chmod +x "$consumer_stub_bin/gh"

consumer_preflight_output="$consumer_test_dir/preflight-output"
run_consumer_preflight() {
  local event_name=$1
  local dispatch_version=$2
  local workflow_run_name=$3
  local workflow_run_conclusion=$4
  local visibility=$5
  : > "$consumer_preflight_output"
  env \
    PATH="$consumer_stub_bin:$PATH" \
    STUB_VISIBILITY="$visibility" \
    REPOSITORY=KyuzanInc/peak-sdk-csharp \
    EVENT_NAME="$event_name" \
    DISPATCH_VERSION="$dispatch_version" \
    WORKFLOW_RUN_NAME="$workflow_run_name" \
    WORKFLOW_RUN_CONCLUSION="$workflow_run_conclusion" \
    EXPECTED_VERSION=1.0.0 \
    GITHUB_OUTPUT="$consumer_preflight_output" \
    /''bin/bash "$consumer_preflight_script"
}

for validated_visibility in private public internal; do
  run_consumer_preflight \
    workflow_dispatch 1.0.0 '' '' "$validated_visibility"
  grep -Fxq "visibility=$validated_visibility" "$consumer_preflight_output"
  grep -Fxq 'package_version=1.0.0' "$consumer_preflight_output"
  env \
    NUGET_READ_USERNAME=reader \
    NUGET_READ_TOKEN=read-token \
    /''bin/bash "$consumer_credential_check_script"
done
run_consumer_preflight workflow_run '' Release success private
grep -Fxq 'visibility=private' "$consumer_preflight_output"
for invalid_case in \
  'workflow_dispatch|1.0.1|||public' \
  'workflow_dispatch|1.*|||internal' \
  'workflow_run||Other|success|private' \
  'workflow_run||Release|failure|private' \
  'push||||private' \
  'workflow_dispatch|1.0.0|||unknown'; do
  IFS='|' read -r \
    event_name dispatch_version workflow_name conclusion visibility \
    <<< "$invalid_case"
  if run_consumer_preflight \
    "$event_name" "$dispatch_version" "$workflow_name" "$conclusion" "$visibility" \
    >/dev/null 2>&1; then
    echo "expected consumer preflight to reject: $invalid_case" >&2
    exit 1
  fi
done

for missing_read_credential in NUGET_READ_USERNAME NUGET_READ_TOKEN; do
  if env \
    NUGET_READ_USERNAME=reader \
    NUGET_READ_TOKEN=read-token \
    "$missing_read_credential=" \
    /''bin/bash "$consumer_credential_check_script" >/dev/null 2>&1; then
    echo "expected dedicated consumer route to reject missing $missing_read_credential" >&2
    exit 1
  fi
done
if env \
  NUGET_READ_USERNAME= \
  NUGET_READ_TOKEN= \
  /''bin/bash "$consumer_credential_check_script" >/dev/null 2>&1; then
  echo "expected private consumer route to reject absent dedicated credentials" >&2
  exit 1
fi

consumer_root="$consumer_test_dir/peak-consumer"
consumer_project="$consumer_root/PeakSdkConsumer"
consumer_config="$consumer_root/NuGet.Config"
consumer_packages="$consumer_root/.nuget/packages"
env \
  CONSUMER_ROOT="$consumer_root" \
  CONSUMER_PROJECT="$consumer_project" \
  DOTNET_CLI_HOME="$consumer_root/.dotnet" \
  NUGET_PACKAGES="$consumer_packages" \
  NUGET_CONFIG="$consumer_config" \
  /''bin/bash "$consumer_prepare_script"
grep -Fxq \
  '    <PackageReference Include="KyuzanInc.Peak.Sdk" Version="[1.0.0]" />' \
  "$consumer_project/PeakSdkConsumer.csproj"
grep -Fq 'PeakClient.Initialize(new PeakClientOptions' "$consumer_project/Program.cs"
grep -Fq 'var storage = new InMemoryStorage();' "$consumer_project/Program.cs"
grep -Fq '<package pattern="KyuzanInc.Turnkey.*" />' "$consumer_config"
grep -Fq 'https://nuget.pkg.github.com/KyuzanInc/index.json' "$consumer_config"
python3 - "$consumer_config" <<'PY'
import pathlib
import sys

path = pathlib.Path(sys.argv[1])
if path.stat().st_mode & 0o077:
    raise SystemExit("consumer NuGet config permissions are broader than owner-only")
PY

consumer_dotnet_log="$consumer_test_dir/dotnet.log"
cat > "$consumer_stub_bin/dotnet" <<'STUB'
#!/usr/bin/env bash
set -euo pipefail
printf '%s\n' "$*" >> "${STUB_DOTNET_LOG:?}"
STUB
cat > "$consumer_stub_bin/stat" <<'STUB'
#!/usr/bin/env bash
set -euo pipefail
[[ "$1" == -c && "$2" == %a ]]
printf '600\n'
STUB
chmod +x "$consumer_stub_bin/dotnet" "$consumer_stub_bin/stat"
: > "$consumer_dotnet_log"
consumer_config_output="$consumer_test_dir/config-output"
env \
  PATH="$consumer_stub_bin:$PATH" \
  STUB_DOTNET_LOG="$consumer_dotnet_log" \
  NUGET_READ_USERNAME=reader \
  NUGET_READ_TOKEN=read-token \
  NUGET_CONFIG="$consumer_config" \
  /''bin/bash "$consumer_config_script" > "$consumer_config_output"
[[ ! -s "$consumer_config_output" ]]
grep -Fq 'nuget update source github-kyuzan' "$consumer_dotnet_log"
grep -Fq -- '--username reader' "$consumer_dotnet_log"
grep -Fq -- '--password read-token' "$consumer_dotnet_log"
grep -Fq -- "--configfile $consumer_config" "$consumer_dotnet_log"

: > "$consumer_dotnet_log"
env \
  PATH="$consumer_stub_bin:$PATH" \
  STUB_DOTNET_LOG="$consumer_dotnet_log" \
  CONSUMER_PROJECT="$consumer_project" \
  NUGET_CONFIG="$consumer_config" \
  /''bin/bash "$consumer_restore_script"
grep -Fq \
  "restore $consumer_project/PeakSdkConsumer.csproj --configfile $consumer_config --no-cache --force-evaluate" \
  "$consumer_dotnet_log"

mkdir -p "$consumer_project/obj" "$consumer_packages/kyuzaninc.peak.sdk/1.0.0"
consumer_assets="$consumer_project/obj/project.assets.json"
consumer_metadata="$consumer_packages/kyuzaninc.peak.sdk/1.0.0/.nupkg.metadata"
cat > "$consumer_assets" <<'JSON'
{
  "libraries": {
    "KyuzanInc.Peak.Sdk/1.0.0": {},
    "kyuzaninc.turnkey.sdk/1.0.0": {}
  }
}
JSON
cat > "$consumer_metadata" <<'JSON'
{"source":"https://nuget.pkg.github.com/KyuzanInc/index.json"}
JSON
env \
  CONSUMER_PROJECT="$consumer_project" \
  NUGET_PACKAGES="$consumer_packages" \
  /''bin/bash "$consumer_assert_script" >/dev/null

for invalid_assets in \
  '{"libraries":{"KyuzanInc.Peak.Sdk/1.0.1":{},"KyuzanInc.Turnkey.Sdk/1.0.0":{}}}' \
  '{"libraries":{"KyuzanInc.Peak.Sdk/1.0.0":{},"KyuzanInc.Peak.Sdk/2.0.0":{},"KyuzanInc.Turnkey.Sdk/1.0.0":{}}}' \
  '{"libraries":{"KyuzanInc.Peak.Sdk/1.0.0":{},"KyuzanInc.Turnkey.Sdk/1.*":{}}}'; do
  printf '%s\n' "$invalid_assets" > "$consumer_assets"
  if env \
    CONSUMER_PROJECT="$consumer_project" \
    NUGET_PACKAGES="$consumer_packages" \
    /''bin/bash "$consumer_assert_script" >/dev/null 2>&1; then
    echo "expected consumer package assertion to reject: $invalid_assets" >&2
    exit 1
  fi
done
cat > "$consumer_assets" <<'JSON'
{"libraries":{"KyuzanInc.Peak.Sdk/1.0.0":{},"KyuzanInc.Turnkey.Sdk/1.0.0":{}}}
JSON
printf '%s\n' '{"source":"https://api.nuget.org/v3/index.json"}' > "$consumer_metadata"
if env \
  CONSUMER_PROJECT="$consumer_project" \
  NUGET_PACKAGES="$consumer_packages" \
  /''bin/bash "$consumer_assert_script" >/dev/null 2>&1; then
  echo "expected consumer package assertion to reject a non-GitHub Peak source" >&2
  exit 1
fi

test -f "$consumer_config"
env NUGET_CONFIG="$consumer_config" /''bin/bash "$consumer_cleanup_script"
test ! -e "$consumer_config"

: > "$consumer_dotnet_log"
env \
  PATH="$consumer_stub_bin:$PATH" \
  STUB_DOTNET_LOG="$consumer_dotnet_log" \
  CONSUMER_PROJECT="$consumer_project" \
  /''bin/bash "$consumer_build_script"
env \
  PATH="$consumer_stub_bin:$PATH" \
  STUB_DOTNET_LOG="$consumer_dotnet_log" \
  CONSUMER_PROJECT="$consumer_project" \
  /''bin/bash "$consumer_run_script"
grep -Fxq \
  "build $consumer_project/PeakSdkConsumer.csproj -c Release --no-restore" \
  "$consumer_dotnet_log"
grep -Fxq \
  "run --project $consumer_project/PeakSdkConsumer.csproj -c Release --no-build --no-restore" \
  "$consumer_dotnet_log"
if grep -Fv -- '--no-restore' "$consumer_dotnet_log" >/dev/null; then
  echo "consumer build/run stub observed an implicit-restore command" >&2
  exit 1
fi

release_source="$repo_root/.github/workflows/release.yml"
if [[ ! -f "$release_source" ]]; then
  echo "release workflow is missing: $release_source" >&2
  exit 1
fi

reset_release_fixture() {
  cp "$release_source" "$fixture/.github/workflows/release.yml"
  git -C "$fixture" add .github/workflows/release.yml
}

replace_release_once() {
  local old=$1
  local new=$2
  python3 - "$fixture/.github/workflows/release.yml" "$old" "$new" <<'PY'
import pathlib
import sys

path = pathlib.Path(sys.argv[1])
old = sys.argv[2]
new = sys.argv[3]
text = path.read_text(encoding="utf-8")
if text.count(old) != 1:
    raise SystemExit(f"fixture mutation expected one match, found {text.count(old)}: {old!r}")
path.write_text(text.replace(old, new), encoding="utf-8")
PY
}

insert_release_once() {
  local marker=$1
  local insertion=$2
  replace_release_once "$marker" "$marker$insertion"
}

reset_release_fixture
run_expect_pass release.yml

reset_release_fixture
replace_release_once $'jobs:\n  publish:' \
  $'jobs:
  "shadow":
    "steps":
      - "run": printf \'quoted shadow job ran\
\'
  publish:'
run_expect_fail release.yml "literal quoted extra job and steps block"

reset_release_fixture
replace_release_once $'jobs:\n  publish:' \
  $'jobs:
  shadow:
    runs-on: ubuntu-latest
    steps:
      - name: Extra executable step
        run: printf \'shadow job ran\
\'
  publish:'
run_expect_fail release.yml "extra executable release job"

reset_release_fixture
insert_release_once $'    steps:\n' \
  $'      - name: Unknown release step
        id: unknown-release-step
        run: printf \'unknown step ran\
\'
'
run_expect_fail release.yml "unknown release step"

reset_release_fixture
insert_release_once $'        id: preflight\n' \
  $'        id: preflight\n'
run_expect_fail release.yml "duplicate release step id"

reset_release_fixture
replace_release_once \
  '          NUPKG_PATH: ${{ steps.package.outputs.nupkg_path }}' \
  '          NUPKG_PATH: ${{ steps.package.outputs.snupkg_path }}'
run_expect_fail release.yml "registry nupkg input rebound to snupkg output"

reset_release_fixture
replace_release_once \
  $'          visibility="$(gh api "repos/$REPOSITORY" --jq \'.visibility\')"' \
  $'          false && visibility="$(gh api "repos/$REPOSITORY" --jq \'.visibility\')" || visibility=private'
run_expect_fail release.yml "inert visibility query with private fallback"

reset_release_fixture
replace_release_once \
  $'        id: publish-private-fallback\n        if: steps.preflight.outputs.visibility == \'private\' && steps.credential-mode.outputs.mode == \'fallback\'' \
  $'        id: publish-private-fallback'
run_expect_fail release.yml "private fallback guard removed"

reset_release_fixture
replace_release_once \
  $'        id: read-public-internal\n        if: steps.preflight.outputs.visibility == \'public\' || steps.preflight.outputs.visibility == \'internal\'\n        shell: bash\n        env:\n          NUGET_READ_USERNAME: ${{ secrets.NUGET_READ_USERNAME }}\n          NUGET_READ_TOKEN: ${{ secrets.NUGET_READ_TOKEN }}' \
  $'        id: read-public-internal\n        if: steps.preflight.outputs.visibility == \'public\' || steps.preflight.outputs.visibility == \'internal\'\n        shell: bash\n        env:\n          NUGET_READ_USERNAME: ${{ secrets.NUGET_READ_USERNAME }}\n          NUGET_READ_TOKEN: ${{ secrets.NUGET_PUBLISH_TOKEN }}'
run_expect_fail release.yml "read credential rebound to publish secret"

reset_release_fixture
replace_release_once \
  $'        id: pack\n        shell: bash\n        env:\n          PACKAGE_DIR: ${{ runner.temp }}/peak-package\n        run: |\n          set -euo pipefail\n' \
  $'        id: pack
        shell: bash
        env:
          PACKAGE_DIR: ${{ runner.temp }}/peak-package
        run: |
          set -euo pipefail
          printf \'arbitrary critical mutation\
\'
'
run_expect_fail release.yml "arbitrary pack step body mutation"

reset_release_fixture
replace_release_once \
  'cmp --silent "$DOWNLOADED_MANIFEST" "$MANIFEST_PATH"' \
  'false && cmp --silent "$DOWNLOADED_MANIFEST" "$MANIFEST_PATH" || cp "$DOWNLOADED_MANIFEST" "$MANIFEST_PATH"'
run_expect_fail release.yml "inert manifest comparison with unsafe replacement"

reset_release_fixture
replace_release_once \
  'cmp --silent "$DOWNLOADED_NUPKG" "$NUPKG_PATH"' \
  'false && cmp --silent "$DOWNLOADED_NUPKG" "$NUPKG_PATH" || cp "$DOWNLOADED_NUPKG" "$NUPKG_PATH"'
run_expect_fail release.yml "inert package comparison with unsafe replacement"

reset_release_fixture
replace_release_once \
  $'            gh api "repos/$REPOSITORY/releases/assets/$manifest_asset_id" \\\n              -H "Accept: application/octet-stream" > "$DOWNLOADED_MANIFEST"\n            cmp --silent "$DOWNLOADED_MANIFEST" "$MANIFEST_PATH"' \
  $'            cmp --silent "$DOWNLOADED_MANIFEST" "$MANIFEST_PATH"\n            gh api "repos/$REPOSITORY/releases/assets/$manifest_asset_id" \\\n              -H "Accept: application/octet-stream" > "$DOWNLOADED_MANIFEST"'
run_expect_fail release.yml "comparison reordered before Release asset download"

reset_release_fixture
replace_release_once $'  release:\n    types: [published]' \
  $'  push:\n    tags: [v1.0.0]'
run_expect_fail release.yml "push publication trigger"

reset_release_fixture
replace_release_once $'  release:\n    types: [published]' \
  $'  workflow_dispatch:'
run_expect_fail release.yml "workflow_dispatch publication trigger"

reset_release_fixture
replace_release_once $'  release:\n    types: [published]' \
  $'  pull_request:'
run_expect_fail release.yml "non-release publication trigger"

reset_release_fixture
insert_release_once $'  packages: write\n' $'  id-token: write\n'
run_expect_fail release.yml "id-token write permission"

reset_release_fixture
insert_release_once $'    steps:\n' \
  $'      - uses: actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a\n        with:\n          path: .artifacts/package/*.nupkg\n'
run_expect_fail release.yml "release package Actions artifact"

reset_release_fixture
insert_release_once \
  $'        id: preflight\n        shell: bash\n        env:\n          GH_TOKEN: ${{ github.token }}\n' \
  $'          NUGET_API_KEY: ${{ secrets.NUGET_API_KEY }}\n'
run_expect_fail release.yml "NuGet API key"

reset_release_fixture
insert_release_once $'          dotnet nuget push "$NUPKG_PATH"' \
  $' --source https://api.nuget.org/v3/index.json'
run_expect_fail release.yml "nuget.org push"

reset_release_fixture
insert_release_once $'          dotnet nuget push "$NUPKG_PATH"' \
  $' --skip-duplicate'
run_expect_fail release.yml "skip duplicate"

reset_release_fixture
replace_release_once '    environment: github-packages' \
  '    environment: unprotected'
run_expect_fail release.yml "wrong protected environment"

reset_release_fixture
replace_release_once $'gh api "repos/$REPOSITORY" --jq \'.visibility\'' \
  $'printf \'private\\n\''
run_expect_fail release.yml "missing live visibility query"

reset_release_fixture
replace_release_once \
  $'        id: dedicated-credential-check\n        if: steps.preflight.outputs.visibility == \'public\' || steps.preflight.outputs.visibility == \'internal\'\n        shell: bash\n        env:\n          NUGET_READ_USERNAME: ${{ secrets.NUGET_READ_USERNAME }}' \
  $'        id: dedicated-credential-check\n        if: steps.preflight.outputs.visibility == \'public\' || steps.preflight.outputs.visibility == \'internal\'\n        shell: bash\n        env:\n          NUGET_READ_USERNAME_MISSING: ${{ secrets.NUGET_READ_USERNAME }}'
run_expect_fail release.yml "missing dedicated read username binding"

reset_release_fixture
replace_release_once \
  $'        id: dedicated-credential-check\n        if: steps.preflight.outputs.visibility == \'public\' || steps.preflight.outputs.visibility == \'internal\'\n        shell: bash\n        env:\n          NUGET_READ_USERNAME: ${{ secrets.NUGET_READ_USERNAME }}\n          NUGET_READ_TOKEN: ${{ secrets.NUGET_READ_TOKEN }}\n          NUGET_PUBLISH_USERNAME: ${{ secrets.NUGET_PUBLISH_USERNAME }}\n          NUGET_PUBLISH_TOKEN: ${{ secrets.NUGET_PUBLISH_TOKEN }}' \
  $'        id: dedicated-credential-check\n        if: steps.preflight.outputs.visibility == \'public\' || steps.preflight.outputs.visibility == \'internal\'\n        shell: bash\n        env:\n          NUGET_READ_USERNAME: ${{ secrets.NUGET_READ_USERNAME }}\n          NUGET_READ_TOKEN: ${{ secrets.NUGET_READ_TOKEN }}\n          NUGET_PUBLISH_USERNAME: ${{ secrets.NUGET_PUBLISH_USERNAME }}\n          NUGET_PUBLISH_TOKEN_MISSING: ${{ secrets.NUGET_PUBLISH_TOKEN }}'
run_expect_fail release.yml "missing dedicated publish token binding"

reset_release_fixture
insert_release_once $'    steps:\n' \
  $'      - name: Early package access\n        run: dotnet restore peak-sdk-csharp.sln\n'
run_expect_fail release.yml "package access before preflight"

reset_release_fixture
replace_release_once \
  $'        id: read-private-fallback\n        if: steps.preflight.outputs.visibility == \'private\' && steps.credential-mode.outputs.mode == \'fallback\'' \
  $'        id: read-private-fallback\n        if: steps.preflight.outputs.visibility != \'private\' && steps.credential-mode.outputs.mode == \'fallback\''
run_expect_fail release.yml "fallback token on a public or internal path"

reset_release_fixture
replace_release_once \
  'if [[ "$asset_name" != "release-checksums.txt" ]]; then' \
  'if [[ "$asset_name" != "release-checksums.txt" && "$asset_name" != *.nupkg ]]; then'
run_expect_fail release.yml "package binary allowed as a Release asset"

reset_release_fixture
insert_release_once $'          dotnet nuget push "$NUPKG_PATH"' \
  $'\n          dotnet nuget push "$SNUPKG_PATH"'
run_expect_fail release.yml "symbol package registry push"

reset_release_fixture
replace_release_once \
  'gh release upload "$TAG_NAME" "$MANIFEST_PATH"' \
  'gh release upload "$TAG_NAME" "$NUPKG_PATH"'
run_expect_fail release.yml "package binary uploaded to the Release"

reset_release_fixture
replace_release_once \
  'python3 tools/package/canonicalize-nuget-package.py "$NUPKG_PATH"' \
  'true # package canonicalization removed'
run_expect_fail release.yml "missing deterministic package canonicalization"

reset_release_fixture
replace_release_once \
  'gh release upload "$TAG_NAME" "$MANIFEST_PATH"' \
  'gh release upload "$TAG_NAME" "$MANIFEST_PATH" --clobber'
run_expect_fail release.yml "release manifest clobber"

reset_release_fixture
replace_release_once \
  'repos/$REPOSITORY/compare/main...$TAG_COMMIT' \
  'repos/$REPOSITORY/compare/$TAG_COMMIT...main'
run_expect_fail release.yml "reversed main and release comparison"

reset_release_fixture
replace_release_once \
  '[[ "$TAG_NAME" =~ ^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$ ]]' \
  '[[ "$TAG_NAME" == v* ]]'
run_expect_fail release.yml "loose release tag validation"

reset_release_fixture
replace_release_once \
  'cmp --silent "$DOWNLOADED_MANIFEST" "$MANIFEST_PATH"' \
  'test -s "$DOWNLOADED_MANIFEST"'
run_expect_fail release.yml "missing existing manifest byte comparison"

reset_release_fixture
replace_release_once \
  'cmp --silent "$DOWNLOADED_NUPKG" "$NUPKG_PATH"' \
  'test -s "$DOWNLOADED_NUPKG"'
run_expect_fail release.yml "missing existing package byte comparison"

reset_release_fixture
replace_release_once 'elif [[ "$VERSION_PRESENT" == 0 ]]; then' \
  'elif [[ "$VERSION_PRESENT" != 0 ]]; then'
run_expect_fail release.yml "reversed publish versus no-publish decision"

extract_release_run() {
  extract_workflow_run_from "$release_source" "$1" "$2"
}

local_test_dir="$fixture/release-script-tests"
stub_bin="$local_test_dir/bin"
mkdir -p "$stub_bin"
preflight_script="$local_test_dir/preflight.sh"
credential_check_script="$local_test_dir/dedicated-credential-check.sh"
credential_mode_script="$local_test_dir/credential-mode.sh"
read_config_script="$local_test_dir/read-public-internal.sh"
publish_config_script="$local_test_dir/publish-public-internal.sh"
package_script="$local_test_dir/package.sh"
release_assets_script="$local_test_dir/release-assets.sh"
registry_script="$local_test_dir/registry.sh"
extract_release_run preflight "$preflight_script"
extract_release_run dedicated-credential-check "$credential_check_script"
extract_release_run credential-mode "$credential_mode_script"
extract_release_run read-public-internal "$read_config_script"
extract_release_run publish-public-internal "$publish_config_script"
extract_release_run package "$package_script"
extract_release_run release-assets "$release_assets_script"
extract_release_run registry "$registry_script"

cat > "$stub_bin/gh" <<'STUB'
#!/usr/bin/env bash
set -euo pipefail
case " $* " in
  *"/compare/"*) printf '%s\n' "${STUB_COMPARE_STATUS:?}" ;;
  *" repos/"*) printf '%s\n' "${STUB_VISIBILITY:?}" ;;
  *) exit 91 ;;
esac
STUB
cat > "$stub_bin/git" <<'STUB'
#!/usr/bin/env bash
set -euo pipefail
[[ "$*" == "rev-parse HEAD" ]]
printf '%s\n' "${STUB_CHECKED_OUT_COMMIT:?}"
STUB
chmod +x "$stub_bin/gh" "$stub_bin/git"

project_fixture="$local_test_dir/peak-sdk-csharp.csproj"
write_project_fixture() {
  local version=$1
  local package_id=${2:-KyuzanInc.Peak.Sdk}
  cat > "$project_fixture" <<PROJECT
<Project>
  <PropertyGroup>
    <PackageId>$package_id</PackageId>
    <Version>$version</Version>
  </PropertyGroup>
</Project>
PROJECT
}

run_preflight() {
  local tag=$1
  local visibility=$2
  local compare_status=$3
  local checked_out_commit=${4:-0123456789012345678901234567890123456789}
  local tag_commit=${5:-0123456789012345678901234567890123456789}
  local output="$local_test_dir/preflight-output"
  : > "$output"
  env \
    PATH="$stub_bin:$PATH" \
    STUB_VISIBILITY="$visibility" \
    STUB_COMPARE_STATUS="$compare_status" \
    STUB_CHECKED_OUT_COMMIT="$checked_out_commit" \
    REPOSITORY=KyuzanInc/peak-sdk-csharp \
    TAG_NAME="$tag" \
    TAG_COMMIT="$tag_commit" \
    PROJECT_FILE="$project_fixture" \
    EXPECTED_VERSION=1.0.0 \
    GITHUB_OUTPUT="$output" \
    /''bin/bash "$preflight_script"
}

write_project_fixture 1.0.0
run_preflight v1.0.0 private identical
grep -Fxq 'visibility=private' "$local_test_dir/preflight-output"
if run_preflight v1.0.0 public behind >/dev/null 2>&1; then
  echo "expected Release preflight to reject a tag behind main" >&2
  exit 1
fi

for invalid_tag in \
  v01.0.0 \
  v1.00.0 \
  v1.0.00 \
  v1.0.0-alpha \
  v1.0.0+build \
  prefix-v1.0.0 \
  v1.0.0-suffix \
  v1.0.1; do
  if run_preflight "$invalid_tag" private identical >/dev/null 2>&1; then
    echo "expected strict Release preflight to reject tag: $invalid_tag" >&2
    exit 1
  fi
done

for invalid_visibility in unknown ''; do
  if run_preflight v1.0.0 "$invalid_visibility" identical >/dev/null 2>&1; then
    echo "expected Release preflight to reject visibility: $invalid_visibility" >&2
    exit 1
  fi
done

for invalid_compare in behind ahead diverged unexpected ''; do
  if run_preflight v1.0.0 private "$invalid_compare" >/dev/null 2>&1; then
    echo "expected Release preflight to reject compare status: $invalid_compare" >&2
    exit 1
  fi
done

if run_preflight v1.0.0 private identical \
  1111111111111111111111111111111111111111 \
  2222222222222222222222222222222222222222 >/dev/null 2>&1; then
  echo "expected Release preflight to reject a checkout/tag commit mismatch" >&2
  exit 1
fi

write_project_fixture 1.0.1
if run_preflight v1.0.0 private identical >/dev/null 2>&1; then
  echo "expected Release preflight to reject a project version mismatch" >&2
  exit 1
fi
write_project_fixture 1.0.0 Wrong.Package
if run_preflight v1.0.0 private identical >/dev/null 2>&1; then
  echo "expected Release preflight to reject a package id mismatch" >&2
  exit 1
fi
write_project_fixture 1.0.0

credential_output="$local_test_dir/credential-output"
run_credential_mode() {
  : > "$credential_output"
  env \
    NUGET_READ_USERNAME="${1-}" \
    NUGET_READ_TOKEN="${2-}" \
    NUGET_PUBLISH_USERNAME="${3-}" \
    NUGET_PUBLISH_TOKEN="${4-}" \
    GITHUB_OUTPUT="$credential_output" \
    /''bin/bash "$credential_mode_script"
}
run_credential_mode '' '' '' ''
grep -Fxq 'mode=fallback' "$credential_output"
run_credential_mode read-user read-token publish-user publish-token
grep -Fxq 'mode=dedicated' "$credential_output"
if run_credential_mode read-user '' '' '' >/dev/null 2>&1; then
  echo "expected private credential routing to reject partial credentials" >&2
  exit 1
fi

env \
  NUGET_READ_USERNAME=read-user \
  NUGET_READ_TOKEN=read-token \
  NUGET_PUBLISH_USERNAME=publish-user \
  NUGET_PUBLISH_TOKEN=publish-token \
  /''bin/bash "$credential_check_script"
for missing_credential in \
  NUGET_READ_USERNAME \
  NUGET_READ_TOKEN \
  NUGET_PUBLISH_USERNAME \
  NUGET_PUBLISH_TOKEN; do
  if env \
    NUGET_READ_USERNAME=read-user \
    NUGET_READ_TOKEN=read-token \
    NUGET_PUBLISH_USERNAME=publish-user \
    NUGET_PUBLISH_TOKEN=publish-token \
    "$missing_credential=" \
    /''bin/bash "$credential_check_script" >/dev/null 2>&1; then
    echo "expected public/internal credential check to reject missing $missing_credential" >&2
    exit 1
  fi
done

read_config="$local_test_dir/nuget-read.config"
env \
  NUGET_READ_USERNAME=fixture-reader \
  NUGET_READ_TOKEN=fixture-read-token \
  READ_CONFIG="$read_config" \
  /''bin/bash "$read_config_script" >/dev/null
grep -Fq 'https://api.nuget.org/v3/index.json' "$read_config"
grep -Fq 'https://nuget.pkg.github.com/KyuzanInc/index.json' "$read_config"
python3 - "$read_config" <<'PY'
import os
import pathlib
import sys

mode = pathlib.Path(sys.argv[1]).stat().st_mode
if mode & 0o077:
    raise SystemExit("read NuGet config permissions are broader than owner-only")
PY

publish_config_fixture="$local_test_dir/generated-nuget-publish.config"
publish_username_fixture="$local_test_dir/generated-publish-username"
publish_token_fixture="$local_test_dir/generated-publish-token"
env \
  NUGET_PUBLISH_USERNAME=fixture-publisher \
  NUGET_PUBLISH_TOKEN=fixture-publish-token \
  PUBLISH_CONFIG="$publish_config_fixture" \
  PUBLISH_USERNAME_FILE="$publish_username_fixture" \
  PUBLISH_TOKEN_FILE="$publish_token_fixture" \
  /''bin/bash "$publish_config_script" >/dev/null
grep -Fq 'https://nuget.pkg.github.com/KyuzanInc/index.json' "$publish_config_fixture"
[[ "$(cat "$publish_username_fixture")" == fixture-publisher ]]
[[ "$(cat "$publish_token_fixture")" == fixture-publish-token ]]
python3 - \
  "$publish_config_fixture" \
  "$publish_username_fixture" \
  "$publish_token_fixture" <<'PY'
import pathlib
import sys

for path_value in sys.argv[1:]:
    path = pathlib.Path(path_value)
    if path.stat().st_mode & 0o077:
        raise SystemExit(f"publish credential file permissions are too broad: {path}")
PY

cat > "$stub_bin/bash" <<'STUB'
#!/bin/bash
set -euo pipefail
[[ "${1-}" == "tools/package/validate-package.sh" ]]
exit 0
STUB
chmod +x "$stub_bin/bash"
cat > "$stub_bin/python3" <<'STUB'
#!/bin/bash
set -euo pipefail
[[ "${1-}" == "tools/package/canonicalize-nuget-package.py" ]]
[[ -f "${2-}" ]]
exit 0
STUB
chmod +x "$stub_bin/python3"
package_dir="$local_test_dir/package"
manifest_path="$local_test_dir/release-checksums.txt"
mkdir -p "$package_dir"
printf 'nupkg bytes\n' > "$package_dir/KyuzanInc.Peak.Sdk.1.0.0.nupkg"
printf 'snupkg bytes\n' > "$package_dir/KyuzanInc.Peak.Sdk.1.0.0.snupkg"
: > "$local_test_dir/package-output"
env \
  PATH="$stub_bin:$PATH" \
  PACKAGE_DIR="$package_dir" \
  MANIFEST_PATH="$manifest_path" \
  GITHUB_OUTPUT="$local_test_dir/package-output" \
  /''bin/bash "$package_script"
expected_manifest="$local_test_dir/expected-release-checksums.txt"
(
  cd "$package_dir"
  printf '%s\n' \
    KyuzanInc.Peak.Sdk.1.0.0.nupkg \
    KyuzanInc.Peak.Sdk.1.0.0.snupkg |
    LC_ALL=C sort |
    while IFS= read -r package; do
      sha256sum "$package"
    done
) > "$expected_manifest"
cmp "$expected_manifest" "$manifest_path"
if rg -q "$package_dir" "$manifest_path"; then
  echo "checksum manifest must contain basename-only paths" >&2
  exit 1
fi
printf 'unexpected\n' > "$package_dir/Unexpected.1.0.0.nupkg"
if env \
  PATH="$stub_bin:$PATH" \
  PACKAGE_DIR="$package_dir" \
  MANIFEST_PATH="$manifest_path" \
  GITHUB_OUTPUT="$local_test_dir/package-output" \
  /''bin/bash "$package_script" >/dev/null 2>&1; then
  echo "expected checksum generation to reject an extra nupkg" >&2
  exit 1
fi
rm "$package_dir/Unexpected.1.0.0.nupkg"
rm "$stub_bin/bash"
rm "$stub_bin/python3"

cat > "$stub_bin/gh" <<'STUB'
#!/usr/bin/env bash
set -euo pipefail
case " $* " in
  *"--paginate --slurp"*) printf '%s\n' "${STUB_ASSET_JSON:?}" ;;
  *"/releases/assets/"*) cat "${STUB_MANIFEST_DOWNLOAD:?}" ;;
  *) exit 92 ;;
esac
STUB
chmod +x "$stub_bin/gh"
run_release_assets() {
  local payload=$1
  local download_source=${2:-"$manifest_path"}
  local script=${3:-"$release_assets_script"}
  local output="$local_test_dir/release-assets-output"
  : > "$output"
  env \
    PATH="$stub_bin:$PATH" \
    STUB_ASSET_JSON="$payload" \
    STUB_MANIFEST_DOWNLOAD="$download_source" \
    REPOSITORY=KyuzanInc/peak-sdk-csharp \
    RELEASE_ID=100 \
    MANIFEST_PATH="$manifest_path" \
    ASSET_JSON="$local_test_dir/release-assets.json" \
    ASSET_NAME_FILE="$local_test_dir/release-asset-names" \
    MANIFEST_ASSET_ID="$local_test_dir/release-manifest-asset-id" \
    DOWNLOADED_MANIFEST="$local_test_dir/existing-release-checksums.txt" \
    GITHUB_OUTPUT="$output" \
    /''bin/bash "$script"
}
run_release_assets '[[]]'
grep -Fxq 'manifest_exists=0' "$local_test_dir/release-assets-output"
run_release_assets '[[{"name":"release-checksums.txt","id":42}]]'
grep -Fxq 'manifest_exists=1' "$local_test_dir/release-assets-output"
printf 'different manifest\n' > "$local_test_dir/different-manifest"
if run_release_assets '[[{"name":"release-checksums.txt","id":42}]]' \
  "$local_test_dir/different-manifest" >/dev/null 2>&1; then
  echo "expected Release preflight to reject a changed checksum manifest" >&2
  exit 1
fi
for invalid_assets in \
  '[[{"name":"KyuzanInc.Peak.Sdk.1.0.0.nupkg","id":41}]]' \
  '[[{"name":"KyuzanInc.Peak.Sdk.1.0.0.snupkg","id":41}]]' \
  '[[{"name":"release-checksums.txt","id":41},{"name":"release-checksums.txt","id":42}]]' \
  '{"name":"release-checksums.txt","id":41}' \
  '[[{"name":4,"id":41}]]'; do
  if run_release_assets "$invalid_assets" >/dev/null 2>&1; then
    echo "expected Release asset preflight to reject: $invalid_assets" >&2
    exit 1
  fi
done

mutated_release_workflow="$local_test_dir/mutated-release.yml"
mutated_release_assets_script="$local_test_dir/mutated-release-assets.sh"
cp "$release_source" "$mutated_release_workflow"
python3 - "$mutated_release_workflow" <<'PY'
import pathlib
import sys

path = pathlib.Path(sys.argv[1])
text = path.read_text(encoding="utf-8")
safe = (
    '            gh api "repos/$REPOSITORY/releases/assets/$manifest_asset_id" \\\n'
    '              -H "Accept: application/octet-stream" > "$DOWNLOADED_MANIFEST"\n'
    '            cmp --silent "$DOWNLOADED_MANIFEST" "$MANIFEST_PATH"'
)
unsafe = (
    '            cmp --silent "$DOWNLOADED_MANIFEST" "$MANIFEST_PATH"\n'
    '            gh api "repos/$REPOSITORY/releases/assets/$manifest_asset_id" \\\n'
    '              -H "Accept: application/octet-stream" > "$DOWNLOADED_MANIFEST"'
)
if text.count(safe) != 1:
    raise SystemExit("reordered-comparison behavior mutation lost its anchor")
path.write_text(text.replace(safe, unsafe), encoding="utf-8")
PY
extract_workflow_run_from \
  "$mutated_release_workflow" \
  release-assets \
  "$mutated_release_assets_script"
cp "$manifest_path" "$local_test_dir/existing-release-checksums.txt"
if ! run_release_assets \
  '[[{"name":"release-checksums.txt","id":42}]]' \
  "$local_test_dir/different-manifest" \
  "$mutated_release_assets_script" >/dev/null 2>&1; then
  echo "reordered comparison mutation no longer demonstrates unsafe stale comparison behavior" >&2
  exit 1
fi

cat > "$stub_bin/gh" <<'STUB'
#!/usr/bin/env bash
set -euo pipefail
printf '%s\n' "$*" >> "${STUB_GH_LOG:?}"
case " $* " in
  *"packages?package_type=nuget&per_page=100"*)
    [[ "${STUB_PACKAGE_INVENTORY_STATUS:?}" == 200 ]] || exit 22
    printf '%s\n' "${STUB_PACKAGE_INVENTORY_JSON:?}"
    ;;
  *"/packages/nuget/KyuzanInc.Peak.Sdk/versions?per_page=100"*)
    [[ "${STUB_VERSION_INVENTORY_STATUS:?}" == 200 ]] || exit 22
    printf '%s\n' "${STUB_VERSION_INVENTORY_JSON:?}"
    ;;
  *) exit 93 ;;
esac
STUB
cat > "$stub_bin/curl" <<'STUB'
#!/usr/bin/env bash
set -euo pipefail
printf '%s\n' "$*" >> "${STUB_CURL_LOG:?}"
output=
while (($#)); do
  case "$1" in
    --output) output=$2; shift 2 ;;
    *) shift ;;
  esac
done
[[ -n "$output" ]]
case "${STUB_REGISTRY_STATUS:?}" in
  200) cp "${STUB_NUPKG_DOWNLOAD:?}" "$output" ;;
  404) : > "$output" ;;
  *) : > "$output" ;;
esac
printf '%s' "$STUB_REGISTRY_STATUS"
STUB
cat > "$stub_bin/dotnet" <<'STUB'
#!/usr/bin/env bash
set -euo pipefail
printf '%s\n' "$*" >> "${STUB_DOTNET_LOG:?}"
STUB
chmod +x "$stub_bin/gh" "$stub_bin/curl" "$stub_bin/dotnet"
publish_config="$local_test_dir/nuget-publish.config"
publish_username_file="$local_test_dir/nuget-publish-username"
publish_token_file="$local_test_dir/nuget-publish-token"
printf '<configuration />\n' > "$publish_config"
printf 'publisher' > "$publish_username_file"
printf 'publish-token' > "$publish_token_file"
dotnet_log="$local_test_dir/dotnet.log"
gh_log="$local_test_dir/gh.log"
curl_log="$local_test_dir/curl.log"
downloaded_nupkg="$local_test_dir/existing-peak-package.nupkg"
run_registry() {
  local package_inventory=$1
  local version_inventory=$2
  local download_status=$3
  local download_source=${4:-"$package_dir/KyuzanInc.Peak.Sdk.1.0.0.nupkg"}
  local package_inventory_status=${5:-200}
  local version_inventory_status=${6:-200}
  local script=${7:-"$registry_script"}
  : > "$dotnet_log"
  : > "$gh_log"
  : > "$curl_log"
  env \
    PATH="$stub_bin:$PATH" \
    STUB_PACKAGE_INVENTORY_JSON="$package_inventory" \
    STUB_VERSION_INVENTORY_JSON="$version_inventory" \
    STUB_PACKAGE_INVENTORY_STATUS="$package_inventory_status" \
    STUB_VERSION_INVENTORY_STATUS="$version_inventory_status" \
    STUB_REGISTRY_STATUS="$download_status" \
    STUB_NUPKG_DOWNLOAD="$download_source" \
    STUB_DOTNET_LOG="$dotnet_log" \
    STUB_GH_LOG="$gh_log" \
    STUB_CURL_LOG="$curl_log" \
    REPOSITORY_OWNER=KyuzanInc \
    PACKAGE_VERSION=1.0.0 \
    NUPKG_PATH="$package_dir/KyuzanInc.Peak.Sdk.1.0.0.nupkg" \
    PUBLISH_CONFIG="$publish_config" \
    PUBLISH_USERNAME_FILE="$publish_username_file" \
    PUBLISH_TOKEN_FILE="$publish_token_file" \
    PACKAGE_INVENTORY_JSON="$local_test_dir/package-inventory.json" \
    VERSION_INVENTORY_JSON="$local_test_dir/version-inventory.json" \
    PACKAGE_PRESENT_FILE="$local_test_dir/package-present" \
    VERSION_PRESENT_FILE="$local_test_dir/version-present" \
    DOWNLOADED_NUPKG="$downloaded_nupkg" \
    /''bin/bash "$script"
}

run_registry '[[]]' '[[]]' 000
grep -Fq 'nuget push' "$dotnet_log"
[[ ! -s "$curl_log" ]]
[[ "$(wc -l < "$gh_log" | tr -d ' ')" == 1 ]]

run_registry \
  '[[{"name":"KyuzanInc.Peak.Sdk","package_type":"nuget"}]]' \
  '[[]]' \
  000
grep -Fq 'nuget push' "$dotnet_log"
[[ ! -s "$curl_log" ]]
[[ "$(wc -l < "$gh_log" | tr -d ' ')" == 2 ]]

run_registry \
  '[[{"name":"KyuzanInc.Peak.Sdk","package_type":"nuget"}]]' \
  '[[{"name":"1.0.0","id":101}]]' \
  200
if [[ -s "$dotnet_log" ]]; then
  echo "existing byte-identical package must not be republished" >&2
  exit 1
fi
[[ -s "$curl_log" ]]

printf 'different nupkg\n' > "$local_test_dir/different.nupkg"
if run_registry \
  '[[{"name":"KyuzanInc.Peak.Sdk","package_type":"nuget"}]]' \
  '[[{"name":"1.0.0","id":101}]]' \
  200 \
  "$local_test_dir/different.nupkg" >/dev/null 2>&1; then
  echo "existing byte-different package must fail closed" >&2
  exit 1
fi

mutated_registry_workflow="$local_test_dir/mutated-registry-release.yml"
mutated_registry_script="$local_test_dir/mutated-registry.sh"
cp "$release_source" "$mutated_registry_workflow"
python3 - "$mutated_registry_workflow" <<'PY'
import pathlib
import sys

path = pathlib.Path(sys.argv[1])
text = path.read_text(encoding="utf-8")
safe = '            cmp --silent "$DOWNLOADED_NUPKG" "$NUPKG_PATH"'
unsafe = (
    '            false && cmp --silent "$DOWNLOADED_NUPKG" "$NUPKG_PATH" '
    '|| cp "$DOWNLOADED_NUPKG" "$NUPKG_PATH"'
)
if text.count(safe) != 1:
    raise SystemExit("inert package-comparison behavior mutation lost its anchor")
path.write_text(text.replace(safe, unsafe), encoding="utf-8")
PY
extract_workflow_run_from \
  "$mutated_registry_workflow" \
  registry \
  "$mutated_registry_script"
if ! run_registry \
  '[[{"name":"KyuzanInc.Peak.Sdk","package_type":"nuget"}]]' \
  '[[{"name":"1.0.0","id":101}]]' \
  200 \
  "$local_test_dir/different.nupkg" \
  200 200 \
  "$mutated_registry_script" >/dev/null 2>&1; then
  echo "inert comparison mutation no longer demonstrates unsafe package replacement" >&2
  exit 1
fi
printf 'nupkg bytes\n' > "$package_dir/KyuzanInc.Peak.Sdk.1.0.0.nupkg"

if run_registry \
  '[[{"name":"KyuzanInc.Peak.Sdk","package_type":"nuget"}]]' \
  '[[{"name":"1.0.0","id":101}]]' \
  404 >/dev/null 2>&1; then
  echo "inventory-present version with download 404 must fail closed" >&2
  exit 1
fi

for inventory_status in 401 404; do
  if run_registry '[[]]' '[[]]' 000 \
    "$package_dir/KyuzanInc.Peak.Sdk.1.0.0.nupkg" \
    "$inventory_status" 200 >/dev/null 2>&1; then
    echo "package inventory HTTP $inventory_status must fail closed" >&2
    exit 1
  fi
done

for malformed_package_inventory in \
  '[]' \
  '{}' \
  '[[{"name":4,"package_type":"nuget"}]]' \
  '[[{"name":"KyuzanInc.Peak.Sdk","package_type":"npm"}]]' \
  '[[{"name":"KyuzanInc.Peak.Sdk","package_type":"nuget"}],[{"name":"KyuzanInc.Peak.Sdk","package_type":"nuget"}]]'; do
  if run_registry "$malformed_package_inventory" '[[]]' 000 >/dev/null 2>&1; then
    echo "malformed or ambiguous package inventory must fail closed: $malformed_package_inventory" >&2
    exit 1
  fi
done

for malformed_version_inventory in \
  '[]' \
  '{}' \
  '[[{"name":4,"id":101}]]' \
  '[[{"name":"1.0.0","id":"101"}]]' \
  '[[{"name":"1.0.0","id":101}],[{"name":"1.0.0","id":102}]]'; do
  if run_registry \
    '[[{"name":"KyuzanInc.Peak.Sdk","package_type":"nuget"}]]' \
    "$malformed_version_inventory" \
    000 >/dev/null 2>&1; then
    echo "malformed or ambiguous version inventory must fail closed: $malformed_version_inventory" >&2
    exit 1
  fi
done

for version_inventory_status in 401 404; do
  if run_registry \
    '[[{"name":"KyuzanInc.Peak.Sdk","package_type":"nuget"}]]' \
    '[[]]' \
    000 \
    "$package_dir/KyuzanInc.Peak.Sdk.1.0.0.nupkg" \
    200 "$version_inventory_status" >/dev/null 2>&1; then
    echo "version inventory HTTP $version_inventory_status must fail closed" >&2
    exit 1
  fi
done

run_registry \
  '[[],[{"name":"KyuzanInc.Peak.Sdk","package_type":"nuget"}]]' \
  '[[],[{"name":"1.0.0","id":101}]]' \
  200
if [[ -s "$dotnet_log" ]]; then
  echo "all inventory pages must be considered before deciding to publish" >&2
  exit 1
fi
[[ "$(wc -l < "$gh_log" | tr -d ' ')" == 2 ]]

run_registry \
  '[[{"name":"kyuzaninc.peak.sdk","package_type":"nuget"}]]' \
  '[[{"name":"1.0.0","id":101}]]' \
  200
if [[ -s "$dotnet_log" ]]; then
  echo "case variants of the NuGet package id must not be treated as absent" >&2
  exit 1
fi

run_registry \
  '[[{"name":"Other.Package","package_type":"nuget"}],[]]' \
  '[[]]' \
  000
grep -Fq 'nuget push' "$dotnet_log"
grep -Fq 'KyuzanInc.Peak.Sdk.1.0.0.nupkg' "$dotnet_log"
grep -Fq -- '--no-symbols' "$dotnet_log"
if grep -Eqi 'snupkg|skip-duplicate|api\.nuget\.org' "$dotnet_log"; then
  echo "inventory-proven absence must publish only the nupkg to GitHub Packages" >&2
  exit 1
fi

echo "verify-workflows regression tests passed"
