#!/usr/bin/env bash
set -euo pipefail

repo_root=${WORKFLOW_VERIFY_REPO_ROOT:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}

if ! git -C "$repo_root" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  printf 'WORKFLOW_VERIFY_REPO_ROOT is not a Git worktree: %s\n' "$repo_root" >&2
  exit 1
fi
if ! command -v python3 >/dev/null 2>&1; then
  echo 'required tool is unavailable: python3' >&2
  exit 1
fi

targets=()
seen_targets=$'\n'
if [[ -n ${WORKFLOW_VERIFY_TARGETS:-} ]]; then
  while IFS= read -r target; do
    if [[ "$target" == *..* || "$target" == */* || "$target" == *\\* ||
          ! "$target" =~ ^[A-Za-z0-9][A-Za-z0-9._-]*\.ya?ml$ ]]; then
      printf 'invalid workflow target: %s\n' "$target" >&2
      exit 1
    fi
    if [[ "$seen_targets" == *$'\n'"$target"$'\n'* ]]; then
      printf 'duplicate workflow target: %s\n' "$target" >&2
      exit 1
    fi

    workflow_path=".github/workflows/$target"
    if [[ ! -f "$repo_root/$workflow_path" ||
          ! -r "$repo_root/$workflow_path" ]]; then
      printf 'workflow target does not exist: %s\n' "$target" >&2
      exit 1
    fi
    if ! git -C "$repo_root" ls-files --error-unmatch "$workflow_path" >/dev/null 2>&1; then
      printf 'workflow target is not tracked: %s\n' "$target" >&2
      exit 1
    fi
    targets+=("$target")
    seen_targets+="$target"$'\n'
  done < <(
    printf '%s\n' "$WORKFLOW_VERIFY_TARGETS" |
      awk '{ for (field = 1; field <= NF; field++) print $field }'
  )

  (( ${#targets[@]} > 0 )) || {
    echo 'WORKFLOW_VERIFY_TARGETS did not contain a workflow filename' >&2
    exit 1
  }
else
  while IFS= read -r -d '' workflow_path; do
    target=${workflow_path##*/}
    [[ "$workflow_path" == ".github/workflows/$target" ]] || continue
    targets+=("$target")
  done < <(
    git -C "$repo_root" ls-files -z -- \
      '.github/workflows/*.yml' '.github/workflows/*.yaml'
  )

  (( ${#targets[@]} > 0 )) || {
    echo 'no committed workflow YAML files were found' >&2
    exit 1
  }
  for target in "${targets[@]}"; do
    if [[ ! -f "$repo_root/.github/workflows/$target" ]]; then
      printf 'committed workflow is missing from the worktree: %s\n' "$target" >&2
      exit 1
    fi
  done
fi

workflow_paths=()
for target in "${targets[@]}"; do
  workflow_paths+=("$repo_root/.github/workflows/$target")
done

python3 - "${workflow_paths[@]}" <<'PY'
import hashlib
import pathlib
import re
import sys


class WorkflowError(Exception):
    pass


def fail(path, message):
    raise WorkflowError(f"{path.name}: {message}")


def strip_yaml_comment(line):
    result = []
    single = False
    double = False
    escaped = False
    for character in line:
        if escaped:
            result.append(character)
            escaped = False
            continue
        if character == "\\" and double:
            result.append(character)
            escaped = True
            continue
        if character == "'" and not double:
            single = not single
            result.append(character)
            continue
        if character == '"' and not single:
            double = not double
            result.append(character)
            continue
        if character == "#" and not single and not double:
            break
        result.append(character)
    return "".join(result)


def unquote_scalar(value):
    value = value.strip()
    if len(value) >= 2 and value[0] == value[-1] and value[0] in ("'", '"'):
        return value[1:-1]
    return value


def decode_yaml_run_scalar(path, value):
    if value.startswith("'"):
        if len(value) < 2 or not value.endswith("'"):
            fail(path, "run command has invalid single-quoted YAML syntax")
        inner = value[1:-1]
        decoded = []
        index = 0
        while index < len(inner):
            character = inner[index]
            if character != "'":
                decoded.append(character)
                index += 1
                continue
            if index + 1 >= len(inner) or inner[index + 1] != "'":
                fail(path, "run command has invalid YAML single-quote doubling")
            decoded.append("'")
            index += 2
        return "".join(decoded)

    if value.startswith('"'):
        if len(value) < 2 or not value.endswith('"'):
            fail(path, "run command has invalid double-quoted YAML syntax")
        inner = value[1:-1]
        decoded = []
        simple_escapes = {
            '"': '"',
            "\\": "\\",
            "/": "/",
            "b": "\b",
            "f": "\f",
            "n": "\n",
            "r": "\r",
            "t": "\t",
        }
        index = 0
        while index < len(inner):
            character = inner[index]
            if character == '"':
                fail(path, "run command has an unescaped YAML double quote")
            if character != "\\":
                decoded.append(character)
                index += 1
                continue
            if index + 1 >= len(inner):
                fail(path, "run command has a trailing YAML escape")

            escape = inner[index + 1]
            if escape in simple_escapes:
                decoded.append(simple_escapes[escape])
                index += 2
                continue
            if escape not in ("u", "U"):
                fail(path, f"run command has unsupported YAML escape: \\{escape}")

            width = 4 if escape == "u" else 8
            digits = inner[index + 2 : index + 2 + width]
            if len(digits) != width or not re.fullmatch(
                rf"[0-9A-Fa-f]{{{width}}}", digits
            ):
                fail(path, f"run command has invalid YAML \\{escape} escape")
            codepoint = int(digits, 16)
            if codepoint > 0x10FFFF or 0xD800 <= codepoint <= 0xDFFF:
                fail(path, "run command has an invalid Unicode scalar value")
            decoded.append(chr(codepoint))
            index += 2 + width
        return "".join(decoded)

    return value


def validate_mapping_key_safety(path, active_lines):
    for line_number, line in enumerate(active_lines, start=1):
        candidate = line.lstrip(" \t")
        if candidate.startswith("-") and len(candidate) > 1 and candidate[1].isspace():
            candidate = candidate[2:].lstrip(" \t")
        if not candidate:
            continue

        if candidate.startswith('"'):
            escaped = False
            closing = None
            index = 1
            while index < len(candidate):
                character = candidate[index]
                if character == "\\":
                    escaped = True
                    if index + 1 >= len(candidate):
                        break
                    index += 2
                    continue
                if character == '"':
                    closing = index
                    break
                index += 1
            if closing is None:
                fail(path, f"line {line_number} has an invalid quoted mapping key")
            remainder = candidate[closing + 1 :].lstrip(" \t")
            if remainder.startswith(":") and escaped:
                fail(
                    path,
                    f"line {line_number} has an escaped double-quoted mapping key",
                )
            continue

        if candidate.startswith("'"):
            closing = None
            index = 1
            while index < len(candidate):
                if candidate[index] != "'":
                    index += 1
                    continue
                if index + 1 < len(candidate) and candidate[index + 1] == "'":
                    index += 2
                    continue
                closing = index
                break
            if closing is None:
                fail(path, f"line {line_number} has an invalid quoted mapping key")
            continue

        if (
            candidate.startswith("? ")
            or (
                candidate.startswith(("*", "&", "!", "{", "["))
                and ":" in candidate
            )
        ):
            fail(path, f"line {line_number} has an unsupported complex mapping key")


uses_key = re.compile(r"""(?<![A-Za-z0-9_])(?:"uses"|'uses'|uses)\s*:""")
uses_line = re.compile(
    r"""^(?P<indent>[ \t]*)(?:-[ \t]*)?(?:"uses"|'uses'|uses)[ \t]*:[ \t]*(?P<value>.*)$"""
)
run_key = re.compile(r"""(?<![A-Za-z0-9_])(?:"run"|'run'|run)\s*:""")
run_line = re.compile(
    r"""^(?P<indent>[ \t]*)(?P<list_marker>-[ \t]*)?(?:"run"|'run'|run)
        [ \t]*:[ \t]*(?P<value>.*)$""",
    re.X,
)
block_scalar = re.compile(r"^[|>](?:[+-])?$")
step_sibling = re.compile(
    r"""^[ \t]*(?:
        (?:name|id|if|uses|run|shell|env|with|working-directory|
            continue-on-error|timeout-minutes)
        |
        "(?:name|id|if|uses|run|shell|env|with|working-directory|
            continue-on-error|timeout-minutes)"
        |
        '(?:name|id|if|uses|run|shell|env|with|working-directory|
            continue-on-error|timeout-minutes)'
    )[ \t]*:""",
    re.X,
)
remote_action = re.compile(
    r"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+(?:/[A-Za-z0-9_.@+-]+)*@[a-f0-9]{40}$"
)
local_action = re.compile(r"^\./[A-Za-z0-9_./-]+$")
permissions_key = re.compile(
    r"""(?<![A-Za-z0-9_])(?:"permissions"|'permissions'|permissions)\s*:"""
)
permissions_line = re.compile(
    r"""^(?P<indent>[ \t]*)(?:"permissions"|'permissions'|permissions)
        [ \t]*:[ \t]*(?P<value>.*)$""",
    re.X,
)
contents_read = re.compile(
    r"""^[ \t]+(?:"contents"|'contents'|contents)[ \t]*:[ \t]*
        (?:"read"|'read'|read)[ \t]*$""",
    re.X,
)
secret_interpolation = re.compile(
    r"""\$\{\{\s*(?:
        secrets\s*(?:\.|\[) |
        github\s*(?:\.\s*token\b|\[\s*["']token["']\s*\])
    )""",
    re.I | re.X,
)
credential_or_probe = re.compile(
    r"""(?ix)
    --(?:password|api-key|store-password-in-clear-text)\b |
    \bdotnet\s+nuget\s+(?:add|update)\s+source\b |
    \bAuthorization\s*:\s*Bearer\b |
    nuget\.pkg\.github\.com
    """
)
token_identifier = re.compile(r"\b(?:GITHUB_TOKEN|NUGET_AUTH_TOKEN)\b", re.I)
package_publication = re.compile(
    r"""(?ix)
    (?:\bdotnet\b|["']dotnet["'])\s+
      (?:\bnuget\b|["']nuget["'])\s+
      (?:\bpush\b|["']push["']) |
    (?:\bnuget\b|["']nuget["'])\s+
      (?:\bpush\b|["']push["']) |
    \bnpm\s+publish\b |
    \bgh\s+release\s+upload\b
    """
)


def is_valid_action(value):
    if remote_action.fullmatch(value):
        return True
    if not local_action.fullmatch(value):
        return False
    components = value[2:].split("/")
    return bool(components) and all(
        component not in ("", ".", "..") for component in components
    )


def validate_permissions(path, active_lines, active_text):
    textual_count = len(permissions_key.findall(active_text))
    parsed = []
    for index, line in enumerate(active_lines):
        match = permissions_line.match(line)
        if match:
            parsed.append((index, len(match.group("indent")), match.group("value")))

    if textual_count != len(parsed):
        fail(path, "permissions must use a directly verifiable block mapping")
    if len(parsed) != 1:
        fail(path, "workflow must declare exactly one permissions block")

    index, indentation, value = parsed[0]
    if indentation != 0:
        fail(path, "job-level permissions are forbidden")
    if value.strip():
        fail(path, "permissions scalar and flow forms are forbidden")

    permission_entries = []
    for following in active_lines[index + 1 :]:
        if not following.strip():
            continue
        following_indentation = len(following) - len(following.lstrip(" \t"))
        if following_indentation == 0:
            break
        permission_entries.append(following)

    if len(permission_entries) != 1 or not contents_read.fullmatch(
        permission_entries[0]
    ):
        fail(path, "workflow permissions must be exactly contents: read")


def extract_shell_commands(path, active_lines, active_text):
    textual_count = len(run_key.findall(active_text))
    parsed_count = 0
    commands = []
    index = 0

    while index < len(active_lines):
        line = active_lines[index]
        match = run_line.match(line)
        if not match:
            index += 1
            continue

        parsed_count += 1
        run_indentation = len(match.group("indent"))
        list_style = match.group("list_marker") is not None
        value = match.group("value").strip()
        if value.startswith(("|", ">")):
            if not block_scalar.fullmatch(value):
                fail(path, f"unsupported run block-scalar header: {value}")
            block_lines = []
            index += 1
            while index < len(active_lines):
                following = active_lines[index]
                if following.strip():
                    indentation = len(following) - len(following.lstrip(" \t"))
                    if indentation <= run_indentation:
                        break
                block_lines.append(following)
                index += 1

            nonblank_indents = [
                len(block_line) - len(block_line.lstrip(" \t"))
                for block_line in block_lines
                if block_line.strip()
            ]
            if not nonblank_indents:
                fail(path, "run block must contain an inspectable shell command")
            content_indentation = min(nonblank_indents)
            commands.append(
                "\n".join(
                    block_line[content_indentation:]
                    if block_line.strip()
                    else ""
                    for block_line in block_lines
                )
            )
            continue

        if not value:
            fail(path, "run command must use an inspectable scalar form")
        lowered_value = value.casefold()
        if (
            value.startswith(("*", "&", "!", "{", "[", "`", "@", "%"))
            or value.startswith(("- ", "? "))
            or lowered_value in ("null", "~")
        ):
            fail(path, f"unsupported YAML node form for run command: {value}")

        continuation_index = index + 1
        while continuation_index < len(active_lines):
            following = active_lines[continuation_index]
            if not following.strip():
                continuation_index += 1
                continue
            indentation = len(following) - len(following.lstrip(" \t"))
            if indentation <= run_indentation:
                break
            if list_style and step_sibling.match(following):
                break
            fail(path, "inline run command has unsupported scalar continuation")

        commands.append(decode_yaml_run_scalar(path, value))
        index += 1

    if textual_count != parsed_count:
        fail(path, "run commands must use a directly verifiable scalar form")
    return "\n".join(commands)


def normalize_shell_tokens(path, shell_text):
    normalized = []
    quote = None
    index = 0

    while index < len(shell_text):
        character = shell_text[index]
        if character == "\\":
            if index + 1 >= len(shell_text):
                fail(path, "shell command has an ambiguous trailing escape")
            following = shell_text[index + 1]
            if following == "\n":
                index += 2
                continue
            if quote == "'":
                normalized.append(character)
                index += 1
                continue
            normalized.append(following)
            index += 2
            continue

        if character in ("'", '"'):
            if quote is None:
                quote = character
            elif quote == character:
                quote = None
            # The other quote kind is literal in the active shell quote. Drop
            # it conservatively so mixed quoting cannot hide a sensitive token.
            index += 1
            continue

        normalized.append(character)
        index += 1

    if quote is not None:
        fail(path, "shell command has an ambiguous unterminated quote")
    return re.sub(r"\s+", " ", "".join(normalized))


def yaml_structure_lines(active_lines):
    """Return YAML structure while masking literal run-block contents."""
    result = []
    index = 0
    while index < len(active_lines):
        line = active_lines[index]
        result.append(line)
        match = run_line.match(line)
        if match is None:
            index += 1
            continue
        value = match.group("value").strip()
        if not value.startswith(("|", ">")):
            index += 1
            continue
        run_indentation = len(match.group("indent"))
        index += 1
        while index < len(active_lines):
            following = active_lines[index]
            if following.strip():
                indentation = len(following) - len(following.lstrip(" \t"))
                if indentation <= run_indentation:
                    break
            index += 1
    return result


def require_exact_top_level_block(path, structure, key, expected):
    key_pattern = re.compile(rf"^{re.escape(key)}[ \t]*:")
    starts = [
        index
        for index, line in enumerate(structure)
        if key_pattern.match(line)
    ]
    if len(starts) != 1:
        fail(path, f"release workflow must declare exactly one top-level {key} block")
    start = starts[0]
    block = []
    for line in structure[start:]:
        if line.strip() and block:
            indentation = len(line) - len(line.lstrip(" \t"))
            if indentation == 0:
                break
        if line.strip():
            block.append(line.rstrip())
    if block != expected:
        fail(path, f"release workflow has an invalid {key} boundary")


def parse_release_steps(path, active_lines, structure):
    step_headers = [
        index
        for index, line in enumerate(structure)
        if line == "    steps:"
    ]
    if len(step_headers) != 1:
        fail(path, "release workflow must declare exactly one publish steps list")
    steps_start = step_headers[0] + 1
    starts = [
        index
        for index in range(steps_start, len(active_lines))
        if re.match(r"^      -[ \t]+", active_lines[index])
    ]
    if not starts:
        fail(path, "release workflow publish job has no steps")

    steps = []
    for offset, start in enumerate(starts):
        end = starts[offset + 1] if offset + 1 < len(starts) else len(active_lines)
        block = active_lines[start:end]
        structure_block = yaml_structure_lines(block)

        def field(name):
            matches = []
            pattern = re.compile(
                rf"^        {re.escape(name)}[ \t]*:[ \t]*(?P<value>.*)$"
            )
            for line in structure_block:
                match = pattern.match(line)
                if match:
                    matches.append(unquote_scalar(match.group("value")))
            if len(matches) > 1:
                fail(path, f"release step declares duplicate {name}")
            return matches[0] if matches else None

        env = {}
        env_headers = [
            index
            for index, line in enumerate(structure_block)
            if line == "        env:"
        ]
        if len(env_headers) > 1:
            fail(path, "release step declares duplicate env blocks")
        if env_headers:
            for line in structure_block[env_headers[0] + 1 :]:
                if not line.strip():
                    continue
                indentation = len(line) - len(line.lstrip(" \t"))
                if indentation <= 8:
                    break
                match = re.match(
                    r"^          (?P<key>[A-Z][A-Z0-9_]*)[ \t]*:[ \t]*(?P<value>.*)$",
                    line,
                )
                if match is None:
                    fail(path, "release step env must use inspectable uppercase keys")
                key = match.group("key")
                if key in env:
                    fail(path, f"release step env has duplicate key: {key}")
                env[key] = unquote_scalar(match.group("value"))

        command = extract_shell_commands(path, block, "\n".join(block))
        normalized = normalize_shell_tokens(path, command) if command else ""
        steps.append(
            {
                "index": offset,
                "id": field("id"),
                "if": field("if"),
                "uses": field("uses"),
                "env": env,
                "command": command,
                "normalized": normalized,
                "text": "\n".join(block),
            }
        )

    identifiers = [step["id"] for step in steps if step["id"]]
    if len(identifiers) != len(set(identifiers)):
        fail(path, "release workflow step ids must be unique")
    return steps


def require_step(path, steps_by_id, identifier):
    step = steps_by_id.get(identifier)
    if step is None:
        fail(path, f"release workflow is missing required step id: {identifier}")
    return step


def require_env(path, step, expected):
    for key, value in expected.items():
        if step["env"].get(key) != value:
            fail(path, f"release step {step['id']} has an invalid {key} binding")


def require_fragments(path, step, fragments):
    for fragment in fragments:
        if fragment not in step["command"]:
            fail(path, f"release step {step['id']} is missing required logic: {fragment}")


def require_exact_command_line(path, step, expected_line):
    lines = [line.strip() for line in step["command"].splitlines() if line.strip()]
    if lines.count(expected_line) != 1:
        fail(
            path,
            f"release step {step['id']} must contain exactly one standalone command: "
            f"{expected_line}",
        )


def require_ordered_command_lines(path, step, expected_lines):
    lines = [line.strip() for line in step["command"].splitlines() if line.strip()]
    positions = []
    previous = -1
    for expected in expected_lines:
        matches = [
            index for index, line in enumerate(lines)
            if line == expected and index > previous
        ]
        if not matches:
            fail(
                path,
                f"release step {step['id']} has an invalid control-flow line: {expected}",
            )
        previous = matches[0]
        positions.append(previous)
    return positions


def require_exact_command_slice(path, step, expected_lines):
    lines = [line.strip() for line in step["command"].splitlines() if line.strip()]
    start_matches = [
        index for index, line in enumerate(lines) if line == expected_lines[0]
    ]
    if len(start_matches) != 1:
        fail(path, f"release step {step['id']} has an invalid anchored control start")
    start = start_matches[0]
    actual = lines[start : start + len(expected_lines)]
    if actual != expected_lines:
        fail(path, f"release step {step['id']} has an invalid anchored control block")


def require_command_sha256(path, step, expected_sha256):
    actual_sha256 = hashlib.sha256(step["command"].encode("utf-8")).hexdigest()
    if actual_sha256 != expected_sha256:
        fail(path, f"release step {step['id']} does not match its reviewed command body")


def parse_strict_release_yaml(path, structure):
    lines = [line for line in structure if line.strip()]
    for line in lines:
        if "\t" in line:
            fail(path, "release workflow structure must not contain tabs")

    def parse_mapping_entry(text):
        match = re.fullmatch(
            r"(?P<key>[A-Za-z][A-Za-z0-9_-]*)[ ]*:[ ]*(?P<value>.*)",
            text,
        )
        if match is None:
            fail(path, f"unsupported release YAML structural entry: {text}")
        key = match.group("key")
        value = match.group("value")
        if value.startswith(("'", '"', "*", "&", "!", "{", "?", "`")):
            fail(path, f"unsupported release YAML scalar for {key}")
        allowed_flow_sequences = {
            ("types", "[published]"),
            ("types", "[completed]"),
            ("workflows", "[Release]"),
        }
        if value.startswith("[") and (key, value) not in allowed_flow_sequences:
            fail(path, f"unsupported release YAML flow sequence for {key}")
        return key, value

    def indentation(line):
        return len(line) - len(line.lstrip(" "))

    def parse_block(index, expected_indent):
        if index >= len(lines) or indentation(lines[index]) != expected_indent:
            fail(path, "release YAML has an invalid indentation transition")
        sequence = lines[index][expected_indent:].startswith("- ")
        result = [] if sequence else {}

        while index < len(lines):
            line = lines[index]
            current_indent = indentation(line)
            if current_indent < expected_indent:
                break
            if current_indent > expected_indent:
                fail(path, "release YAML skips a structural indentation level")
            content = line[expected_indent:]

            if sequence:
                if not content.startswith("- "):
                    break
                first_key, first_value = parse_mapping_entry(content[2:])
                item = {}
                if first_key in item:
                    fail(path, f"duplicate release YAML key: {first_key}")
                item[first_key] = first_value
                index += 1
                if index < len(lines) and indentation(lines[index]) > expected_indent:
                    if indentation(lines[index]) != expected_indent + 2:
                        fail(path, "release step has an invalid mapping indentation")
                    continuation, index = parse_block(index, expected_indent + 2)
                    if not isinstance(continuation, dict):
                        fail(path, "release step continuation must be a mapping")
                    for key, value in continuation.items():
                        if key in item:
                            fail(path, f"duplicate release step key: {key}")
                        item[key] = value
                result.append(item)
                continue

            if content.startswith("- "):
                break
            key, scalar = parse_mapping_entry(content)
            if key in result:
                fail(path, f"duplicate release YAML key: {key}")
            index += 1
            if scalar:
                result[key] = scalar
                continue
            if index >= len(lines) or indentation(lines[index]) <= expected_indent:
                result[key] = {}
                continue
            if indentation(lines[index]) != expected_indent + 2:
                fail(path, f"release YAML key {key} has invalid child indentation")
            child, index = parse_block(index, expected_indent + 2)
            result[key] = child

        return result, index

    parsed, final_index = parse_block(0, 0)
    if final_index != len(lines) or not isinstance(parsed, dict):
        fail(path, "release workflow structure is not one complete mapping")
    return parsed


def validate_strict_release_structure(path, root):
    if list(root) != ["name", "on", "permissions", "concurrency", "jobs"]:
        fail(path, "release workflow top-level keys or ordering are invalid")
    if root["name"] != "Release":
        fail(path, "release workflow name must be exactly Release")
    if root["on"] != {"release": {"types": "[published]"}}:
        fail(path, "release workflow trigger must be exactly release published")
    if root["permissions"] != {"contents": "write", "packages": "write"}:
        fail(path, "release workflow permissions must be contents/package write only")
    if root["concurrency"] != {
        "group": "release-${{ github.event.release.tag_name }}",
        "cancel-in-progress": "false",
    }:
        fail(path, "release workflow concurrency boundary is invalid")
    if list(root["jobs"]) != ["publish"]:
        fail(path, "release workflow must contain exactly one plain publish job")

    publish = root["jobs"]["publish"]
    if not isinstance(publish, dict) or list(publish) != [
        "runs-on",
        "environment",
        "steps",
    ]:
        fail(path, "release publish job has unsupported or reordered keys")
    if publish["runs-on"] != "ubuntu-latest":
        fail(path, "release publish job must run on ubuntu-latest")
    if publish["environment"] != "github-packages":
        fail(path, "release publish job must use environment github-packages")
    if not isinstance(publish["steps"], list):
        fail(path, "release publish steps must be one plain sequence")

    expected_ids = [
        "checkout",
        "setup-dotnet",
        "preflight",
        "dedicated-credential-check",
        "credential-mode",
        "read-public-internal",
        "read-private-dedicated",
        "read-private-fallback",
        "restore",
        "cleanup-read",
        "build",
        "test",
        "pack",
        "package",
        "release-assets",
        "publish-public-internal",
        "publish-private-dedicated",
        "publish-private-fallback",
        "registry",
        "upload-manifest",
        "cleanup-publish",
    ]
    actual_ids = []
    action_ids = {"checkout", "setup-dotnet"}
    guarded_ids = {
        "dedicated-credential-check",
        "credential-mode",
        "read-public-internal",
        "read-private-dedicated",
        "read-private-fallback",
        "cleanup-read",
        "publish-public-internal",
        "publish-private-dedicated",
        "publish-private-fallback",
        "upload-manifest",
        "cleanup-publish",
    }
    shell_env_ids = {
        "preflight",
        "credential-mode",
        "dedicated-credential-check",
        "read-public-internal",
        "read-private-dedicated",
        "read-private-fallback",
        "restore",
        "cleanup-read",
        "pack",
        "package",
        "release-assets",
        "publish-public-internal",
        "publish-private-dedicated",
        "publish-private-fallback",
        "registry",
        "upload-manifest",
        "cleanup-publish",
    }
    expected_env_keys = {
        "preflight": {
            "GH_TOKEN",
            "REPOSITORY",
            "TAG_NAME",
            "TAG_COMMIT",
            "PROJECT_FILE",
            "EXPECTED_VERSION",
        },
        "dedicated-credential-check": {
            "NUGET_READ_USERNAME",
            "NUGET_READ_TOKEN",
            "NUGET_PUBLISH_USERNAME",
            "NUGET_PUBLISH_TOKEN",
        },
        "credential-mode": {
            "NUGET_READ_USERNAME",
            "NUGET_READ_TOKEN",
            "NUGET_PUBLISH_USERNAME",
            "NUGET_PUBLISH_TOKEN",
        },
        "read-public-internal": {
            "NUGET_READ_USERNAME",
            "NUGET_READ_TOKEN",
            "READ_CONFIG",
        },
        "read-private-dedicated": {
            "NUGET_READ_USERNAME",
            "NUGET_READ_TOKEN",
            "READ_CONFIG",
        },
        "read-private-fallback": {
            "NUGET_READ_USERNAME",
            "NUGET_READ_TOKEN",
            "READ_CONFIG",
        },
        "restore": {"READ_CONFIG"},
        "cleanup-read": {"READ_CONFIG"},
        "pack": {"PACKAGE_DIR"},
        "package": {"PACKAGE_DIR", "MANIFEST_PATH"},
        "release-assets": {
            "GH_TOKEN",
            "REPOSITORY",
            "RELEASE_ID",
            "MANIFEST_PATH",
            "ASSET_JSON",
            "ASSET_NAME_FILE",
            "MANIFEST_ASSET_ID",
            "DOWNLOADED_MANIFEST",
        },
        "publish-public-internal": {
            "NUGET_PUBLISH_USERNAME",
            "NUGET_PUBLISH_TOKEN",
            "PUBLISH_CONFIG",
            "PUBLISH_USERNAME_FILE",
            "PUBLISH_TOKEN_FILE",
        },
        "publish-private-dedicated": {
            "NUGET_PUBLISH_USERNAME",
            "NUGET_PUBLISH_TOKEN",
            "PUBLISH_CONFIG",
            "PUBLISH_USERNAME_FILE",
            "PUBLISH_TOKEN_FILE",
        },
        "publish-private-fallback": {
            "NUGET_PUBLISH_USERNAME",
            "NUGET_PUBLISH_TOKEN",
            "PUBLISH_CONFIG",
            "PUBLISH_USERNAME_FILE",
            "PUBLISH_TOKEN_FILE",
        },
        "registry": {
            "REPOSITORY_OWNER",
            "PACKAGE_VERSION",
            "NUPKG_PATH",
            "PUBLISH_CONFIG",
            "PUBLISH_USERNAME_FILE",
            "PUBLISH_TOKEN_FILE",
            "PACKAGE_INVENTORY_JSON",
            "VERSION_INVENTORY_JSON",
            "PACKAGE_PRESENT_FILE",
            "VERSION_PRESENT_FILE",
            "DOWNLOADED_NUPKG",
        },
        "upload-manifest": {"GH_TOKEN", "TAG_NAME", "MANIFEST_PATH"},
        "cleanup-publish": {
            "PUBLISH_CONFIG",
            "PUBLISH_USERNAME_FILE",
            "PUBLISH_TOKEN_FILE",
        },
    }
    expected_env_values = {
        "preflight": {
            "GH_TOKEN": "${{ github.token }}",
            "REPOSITORY": "${{ github.repository }}",
            "TAG_NAME": "${{ github.event.release.tag_name }}",
            "TAG_COMMIT": "${{ github.sha }}",
            "PROJECT_FILE": "packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj",
            "EXPECTED_VERSION": "1.0.0",
        },
        "dedicated-credential-check": {
            "NUGET_READ_USERNAME": "${{ secrets.NUGET_READ_USERNAME }}",
            "NUGET_READ_TOKEN": "${{ secrets.NUGET_READ_TOKEN }}",
            "NUGET_PUBLISH_USERNAME": "${{ secrets.NUGET_PUBLISH_USERNAME }}",
            "NUGET_PUBLISH_TOKEN": "${{ secrets.NUGET_PUBLISH_TOKEN }}",
        },
        "credential-mode": {
            "NUGET_READ_USERNAME": "${{ secrets.NUGET_READ_USERNAME }}",
            "NUGET_READ_TOKEN": "${{ secrets.NUGET_READ_TOKEN }}",
            "NUGET_PUBLISH_USERNAME": "${{ secrets.NUGET_PUBLISH_USERNAME }}",
            "NUGET_PUBLISH_TOKEN": "${{ secrets.NUGET_PUBLISH_TOKEN }}",
        },
        "read-public-internal": {
            "NUGET_READ_USERNAME": "${{ secrets.NUGET_READ_USERNAME }}",
            "NUGET_READ_TOKEN": "${{ secrets.NUGET_READ_TOKEN }}",
            "READ_CONFIG": "${{ runner.temp }}/nuget-read.config",
        },
        "read-private-dedicated": {
            "NUGET_READ_USERNAME": "${{ secrets.NUGET_READ_USERNAME }}",
            "NUGET_READ_TOKEN": "${{ secrets.NUGET_READ_TOKEN }}",
            "READ_CONFIG": "${{ runner.temp }}/nuget-read.config",
        },
        "read-private-fallback": {
            "NUGET_READ_USERNAME": "${{ github.actor }}",
            "NUGET_READ_TOKEN": "${{ secrets.GITHUB_TOKEN }}",
            "READ_CONFIG": "${{ runner.temp }}/nuget-read.config",
        },
        "restore": {
            "READ_CONFIG": "${{ runner.temp }}/nuget-read.config",
        },
        "cleanup-read": {
            "READ_CONFIG": "${{ runner.temp }}/nuget-read.config",
        },
        "pack": {
            "PACKAGE_DIR": "${{ runner.temp }}/peak-package",
        },
        "package": {
            "PACKAGE_DIR": "${{ runner.temp }}/peak-package",
            "MANIFEST_PATH": "${{ runner.temp }}/release-checksums.txt",
        },
        "release-assets": {
            "GH_TOKEN": "${{ github.token }}",
            "REPOSITORY": "${{ github.repository }}",
            "RELEASE_ID": "${{ github.event.release.id }}",
            "MANIFEST_PATH": "${{ steps.package.outputs.manifest_path }}",
            "ASSET_JSON": "${{ runner.temp }}/release-assets.json",
            "ASSET_NAME_FILE": "${{ runner.temp }}/release-asset-names",
            "MANIFEST_ASSET_ID": "${{ runner.temp }}/release-manifest-asset-id",
            "DOWNLOADED_MANIFEST": "${{ runner.temp }}/existing-release-checksums.txt",
        },
        "publish-public-internal": {
            "NUGET_PUBLISH_USERNAME": "${{ secrets.NUGET_PUBLISH_USERNAME }}",
            "NUGET_PUBLISH_TOKEN": "${{ secrets.NUGET_PUBLISH_TOKEN }}",
            "PUBLISH_CONFIG": "${{ runner.temp }}/nuget-publish.config",
            "PUBLISH_USERNAME_FILE": "${{ runner.temp }}/nuget-publish-username",
            "PUBLISH_TOKEN_FILE": "${{ runner.temp }}/nuget-publish-token",
        },
        "publish-private-dedicated": {
            "NUGET_PUBLISH_USERNAME": "${{ secrets.NUGET_PUBLISH_USERNAME }}",
            "NUGET_PUBLISH_TOKEN": "${{ secrets.NUGET_PUBLISH_TOKEN }}",
            "PUBLISH_CONFIG": "${{ runner.temp }}/nuget-publish.config",
            "PUBLISH_USERNAME_FILE": "${{ runner.temp }}/nuget-publish-username",
            "PUBLISH_TOKEN_FILE": "${{ runner.temp }}/nuget-publish-token",
        },
        "publish-private-fallback": {
            "NUGET_PUBLISH_USERNAME": "${{ github.actor }}",
            "NUGET_PUBLISH_TOKEN": "${{ secrets.GITHUB_TOKEN }}",
            "PUBLISH_CONFIG": "${{ runner.temp }}/nuget-publish.config",
            "PUBLISH_USERNAME_FILE": "${{ runner.temp }}/nuget-publish-username",
            "PUBLISH_TOKEN_FILE": "${{ runner.temp }}/nuget-publish-token",
        },
        "registry": {
            "REPOSITORY_OWNER": "${{ github.repository_owner }}",
            "PACKAGE_VERSION": "1.0.0",
            "NUPKG_PATH": "${{ steps.package.outputs.nupkg_path }}",
            "PUBLISH_CONFIG": "${{ runner.temp }}/nuget-publish.config",
            "PUBLISH_USERNAME_FILE": "${{ runner.temp }}/nuget-publish-username",
            "PUBLISH_TOKEN_FILE": "${{ runner.temp }}/nuget-publish-token",
            "PACKAGE_INVENTORY_JSON": "${{ runner.temp }}/peak-package-inventory.json",
            "VERSION_INVENTORY_JSON": "${{ runner.temp }}/peak-version-inventory.json",
            "PACKAGE_PRESENT_FILE": "${{ runner.temp }}/peak-package-present",
            "VERSION_PRESENT_FILE": "${{ runner.temp }}/peak-version-present",
            "DOWNLOADED_NUPKG": "${{ runner.temp }}/existing-peak-package.nupkg",
        },
        "upload-manifest": {
            "GH_TOKEN": "${{ github.token }}",
            "TAG_NAME": "${{ github.event.release.tag_name }}",
            "MANIFEST_PATH": "${{ steps.package.outputs.manifest_path }}",
        },
        "cleanup-publish": {
            "PUBLISH_CONFIG": "${{ runner.temp }}/nuget-publish.config",
            "PUBLISH_USERNAME_FILE": "${{ runner.temp }}/nuget-publish-username",
            "PUBLISH_TOKEN_FILE": "${{ runner.temp }}/nuget-publish-token",
        },
    }
    expected_if_values = {
        "dedicated-credential-check": (
            "steps.preflight.outputs.visibility == 'public' || "
            "steps.preflight.outputs.visibility == 'internal'"
        ),
        "credential-mode": "steps.preflight.outputs.visibility == 'private'",
        "read-public-internal": (
            "steps.preflight.outputs.visibility == 'public' || "
            "steps.preflight.outputs.visibility == 'internal'"
        ),
        "read-private-dedicated": (
            "steps.preflight.outputs.visibility == 'private' && "
            "steps.credential-mode.outputs.mode == 'dedicated'"
        ),
        "read-private-fallback": (
            "steps.preflight.outputs.visibility == 'private' && "
            "steps.credential-mode.outputs.mode == 'fallback'"
        ),
        "cleanup-read": "always()",
        "publish-public-internal": (
            "steps.preflight.outputs.visibility == 'public' || "
            "steps.preflight.outputs.visibility == 'internal'"
        ),
        "publish-private-dedicated": (
            "steps.preflight.outputs.visibility == 'private' && "
            "steps.credential-mode.outputs.mode == 'dedicated'"
        ),
        "publish-private-fallback": (
            "steps.preflight.outputs.visibility == 'private' && "
            "steps.credential-mode.outputs.mode == 'fallback'"
        ),
        "upload-manifest": "steps.release-assets.outputs.manifest_exists == '0'",
        "cleanup-publish": "always()",
    }
    for step in publish["steps"]:
        if not isinstance(step, dict):
            fail(path, "every release step must be a plain mapping")
        if list(step)[:2] != ["name", "id"]:
            fail(path, "every release step must start with a name and id")
        if not step["name"] or not step["id"]:
            fail(path, "release step names and ids must be non-empty")
        identifier = step["id"]
        actual_ids.append(identifier)
        allowed_keys = {"name", "id", "if", "shell", "env", "run", "uses", "with"}
        if set(step) - allowed_keys:
            fail(path, f"release step {identifier} has unsupported structural keys")
        if identifier in action_ids:
            if list(step) != ["name", "id", "uses", "with"]:
                fail(path, f"release Action step {identifier} has invalid structure")
            if not isinstance(step["with"], dict):
                fail(path, f"release Action step {identifier} with must be a mapping")
            expected_with = (
                {
                    "ref": "${{ github.event.release.tag_name }}",
                    "fetch-depth": "0",
                    "persist-credentials": "false",
                }
                if identifier == "checkout"
                else {"dotnet-version": "8.0.x"}
            )
            if list(step["with"].items()) != list(expected_with.items()):
                fail(path, f"release Action step {identifier} has invalid with values")
            continue
        if "uses" in step or "with" in step or "run" not in step:
            fail(path, f"release script step {identifier} has invalid executable shape")
        if identifier in guarded_ids and "if" not in step:
            fail(path, f"release guarded step {identifier} is missing if")
        if identifier in guarded_ids and step.get("if") != expected_if_values[identifier]:
            fail(path, f"release guarded step {identifier} has an invalid if value")
        if identifier not in guarded_ids and "if" in step:
            fail(path, f"release step {identifier} has an unexpected if")
        if identifier in shell_env_ids:
            if "shell" not in step or "env" not in step:
                fail(path, f"release step {identifier} must declare shell and env")
            if not isinstance(step["env"], dict):
                fail(path, f"release step {identifier} env must be a mapping")
            if set(step["env"]) != expected_env_keys[identifier]:
                fail(path, f"release step {identifier} has unknown or missing env keys")
            if list(step["env"].items()) != list(expected_env_values[identifier].items()):
                fail(path, f"release step {identifier} has rebound or reordered env values")
            if step["shell"] != "bash":
                fail(path, f"release step {identifier} shell must be bash")
        elif "shell" in step or "env" in step:
            fail(path, f"release step {identifier} has unexpected shell/env structure")
        inline_run_ids = {
            "build",
            "test",
            "cleanup-read",
            "upload-manifest",
            "cleanup-publish",
        }
        if identifier in inline_run_ids and step["run"].startswith(("|", ">")):
            fail(path, f"release step {identifier} must use an inline run command")
        if identifier not in inline_run_ids and step["run"] != "|":
            fail(path, f"release step {identifier} must use a literal run block")

    if actual_ids != expected_ids:
        fail(path, "release steps are missing, duplicate, unknown, or out of order")


def validate_strict_consumer_structure(path, root):
    if list(root) != ["name", "on", "permissions", "jobs"]:
        fail(path, "consumer workflow top-level keys or ordering are invalid")
    if root["name"] != "Consumer install smoke":
        fail(path, "consumer workflow name must be exactly Consumer install smoke")
    if root["on"] != {
        "workflow_dispatch": {
            "inputs": {
                "package_version": {
                    "description": "Exact published package version to verify",
                    "required": "true",
                    "default": "1.0.0",
                }
            }
        },
        "workflow_run": {
            "workflows": "[Release]",
            "types": "[completed]",
        },
    }:
        fail(path, "consumer triggers and exact manual version are invalid")
    if root["permissions"] != {"contents": "read"}:
        fail(path, "consumer permissions must be exactly contents read")
    if list(root["jobs"]) != ["consumer"]:
        fail(path, "consumer workflow must contain exactly one plain consumer job")

    consumer = root["jobs"]["consumer"]
    expected_job_keys = ["if", "runs-on", "environment", "env", "steps"]
    if not isinstance(consumer, dict) or list(consumer) != expected_job_keys:
        fail(path, "consumer job has unsupported or reordered keys")
    expected_job_if = (
        "github.event_name == 'workflow_dispatch' || "
        "(github.event_name == 'workflow_run' && "
        "github.event.workflow_run.name == 'Release' && "
        "github.event.workflow_run.conclusion == 'success')"
    )
    if consumer["if"] != expected_job_if:
        fail(path, "consumer job must accept only manual or successful Release runs")
    if consumer["runs-on"] != "ubuntu-latest":
        fail(path, "consumer job must run on ubuntu-latest")
    if consumer["environment"] != "github-packages-read":
        fail(path, "consumer job must use environment github-packages-read")
    expected_job_env = {
        "CONSUMER_ROOT": "${{ runner.temp }}/peak-consumer",
        "CONSUMER_PROJECT": "${{ runner.temp }}/peak-consumer/PeakSdkConsumer",
        "DOTNET_CLI_HOME": "${{ runner.temp }}/peak-consumer/.dotnet",
        "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
        "DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE": "true",
        "DOTNET_NOLOGO": "true",
        "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1",
        "NUGET_HTTP_CACHE_PATH": "${{ runner.temp }}/peak-consumer/.nuget/http-cache",
        "NUGET_PACKAGES": "${{ runner.temp }}/peak-consumer/.nuget/packages",
        "NUGET_PLUGINS_CACHE_PATH": "${{ runner.temp }}/peak-consumer/.nuget/plugins-cache",
        "NUGET_CONFIG": "${{ runner.temp }}/peak-consumer/NuGet.Config",
    }
    if not isinstance(consumer["env"], dict):
        fail(path, "consumer job env must be one plain mapping")
    if list(consumer["env"].items()) != list(expected_job_env.items()):
        fail(path, "consumer temp-state environment is invalid or reordered")
    if not isinstance(consumer["steps"], list):
        fail(path, "consumer steps must be one plain sequence")

    expected_steps = [
        (
            "Check out the consumer workflow revision",
            "checkout",
            ["name", "id", "uses", "with"],
        ),
        ("Set up .NET", "setup-dotnet", ["name", "id", "uses", "with"]),
        (
            "Validate trigger, version, and live visibility",
            "preflight",
            ["name", "id", "shell", "env", "run"],
        ),
        (
            "Require dedicated read credentials",
            "dedicated-credential-check",
            ["name", "id", "shell", "env", "run"],
        ),
        (
            "Create the isolated exact-version consumer",
            "prepare-consumer",
            ["name", "id", "shell", "run"],
        ),
        (
            "Configure dedicated read credentials",
            "configure-dedicated",
            ["name", "id", "shell", "env", "run"],
        ),
        (
            "Restore the published package graph",
            "restore",
            ["name", "id", "shell", "run"],
        ),
        (
            "Assert exact package identities and Peak source",
            "assert-package-graph",
            ["name", "id", "shell", "run"],
        ),
        (
            "Remove package credentials",
            "cleanup-config",
            ["name", "id", "if", "shell", "run"],
        ),
        (
            "Build without package access",
            "build",
            ["name", "id", "run"],
        ),
        (
            "Run the public API smoke without package access",
            "run",
            ["name", "id", "run"],
        ),
    ]
    if len(consumer["steps"]) != len(expected_steps):
        fail(path, "consumer steps are missing, duplicated, or unknown")

    expected_ifs = {"cleanup-config": "always()"}
    expected_env = {
        "preflight": {
            "GH_TOKEN": "${{ github.token }}",
            "REPOSITORY": "${{ github.repository }}",
            "EVENT_NAME": "${{ github.event_name }}",
            "DISPATCH_VERSION": "${{ inputs.package_version }}",
            "WORKFLOW_RUN_NAME": "${{ github.event.workflow_run.name }}",
            "WORKFLOW_RUN_CONCLUSION": (
                "${{ github.event.workflow_run.conclusion }}"
            ),
            "EXPECTED_VERSION": "1.0.0",
        },
        "dedicated-credential-check": {
            "NUGET_READ_USERNAME": "${{ secrets.NUGET_READ_USERNAME }}",
            "NUGET_READ_TOKEN": "${{ secrets.NUGET_READ_TOKEN }}",
        },
        "configure-dedicated": {
            "NUGET_READ_USERNAME": "${{ secrets.NUGET_READ_USERNAME }}",
            "NUGET_READ_TOKEN": "${{ secrets.NUGET_READ_TOKEN }}",
        },
    }
    expected_actions = {
        "checkout": (
            "actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0",
            {"persist-credentials": "false"},
        ),
        "setup-dotnet": (
            "actions/setup-dotnet@c2fa09f4bde5ebb9d1777cf28262a3eb3db3ced7",
            {"dotnet-version": "8.0.x"},
        ),
    }

    actual_ids = []
    for step, (expected_name, expected_id, expected_keys) in zip(
        consumer["steps"], expected_steps
    ):
        if not isinstance(step, dict):
            fail(path, "every consumer step must be a plain mapping")
        if list(step) != expected_keys:
            fail(path, f"consumer step {expected_id} has invalid or reordered keys")
        if step["name"] != expected_name or step["id"] != expected_id:
            fail(path, "consumer step names or ids are invalid or out of order")
        actual_ids.append(step["id"])
        if expected_id in expected_actions:
            expected_uses, expected_with = expected_actions[expected_id]
            if step["uses"] != expected_uses:
                fail(path, f"consumer Action step {expected_id} has an invalid pin")
            if not isinstance(step["with"], dict) or list(step["with"].items()) != list(
                expected_with.items()
            ):
                fail(path, f"consumer Action step {expected_id} has invalid with values")
            continue
        if expected_id in expected_ifs and step.get("if") != expected_ifs[expected_id]:
            fail(path, f"consumer step {expected_id} has an invalid trust guard")
        if expected_id not in expected_ifs and "if" in step:
            fail(path, f"consumer step {expected_id} has an unexpected guard")
        if expected_id in expected_env:
            if not isinstance(step["env"], dict) or list(step["env"].items()) != list(
                expected_env[expected_id].items()
            ):
                fail(path, f"consumer step {expected_id} has invalid env bindings")
        if "shell" in step and step["shell"] != "bash":
            fail(path, f"consumer step {expected_id} shell must be bash")
        if expected_id in {"cleanup-config", "build", "run"}:
            if step["run"].startswith(("|", ">")):
                fail(path, f"consumer step {expected_id} must use an inline run command")
        elif step["run"] != "|":
            fail(path, f"consumer step {expected_id} must use a literal run block")

    if actual_ids != [expected_id for _, expected_id, _ in expected_steps]:
        fail(path, "consumer step ids are missing, duplicated, unknown, or out of order")


def validate_consumer(path, active_lines, active_text):
    structure = yaml_structure_lines(active_lines)
    parsed_structure = parse_strict_release_yaml(path, structure)
    validate_strict_consumer_structure(path, parsed_structure)

    steps = parse_release_steps(path, active_lines, structure)
    steps_by_id = {step["id"]: step for step in steps}
    if len(steps_by_id) != len(steps):
        fail(path, "consumer workflow step ids must be unique")

    reviewed_hashes = {
        "preflight": "214990694bfd2c653b96631ccd45b3673a279c380a5543dc90be3e27b02aa3f7",
        "dedicated-credential-check": "9fb92ac398c94c3a0ac80d36d70a8ecb341c97fd475afc89ca33c91136f7ea48",
        "prepare-consumer": "12086072b008d3d4080b36fee039e281d3d45b16e9adf7f8348bcc8060270cc2",
        "configure-dedicated": "df61495d07edf060b218afc4d64bdb78a2e486dff25d68aa1d8ad4f68902e1b5",
        "restore": "630756c60c7bb755a6a3528815a731e787403211dd4ef3316e62c970d68b77be",
        "assert-package-graph": "2b338dcc9bbae928223cbeabe7a7b5bf85ce7a8cd3b949c4e65bbe0f4ac4eeeb",
        "cleanup-config": "4f26f02575d06620e3780ae17c1f0568d9128b0edb73781ebae816390c6337d9",
        "build": "d4c7631358478fc465f319bc4ebd6f384835c8b36fba44b5d0915bd6a8e2783a",
        "run": "d22b208c4ad31279ed42ea5ad0dce0d1676e84b5501509df764b74033303a43f",
    }
    for identifier, expected_hash in reviewed_hashes.items():
        require_command_sha256(path, steps_by_id[identifier], expected_hash)

    preflight = steps_by_id["preflight"]
    require_fragments(
        path,
        preflight,
        [
            'visibility="$(gh api "repos/$REPOSITORY" --jq \'.visibility\')"',
            '[[ "$DISPATCH_VERSION" == "$EXPECTED_VERSION" ]]',
            '[[ "$WORKFLOW_RUN_NAME" == "Release" ]]',
            '[[ "$WORKFLOW_RUN_CONCLUSION" == "success" ]]',
            "printf 'visibility=%s\\n' \"$visibility\" >> \"$GITHUB_OUTPUT\"",
            "printf 'package_version=%s\\n' \"$EXPECTED_VERSION\" >> \"$GITHUB_OUTPUT\"",
        ],
    )
    require_fragments(
        path,
        steps_by_id["dedicated-credential-check"],
        [
            "for variable in NUGET_READ_USERNAME NUGET_READ_TOKEN; do",
            '[[ -n "${!variable}" ]]',
            "A protected package read credential is unavailable",
        ],
    )
    require_fragments(
        path,
        steps_by_id["prepare-consumer"],
        [
            '<PackageReference Include="KyuzanInc.Peak.Sdk" Version="[1.0.0]" />',
            '<package pattern="KyuzanInc.Turnkey.*" />',
            "PeakClient.Initialize(new PeakClientOptions",
            "new InMemoryStorage()",
            'storage.Get("smoke-key") != "smoke-value"',
            'chmod 600 "$NUGET_CONFIG"',
        ],
    )
    require_fragments(
        path,
        steps_by_id["configure-dedicated"],
        [
            "dotnet nuget update source github-kyuzan",
            '--username "$NUGET_READ_USERNAME"',
            '--password "$NUGET_READ_TOKEN"',
            '--configfile "$NUGET_CONFIG"',
            '[[ "$(stat -c \'%a\' "$NUGET_CONFIG")" == 600 ]]',
        ],
    )

    allowed_secrets = {
        "NUGET_READ_USERNAME",
        "NUGET_READ_TOKEN",
    }
    referenced_secrets = re.findall(
        r"\$\{\{\s*secrets\.([A-Za-z0-9_]+)\s*\}\}", active_text
    )
    if not referenced_secrets or any(
        secret not in allowed_secrets for secret in referenced_secrets
    ):
        fail(path, "consumer workflow references an unapproved secret")
    if "${{ secrets.GITHUB_TOKEN }}" in active_text:
        fail(path, "contents-only consumer must never use GITHUB_TOKEN for packages")
    event_token_steps = [
        step["id"] for step in steps if "${{ github.token }}" in step["text"]
    ]
    if event_token_steps != ["preflight"]:
        fail(path, "workflow event token may be used only for live visibility preflight")

    all_normalized = " ".join(step["normalized"] for step in steps)
    if package_publication.search(all_normalized):
        fail(path, "package publication is forbidden in consumer workflow execution")
    ordered_ids = [
        "checkout",
        "setup-dotnet",
        "preflight",
        "dedicated-credential-check",
        "prepare-consumer",
        "configure-dedicated",
        "restore",
        "assert-package-graph",
        "cleanup-config",
        "build",
        "run",
    ]
    if [steps_by_id[identifier]["index"] for identifier in ordered_ids] != list(
        range(len(ordered_ids))
    ):
        fail(path, "consumer security and execution steps are in an unsafe order")

    require_exact_command_line(
        path,
        steps_by_id["cleanup-config"],
        'rm -f "$NUGET_CONFIG"',
    )
    require_exact_command_line(
        path,
        steps_by_id["build"],
        'dotnet build "$CONSUMER_PROJECT/PeakSdkConsumer.csproj" -c Release --no-restore',
    )
    require_exact_command_line(
        path,
        steps_by_id["run"],
        'dotnet run --project "$CONSUMER_PROJECT/PeakSdkConsumer.csproj" -c Release --no-build --no-restore',
    )
    restore = steps_by_id["restore"]
    require_fragments(
        path,
        restore,
        [
            'dotnet restore "$CONSUMER_PROJECT/PeakSdkConsumer.csproj"',
            '--configfile "$NUGET_CONFIG"',
            "--no-cache",
            "--force-evaluate",
        ],
    )
    assertions = steps_by_id["assert-package-graph"]
    require_fragments(
        path,
        assertions,
        [
            "kyuzaninc.peak.sdk",
            "kyuzaninc.turnkey.sdk",
            'split("/")[1] == "1.0.0"',
            'metadata="$NUGET_PACKAGES/kyuzaninc.peak.sdk/1.0.0/.nupkg.metadata"',
            '.source == "https://nuget.pkg.github.com/KyuzanInc/index.json"',
        ],
    )


def validate_release(path, active_lines, active_text):
    structure = yaml_structure_lines(active_lines)
    parsed_structure = parse_strict_release_yaml(path, structure)
    validate_strict_release_structure(path, parsed_structure)

    nonblank_structure = [line for line in structure if line.strip()]
    if not nonblank_structure or nonblank_structure[0] != "name: Release":
        fail(path, "release workflow name must be exactly Release")

    require_exact_top_level_block(
        path,
        structure,
        "on",
        ["on:", "  release:", "    types: [published]"],
    )
    require_exact_top_level_block(
        path,
        structure,
        "permissions",
        ["permissions:", "  contents: write", "  packages: write"],
    )
    require_exact_top_level_block(
        path,
        structure,
        "concurrency",
        [
            "concurrency:",
            "  group: release-${{ github.event.release.tag_name }}",
            "  cancel-in-progress: false",
        ],
    )

    jobs_starts = [
        index for index, line in enumerate(structure) if line == "jobs:"
    ]
    if len(jobs_starts) != 1:
        fail(path, "release workflow must declare exactly one jobs block")
    jobs_structure = [
        line
        for line in structure[jobs_starts[0] + 1 :]
        if line.strip()
    ]
    job_keys = [
        line
        for line in jobs_structure
        if re.match(r"^  [A-Za-z0-9_-]+:$", line)
    ]
    if job_keys != ["  publish:"]:
        fail(path, "release workflow must contain only the publish job")
    if "    runs-on: ubuntu-latest" not in jobs_structure:
        fail(path, "release publish job must run on ubuntu-latest")
    if "    environment: github-packages" not in jobs_structure:
        fail(path, "release publish job must use environment github-packages")

    if re.search(r"(?im)^[ \t]*(?:push|workflow_dispatch|pull_request)[ \t]*:", "\n".join(structure)):
        fail(path, "release publication trigger must be only release published")
    if re.search(r"(?im)^[ \t]*id-token[ \t]*:", "\n".join(structure)):
        fail(path, "release workflow must not request id-token permission")

    textual_uses_count = len(uses_key.findall(active_text))
    parsed_uses = []
    for index, line in enumerate(structure):
        match = uses_line.match(line)
        if match is None:
            continue
        value = unquote_scalar(match.group("value"))
        if not is_valid_action(value):
            fail(path, f"line {index + 1} has a mutable or malformed uses reference")
        parsed_uses.append(value)
    if textual_uses_count != len(parsed_uses):
        fail(path, "release uses references must use an inspectable scalar form")
    if any(
        value.casefold().startswith("actions/upload-artifact@")
        for value in parsed_uses
    ):
        fail(path, "release workflow must not upload Actions artifacts")
    if parsed_uses != [
        "actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0",
        "actions/setup-dotnet@c2fa09f4bde5ebb9d1777cf28262a3eb3db3ced7",
    ]:
        fail(path, "release workflow must use only the approved checkout and setup-dotnet pins")

    steps = parse_release_steps(path, active_lines, structure)
    if steps[0]["uses"] != parsed_uses[0] or steps[1]["uses"] != parsed_uses[1]:
        fail(path, "release workflow must check out and set up .NET before scripted steps")
    if "persist-credentials: false" not in steps[0]["text"]:
        fail(path, "release checkout must disable persisted credentials")
    if "ref: ${{ github.event.release.tag_name }}" not in steps[0]["text"]:
        fail(path, "release checkout must explicitly select the published Release tag")

    steps_by_id = {step["id"]: step for step in steps if step["id"]}
    required_ids = [
        "checkout",
        "setup-dotnet",
        "preflight",
        "dedicated-credential-check",
        "credential-mode",
        "read-public-internal",
        "read-private-dedicated",
        "read-private-fallback",
        "restore",
        "cleanup-read",
        "build",
        "test",
        "pack",
        "package",
        "release-assets",
        "publish-public-internal",
        "publish-private-dedicated",
        "publish-private-fallback",
        "registry",
        "upload-manifest",
        "cleanup-publish",
    ]
    for identifier in required_ids:
        require_step(path, steps_by_id, identifier)
    expected_run_sha256 = {
        "preflight": "82c14567f2d6be2645adf59a795f1299a3dd5dc612762b8c77b70fc923f3e556",
        "dedicated-credential-check": "afedfff7a9ec2d548bd423f2fd0dcce731c1873e5669aba5916ea6e41ad8671a",
        "credential-mode": "b17669336596988f3b205dc5070e4ccb6e727cfb8b9f237fbbfd2378f8f8f839",
        "read-public-internal": "321e8e3d75d815185c65e16064dc6673999161a948c1febf149cc5f6262517c1",
        "read-private-dedicated": "321e8e3d75d815185c65e16064dc6673999161a948c1febf149cc5f6262517c1",
        "read-private-fallback": "70972fd5b63bc9c0bb8221c988f72043842bb2418315c055505456de6605fa08",
        "restore": "8e3a1159de2e648479fbb6337e7ce51ea6220271319b3585e879b1ab164d7caa",
        "cleanup-read": "36d5eb9d960cb412d1b63ba6254b3f0f94e318861e71739e233ef8b2eec42154",
        "build": "540982ff771869914cb47509f42a822657d480d19fd34c6cf8ca50ed1ef7963c",
        "test": "09961f0949f5944d4cd61f4f7af3d6d728f8584ea78d365d09405affead70a64",
        "pack": "969b776288aee4f4fecac1eb1d053f5ee553ccb71465b24794a08078bd9ab11d",
        "package": "8b39602306a4f1b7489c20731c7647f13600ba25011c4d12999f2a0705ce115c",
        "release-assets": "9d218a0d0cd80952205a2f880bf64382d232e07f6cd19282b4f4efed34a0e54f",
        "publish-public-internal": "e3d2df2213ebd3df5283834d1ffce894f183050e7f5b1b1b904ad3b8cf4e9d37",
        "publish-private-dedicated": "e3d2df2213ebd3df5283834d1ffce894f183050e7f5b1b1b904ad3b8cf4e9d37",
        "publish-private-fallback": "7a4e131ff53fa46bd60fb99764f049ab5304e0a35744ce5603dd1d39ecea463d",
        "registry": "3374ff807035a11636bca4d7d6582236e10a9ed5ee56348e07d567aff681ff0b",
        "upload-manifest": "bbc72f84ff43c8aa5cd360c4e07c959fe301c55d7ef4f1ad3ac3ecd5d249ad96",
        "cleanup-publish": "f49065a1922270c825870b86786aded25d18f4573257bcbf8bdbca8dc3b780ca",
    }
    actual_run_ids = {
        step["id"] for step in steps if step["command"]
    }
    if actual_run_ids != set(expected_run_sha256):
        fail(path, "release executable step set differs from the reviewed contract")
    for identifier, expected_sha256 in expected_run_sha256.items():
        require_command_sha256(path, steps_by_id[identifier], expected_sha256)
    if "${{ steps.package.outputs.snupkg_path }}" in active_text:
        fail(path, "snupkg output must not feed any downstream release input")

    preflight = steps_by_id["preflight"]
    if any(key.startswith("NUGET_") for key in preflight["env"]):
        fail(path, "release preflight must not receive package credentials")
    require_env(
        path,
        preflight,
        {
            "GH_TOKEN": "${{ github.token }}",
            "REPOSITORY": "${{ github.repository }}",
            "TAG_NAME": "${{ github.event.release.tag_name }}",
            "TAG_COMMIT": "${{ github.sha }}",
            "PROJECT_FILE": "packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj",
            "EXPECTED_VERSION": "1.0.0",
        },
    )
    require_fragments(
        path,
        preflight,
        [
            "gh api \"repos/$REPOSITORY\" --jq '.visibility'",
            "private|public|internal",
            "[[ \"$TAG_NAME\" =~ ^v(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)$ ]]",
            "[[ \"$TAG_NAME\" == \"v$EXPECTED_VERSION\" ]]",
            "git rev-parse HEAD",
            "gh api \"repos/$REPOSITORY/compare/main...$TAG_COMMIT\" --jq '.status'",
            "[[ \"$compare_status\" == \"identical\" ]]",
            "xml.etree.ElementTree",
            "PackageId",
            "KyuzanInc.Peak.Sdk",
            "printf 'visibility=%s\\n' \"$visibility\" >> \"$GITHUB_OUTPUT\"",
        ],
    )

    public_guard = (
        "steps.preflight.outputs.visibility == 'public' || "
        "steps.preflight.outputs.visibility == 'internal'"
    )
    private_dedicated_guard = (
        "steps.preflight.outputs.visibility == 'private' && "
        "steps.credential-mode.outputs.mode == 'dedicated'"
    )
    private_fallback_guard = (
        "steps.preflight.outputs.visibility == 'private' && "
        "steps.credential-mode.outputs.mode == 'fallback'"
    )
    dedicated_bindings = {
        "NUGET_READ_USERNAME": "${{ secrets.NUGET_READ_USERNAME }}",
        "NUGET_READ_TOKEN": "${{ secrets.NUGET_READ_TOKEN }}",
        "NUGET_PUBLISH_USERNAME": "${{ secrets.NUGET_PUBLISH_USERNAME }}",
        "NUGET_PUBLISH_TOKEN": "${{ secrets.NUGET_PUBLISH_TOKEN }}",
    }
    dedicated_check = steps_by_id["dedicated-credential-check"]
    credential_mode = steps_by_id["credential-mode"]
    if dedicated_check["if"] != public_guard:
        fail(path, "public/internal credential check has an invalid guard")
    if credential_mode["if"] != "steps.preflight.outputs.visibility == 'private'":
        fail(path, "private credential-mode selection has an invalid guard")
    require_env(path, dedicated_check, dedicated_bindings)
    require_env(path, credential_mode, dedicated_bindings)
    require_fragments(
        path,
        dedicated_check,
        [
            "NUGET_READ_USERNAME NUGET_READ_TOKEN",
            "NUGET_PUBLISH_USERNAME NUGET_PUBLISH_TOKEN",
            "[[ -n \"${!variable}\" ]]",
        ],
    )
    require_fragments(
        path,
        credential_mode,
        [
            "0) mode=fallback",
            "4) mode=dedicated",
            "Dedicated package credentials are only partially configured",
            "printf 'mode=%s\\n' \"$mode\" >> \"$GITHUB_OUTPUT\"",
        ],
    )

    read_bindings = {
        "NUGET_READ_USERNAME": "${{ secrets.NUGET_READ_USERNAME }}",
        "NUGET_READ_TOKEN": "${{ secrets.NUGET_READ_TOKEN }}",
        "READ_CONFIG": "${{ runner.temp }}/nuget-read.config",
    }
    publish_bindings = {
        "NUGET_PUBLISH_USERNAME": "${{ secrets.NUGET_PUBLISH_USERNAME }}",
        "NUGET_PUBLISH_TOKEN": "${{ secrets.NUGET_PUBLISH_TOKEN }}",
        "PUBLISH_CONFIG": "${{ runner.temp }}/nuget-publish.config",
        "PUBLISH_USERNAME_FILE": "${{ runner.temp }}/nuget-publish-username",
        "PUBLISH_TOKEN_FILE": "${{ runner.temp }}/nuget-publish-token",
    }
    guarded_steps = [
        ("read-public-internal", public_guard, read_bindings),
        ("read-private-dedicated", private_dedicated_guard, read_bindings),
        ("publish-public-internal", public_guard, publish_bindings),
        ("publish-private-dedicated", private_dedicated_guard, publish_bindings),
    ]
    for identifier, guard, bindings in guarded_steps:
        step = steps_by_id[identifier]
        if step["if"] != guard:
            fail(path, f"release step {identifier} has an invalid credential guard")
        require_env(path, step, bindings)
        if "umask 077" not in step["command"]:
            fail(path, f"release step {identifier} must create credentials with umask 077")

    for identifier in (
        "read-public-internal",
        "read-private-dedicated",
        "read-private-fallback",
    ):
        require_fragments(
            path,
            steps_by_id[identifier],
            [
                "dotnet nuget add source https://nuget.pkg.github.com/KyuzanInc/index.json",
                "--username \"$NUGET_READ_USERNAME\"",
                "--password \"$NUGET_READ_TOKEN\"",
                "--configfile \"$READ_CONFIG\"",
            ],
        )
    for identifier in (
        "publish-public-internal",
        "publish-private-dedicated",
        "publish-private-fallback",
    ):
        require_fragments(
            path,
            steps_by_id[identifier],
            [
                "dotnet nuget add source https://nuget.pkg.github.com/KyuzanInc/index.json",
                "--username \"$NUGET_PUBLISH_USERNAME\"",
                "--password \"$NUGET_PUBLISH_TOKEN\"",
                "--configfile \"$PUBLISH_CONFIG\"",
            ],
        )

    fallback_read = steps_by_id["read-private-fallback"]
    fallback_publish = steps_by_id["publish-private-fallback"]
    for step in (fallback_read, fallback_publish):
        if step["if"] != private_fallback_guard:
            fail(path, f"release step {step['id']} has an invalid fallback guard")
        if "umask 077" not in step["command"]:
            fail(path, f"release step {step['id']} must use umask 077")
    require_env(
        path,
        fallback_read,
        {
            "NUGET_READ_USERNAME": "${{ github.actor }}",
            "NUGET_READ_TOKEN": "${{ secrets.GITHUB_TOKEN }}",
            "READ_CONFIG": "${{ runner.temp }}/nuget-read.config",
        },
    )
    require_env(
        path,
        fallback_publish,
        {
            "NUGET_PUBLISH_USERNAME": "${{ github.actor }}",
            "NUGET_PUBLISH_TOKEN": "${{ secrets.GITHUB_TOKEN }}",
            "PUBLISH_CONFIG": "${{ runner.temp }}/nuget-publish.config",
            "PUBLISH_USERNAME_FILE": "${{ runner.temp }}/nuget-publish-username",
            "PUBLISH_TOKEN_FILE": "${{ runner.temp }}/nuget-publish-token",
        },
    )

    allowed_secrets = {
        "NUGET_READ_USERNAME",
        "NUGET_READ_TOKEN",
        "NUGET_PUBLISH_USERNAME",
        "NUGET_PUBLISH_TOKEN",
        "GITHUB_TOKEN",
    }
    referenced_secrets = re.findall(
        r"\$\{\{\s*secrets\.([A-Za-z0-9_]+)\s*\}\}", active_text
    )
    if not referenced_secrets or any(
        secret not in allowed_secrets for secret in referenced_secrets
    ):
        fail(path, "release workflow references an unapproved secret")
    github_token_steps = [
        step["id"]
        for step in steps
        if "${{ secrets.GITHUB_TOKEN }}" in step["text"]
    ]
    if github_token_steps != ["read-private-fallback", "publish-private-fallback"]:
        fail(path, "GITHUB_TOKEN fallback may appear only in private fallback steps")
    event_token_steps = [
        step["id"]
        for step in steps
        if "${{ github.token }}" in step["text"]
    ]
    if event_token_steps != ["preflight", "release-assets", "upload-manifest"]:
        fail(path, "workflow event token may be used only for repository/Release API steps")
    for step in steps:
        for command_line in step["command"].splitlines():
            if not re.search(
                r"\b(?:NUGET_READ_TOKEN|NUGET_PUBLISH_TOKEN)\b",
                command_line,
            ):
                continue
            if "GITHUB_OUTPUT" in command_line or (
                re.search(r"\b(?:echo|printf)\b", command_line)
                and ">" not in command_line
            ):
                fail(path, "package credentials must never be logged or exposed as outputs")

    ordered_ids = [
        "checkout",
        "setup-dotnet",
        "preflight",
        "dedicated-credential-check",
        "credential-mode",
        "read-public-internal",
        "read-private-dedicated",
        "read-private-fallback",
        "restore",
        "cleanup-read",
        "build",
        "test",
        "pack",
        "package",
        "release-assets",
        "publish-public-internal",
        "publish-private-dedicated",
        "publish-private-fallback",
        "registry",
        "upload-manifest",
        "cleanup-publish",
    ]
    positions = [steps_by_id[identifier]["index"] for identifier in ordered_ids]
    if positions != sorted(positions):
        fail(path, "release security and publication steps are in an unsafe order")
    package_access = re.compile(
        r"""(?ix)
        nuget\.pkg\.github\.com |
        \bdotnet\s+restore\b |
        \bdotnet\s+nuget\s+push\b
        """
    )
    for step in steps:
        if not package_access.search(step["normalized"]):
            continue
        allowed_package_access_ids = {
            "read-public-internal",
            "read-private-dedicated",
            "read-private-fallback",
            "restore",
            "publish-public-internal",
            "publish-private-dedicated",
            "publish-private-fallback",
            "registry",
        }
        if step["id"] not in allowed_package_access_ids:
            fail(path, "package access is confined to the reviewed credential routes")
        if step["index"] < preflight["index"]:
            fail(path, "package access is forbidden before credential-free preflight")
    if steps_by_id["cleanup-read"]["if"] != "always()":
        fail(path, "read credential cleanup must run unconditionally")
    if steps_by_id["cleanup-publish"]["if"] != "always()":
        fail(path, "publish credential cleanup must run unconditionally")
    restore = steps_by_id["restore"]
    require_env(
        path,
        restore,
        {"READ_CONFIG": "${{ runner.temp }}/nuget-read.config"},
    )
    require_fragments(
        path,
        restore,
        [
            "dotnet restore peak-sdk-csharp.sln --locked-mode --configfile \"$READ_CONFIG\"",
        ],
    )
    require_env(
        path,
        steps_by_id["cleanup-read"],
        {"READ_CONFIG": "${{ runner.temp }}/nuget-read.config"},
    )
    require_fragments(
        path,
        steps_by_id["cleanup-read"],
        ["rm -f \"$READ_CONFIG\""],
    )
    require_exact_command_line(
        path,
        steps_by_id["cleanup-read"],
        'rm -f "$READ_CONFIG"',
    )
    require_env(
        path,
        steps_by_id["cleanup-publish"],
        {
            "PUBLISH_CONFIG": "${{ runner.temp }}/nuget-publish.config",
            "PUBLISH_USERNAME_FILE": "${{ runner.temp }}/nuget-publish-username",
            "PUBLISH_TOKEN_FILE": "${{ runner.temp }}/nuget-publish-token",
        },
    )
    require_fragments(
        path,
        steps_by_id["cleanup-publish"],
        [
            "rm -f \"$PUBLISH_CONFIG\" \"$PUBLISH_USERNAME_FILE\" \"$PUBLISH_TOKEN_FILE\"",
        ],
    )
    require_exact_command_line(
        path,
        steps_by_id["cleanup-publish"],
        'rm -f "$PUBLISH_CONFIG" "$PUBLISH_USERNAME_FILE" "$PUBLISH_TOKEN_FILE"',
    )
    required_build_commands = [
        "dotnet build peak-sdk-csharp.sln --no-restore -c Release",
        "dotnet test peak-sdk-csharp.sln --no-build -c Release --filter Category!=E2E",
        "dotnet pack packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj",
    ]
    all_normalized = " ".join(step["normalized"] for step in steps)
    for command in required_build_commands:
        if command not in all_normalized:
            fail(path, f"release workflow is missing required build operation: {command}")
    require_exact_command_line(
        path,
        steps_by_id["build"],
        "dotnet build peak-sdk-csharp.sln --no-restore -c Release -m:1 /nr:false",
    )
    require_exact_command_line(
        path,
        steps_by_id["test"],
        'dotnet test peak-sdk-csharp.sln --no-build -c Release --filter "Category!=E2E" -m:1 /nr:false',
    )

    package_step = steps_by_id["package"]
    require_fragments(
        path,
        package_step,
        [
            "bash tools/package/validate-package.sh \"$NUPKG_PATH\" \"$SNUPKG_PATH\"",
            "KyuzanInc.Peak.Sdk.1.0.0.nupkg",
            "KyuzanInc.Peak.Sdk.1.0.0.snupkg",
            "python3 tools/package/canonicalize-nuget-package.py \"$NUPKG_PATH\"",
            "python3 tools/package/canonicalize-nuget-package.py \"$SNUPKG_PATH\"",
            "LC_ALL=C sort",
            "sha256sum \"$package\"",
            "cd \"$PACKAGE_DIR\"",
        ],
    )

    release_assets = steps_by_id["release-assets"]
    require_env(path, release_assets, {"GH_TOKEN": "${{ github.token }}"})
    require_fragments(
        path,
        release_assets,
        [
            "gh api --paginate --slurp",
            "repos/$REPOSITORY/releases/$RELEASE_ID/assets?per_page=100",
            "if [[ \"$asset_name\" != \"release-checksums.txt\" ]]; then",
            "cmp --silent \"$DOWNLOADED_MANIFEST\" \"$MANIFEST_PATH\"",
            "printf 'manifest_exists=%s\\n' \"$manifest_exists\" >> \"$GITHUB_OUTPUT\"",
        ],
    )
    require_exact_command_line(
        path,
        release_assets,
        'cmp --silent "$DOWNLOADED_MANIFEST" "$MANIFEST_PATH"',
    )
    require_exact_command_slice(
        path,
        release_assets,
        [
            'if [[ -n "$manifest_asset_id" ]]; then',
            'gh api "repos/$REPOSITORY/releases/assets/$manifest_asset_id" \\',
            '-H "Accept: application/octet-stream" > "$DOWNLOADED_MANIFEST"',
            'cmp --silent "$DOWNLOADED_MANIFEST" "$MANIFEST_PATH"',
            "manifest_exists=1",
            "else",
            "manifest_exists=0",
            "fi",
        ],
    )
    require_command_sha256(
        path,
        release_assets,
        "9d218a0d0cd80952205a2f880bf64382d232e07f6cd19282b4f4efed34a0e54f",
    )

    registry = steps_by_id["registry"]
    require_fragments(
        path,
        registry,
        [
            "gh api --paginate --slurp",
            "orgs/$REPOSITORY_OWNER/packages?package_type=nuget&per_page=100",
            "package inventory is not a paginated array",
            "package inventory item has an invalid schema",
            "package inventory contains duplicate package names",
            "if [[ \"$PACKAGE_PRESENT\" == 1 ]]; then",
            "orgs/$REPOSITORY_OWNER/packages/nuget/KyuzanInc.Peak.Sdk/versions?per_page=100",
            "version inventory is not a paginated array",
            "version inventory item has an invalid schema",
            "version inventory contains duplicate version names",
            "elif [[ \"$PACKAGE_PRESENT\" == 0 ]]; then",
            "--write-out '%{http_code}'",
            "if [[ \"$VERSION_PRESENT\" == 1 ]]; then",
            "if [[ \"$HTTP_STATUS\" != 200 ]]; then",
            "cmp --silent \"$DOWNLOADED_NUPKG\" \"$NUPKG_PATH\"",
            "elif [[ \"$VERSION_PRESENT\" == 0 ]]; then",
            "dotnet nuget push \"$NUPKG_PATH\"",
            "--no-symbols",
            "Unexpected version inventory decision",
        ],
    )
    require_exact_command_line(
        path,
        registry,
        'cmp --silent "$DOWNLOADED_NUPKG" "$NUPKG_PATH"',
    )
    require_ordered_command_lines(
        path,
        registry,
        [
            'if [[ "$PACKAGE_PRESENT" == 1 ]]; then',
            'elif [[ "$PACKAGE_PRESENT" == 0 ]]; then',
            "else",
            "fi",
            'if [[ "$VERSION_PRESENT" == 1 ]]; then',
        ],
    )
    require_exact_command_slice(
        path,
        registry,
        [
            'if [[ "$VERSION_PRESENT" == 1 ]]; then',
            'HTTP_STATUS="$(curl --silent --show-error --location \\',
            '--user "$PUBLISH_USERNAME:$PUBLISH_TOKEN" \\',
            '--output "$DOWNLOADED_NUPKG" \\',
            "--write-out '%{http_code}' \\",
            '"https://nuget.pkg.github.com/$REPOSITORY_OWNER/download/kyuzaninc.peak.sdk/$PACKAGE_VERSION/kyuzaninc.peak.sdk.$PACKAGE_VERSION.nupkg")"',
            'if [[ "$HTTP_STATUS" != 200 ]]; then',
            'printf \'Inventory-present package download returned HTTP %s\\n\' "$HTTP_STATUS" >&2',
            "exit 1",
            "fi",
            'cmp --silent "$DOWNLOADED_NUPKG" "$NUPKG_PATH"',
            'elif [[ "$VERSION_PRESENT" == 0 ]]; then',
            'dotnet nuget push "$NUPKG_PATH" \\',
            "--source github-kyuzan \\",
            '--api-key "$PUBLISH_TOKEN" \\',
            "--no-symbols \\",
            '--configfile "$PUBLISH_CONFIG"',
            "else",
            'printf \'Unexpected version inventory decision: %s\\n\' "$VERSION_PRESENT" >&2',
            "exit 1",
            "fi",
        ],
    )
    require_command_sha256(
        path,
        registry,
        "3374ff807035a11636bca4d7d6582236e10a9ed5ee56348e07d567aff681ff0b",
    )
    if re.search(r"\bNUGET_API_KEY\b", active_text, re.I):
        fail(path, "NuGet API key is forbidden")
    if "--skip-duplicate" in all_normalized:
        fail(path, "immutable release publication must not skip duplicates")
    push_steps = [
        step
        for step in steps
        if re.search(r"\bdotnet\s+nuget\s+push\b", step["normalized"], re.I)
    ]
    if push_steps != [registry]:
        fail(path, "release workflow must contain exactly one registry push decision")
    if "dotnet nuget push $NUPKG_PATH" not in registry["normalized"]:
        fail(path, "registry publication may push only the Peak nupkg")
    if re.search(r"dotnet\s+nuget\s+push.*(?:snupkg|SNUPKG)", registry["normalized"], re.I):
        fail(path, "symbol package registry publication is forbidden")
    if (
        "dotnet nuget push" in registry["normalized"]
        and "api.nuget.org" in registry["normalized"]
    ):
        fail(path, "nuget.org publication is forbidden")

    upload = steps_by_id["upload-manifest"]
    require_env(path, upload, {"GH_TOKEN": "${{ github.token }}"})
    if upload["if"] != "steps.release-assets.outputs.manifest_exists == '0'":
        fail(path, "checksum manifest upload must run only when the manifest is absent")
    release_uploads = [
        step
        for step in steps
        if re.search(r"\bgh\s+release\s+upload\b", step["normalized"], re.I)
    ]
    if release_uploads != [upload]:
        fail(path, "release workflow must contain exactly one Release upload")
    if "gh release upload $TAG_NAME $MANIFEST_PATH" not in upload["normalized"]:
        fail(path, "Release upload may contain only release-checksums.txt")
    require_exact_command_line(
        path,
        upload,
        'gh release upload "$TAG_NAME" "$MANIFEST_PATH"',
    )
    if "--clobber" in upload["normalized"]:
        fail(path, "Release checksum manifest must never be clobbered")
    if re.search(r"\bgh\s+release\s+upload\b.*(?:nupkg|snupkg)", all_normalized, re.I):
        fail(path, "package binaries must never be public Release assets")


def validate(path):
    try:
        raw_lines = path.read_text(encoding="utf-8").splitlines()
    except (OSError, UnicodeError) as exc:
        fail(path, f"workflow is unreadable UTF-8: {exc}")

    active_lines = [strip_yaml_comment(line) for line in raw_lines]
    active_text = "\n".join(active_lines)
    if path.name == "release.yml":
        validate_release(path, active_lines, active_text)
        return
    if path.name == "consumer-smoke.yml":
        validate_consumer(path, active_lines, active_text)
        return

    validate_mapping_key_safety(path, active_lines)
    shell_text = extract_shell_commands(path, active_lines, active_text)
    shell_normalized = normalize_shell_tokens(path, shell_text)

    validate_permissions(path, active_lines, active_text)
    if secret_interpolation.search(active_text):
        fail(path, "secret or token interpolation is forbidden")
    if token_identifier.search(active_text):
        fail(path, "GitHub or NuGet token injection is forbidden")
    if credential_or_probe.search(shell_normalized):
        fail(path, "package credential or GitHub Packages authentication probe is forbidden")
    if package_publication.search(shell_normalized):
        fail(path, "package publication is forbidden in normal public workflow execution")

    textual_uses_count = len(uses_key.findall(active_text))
    parsed_uses = []
    for index, line in enumerate(active_lines):
        match = uses_line.match(line)
        if not match:
            continue
        value = unquote_scalar(match.group("value"))
        if not value or value[0] in "|>":
            fail(path, f"line {index + 1} has a multiline or empty uses reference")
        if not is_valid_action(value):
            fail(path, f"line {index + 1} has a mutable or malformed uses reference")
        parsed_uses.append((index, len(match.group("indent")), value))

    if textual_uses_count != len(parsed_uses):
        fail(path, "uses references must use a directly verifiable block-scalar form")

    for index, step_indent, value in parsed_uses:
        if not value.casefold().startswith("actions/upload-artifact@"):
            continue

        block = []
        for following in active_lines[index + 1 :]:
            if not following.strip():
                continue
            indentation = len(following) - len(following.lstrip(" \t"))
            if indentation <= step_indent and following.lstrip().startswith("-"):
                break
            if indentation < step_indent:
                break
            block.append(following)
        block_text = "\n".join(block)
        path_match = re.search(
            r"""(?im)^[ \t]*(?:"path"|'path'|path)[ \t]*:[ \t]*(.*)$""",
            block_text,
        )
        if path_match is None:
            fail(path, "upload-artifact step must declare an inspectable path")
        artifact_path = unquote_scalar(path_match.group(1))
        if artifact_path != "TestResults/**/coverage.cobertura.xml":
            fail(path, "upload-artifact may upload only the coverage XML path")


try:
    for workflow_path in sys.argv[1:]:
        validate(pathlib.Path(workflow_path))
except WorkflowError as exc:
    print(str(exc), file=sys.stderr)
    raise SystemExit(1)

print("workflow verification passed")
PY
