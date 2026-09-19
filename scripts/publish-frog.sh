#!/usr/bin/env bash
# Publish Frog.Server (linux-x64 / win-x64) and WinForms Frog.Client / Frog.Editor (win-x64).
# Self-contained, not single-file: Program.TryLoadPostgreSqlAuthBackend loads
# Frog.Persistence.PostgreSql.dll from AppContext.BaseDirectory.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIGURATION="Release"
OUTPUT_ROOT="${ROOT}/artifacts/publish"
FORCE=0
TARGETS=()

USAGE='Usage: publish-frog.sh [options] --target NAME [--target NAME ...]

Repeatable dotnet publish layouts for Phase 10 packaging (P10-6 self-contained).

Targets:
  server-linux-x64   Frog.Server, RID linux-x64
  server-win-x64     Frog.Server, RID win-x64 (layout only on non-Windows)
  client-win-x64     Frog.Client, RID win-x64 (Windows runtime; layout-only elsewhere)
  editor-win-x64     Frog.Editor, RID win-x64 (Windows runtime; layout-only elsewhere)
  all                all four layouts

Options:
  --target NAME            Required unless --list
  --output-root DIR        Default: <repo>/artifacts/publish
  --configuration NAME     Default: Release
  --force                  Overwrite an existing target directory
  --list                   Print targets and exit
  -h, --help               Show this help

Requires the SDK in global.json (8.0.424, rollForward latestFeature).
Does not create installers, does not enable MariaDB, does not import .fcc.
'

die() {
  echo "error: $*" >&2
  exit 1
}

list_targets() {
  cat <<'EOF'
server-linux-x64
server-win-x64
client-win-x64
editor-win-x64
all
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --target) TARGETS+=("${2:-}"); shift 2 ;;
    --output-root) OUTPUT_ROOT="${2:-}"; shift 2 ;;
    --configuration) CONFIGURATION="${2:-}"; shift 2 ;;
    --force) FORCE=1; shift ;;
    --list) list_targets; exit 0 ;;
    -h|--help) printf '%s' "$USAGE"; exit 0 ;;
    *) die "unknown argument: $1" ;;
  esac
done

[[ ${#TARGETS[@]} -gt 0 ]] || die "pass --target (see --help)"

command -v dotnet >/dev/null 2>&1 || die "dotnet not on PATH (install SDK 8.0.424)"

KNOWN_TARGETS='all server-linux-x64 server-win-x64 client-win-x64 editor-win-x64'
for t in "${TARGETS[@]}"; do
  case " $KNOWN_TARGETS " in
    *" $t "*) ;;
    *) die "unknown --target: $t" ;;
  esac
done

expand_targets() {
  local t
  for t in "$@"; do
    case "$t" in
      all)
        echo server-linux-x64
        echo server-win-x64
        echo client-win-x64
        echo editor-win-x64
        ;;
      *)
        echo "$t"
        ;;
    esac
  done
}

protocol_version() {
  local file="${ROOT}/Frog.Core/Constants/FrogWireProtocol.cs"
  local line
  line="$(grep -E 'public const ushort Version = [0-9]+;' "$file" | head -n1 || true)"
  if [[ "$line" =~ Version\ =\ ([0-9]+) ]]; then
    printf '%s' "${BASH_REMATCH[1]}"
  else
    printf '%s' "10"
  fi
}

git_tip() {
  if git -C "$ROOT" rev-parse HEAD >/dev/null 2>&1; then
    git -C "$ROOT" rev-parse HEAD
  else
    printf '%s' "unknown"
  fi
}

write_manifest() {
  local dest="$1" name="$2" project="$3" rid="$4" tfm="$5"
  local sdk protocol tip published
  sdk="$(dotnet --version)"
  protocol="$(protocol_version)"
  tip="$(git_tip)"
  published="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
  cat > "${dest}/packaging-manifest.json" <<EOF
{
  "product": "Frog",
  "layout": "${name}",
  "project": "${project}",
  "targetFramework": "${tfm}",
  "runtimeIdentifier": "${rid}",
  "configuration": "${CONFIGURATION}",
  "selfContained": true,
  "singleFile": false,
  "sdk": "${sdk}",
  "protocolVersion": ${protocol},
  "gitSha": "${tip}",
  "publishedUtc": "${published}"
}
EOF
}

require_files() {
  local dest="$1"
  shift
  local missing=0 f
  for f in "$@"; do
    if [[ ! -e "${dest}/${f}" ]]; then
      echo "error: missing ${f} in ${dest}" >&2
      missing=1
    fi
  done
  return "$missing"
}

verify_no_local_overlay() {
  local dest="$1"
  if [[ -e "${dest}/appsettings.Local.json" ]]; then
    die "publish output must not contain appsettings.Local.json (secrets). Copy the overlay after publish."
  fi
}

verify_server_layout() {
  local dest="$1" rid="$2"
  require_files "$dest" \
    Frog.Server.dll \
    Frog.Server.runtimeconfig.json \
    Frog.Server.deps.json \
    Frog.Persistence.PostgreSql.dll \
    Npgsql.dll \
    Npgsql.EntityFrameworkCore.PostgreSQL.dll \
    Microsoft.EntityFrameworkCore.dll \
    Microsoft.EntityFrameworkCore.Relational.dll \
    EFCore.NamingConventions.dll \
    appsettings.json \
    appsettings.Local.json.example \
    packaging-manifest.json
  if [[ "$rid" == win-x64 ]]; then
    require_files "$dest" Frog.Server.exe hostfxr.dll
  else
    require_files "$dest" Frog.Server libhostfxr.so
  fi
  verify_no_local_overlay "$dest"
  if grep -qi "Npgsql.EntityFrameworkCore.PostgreSQL" "${dest}/Frog.Server.deps.json"; then
    :
  else
    die "Frog.Server.deps.json does not reference Npgsql.EntityFrameworkCore.PostgreSQL"
  fi
}

verify_client_layout() {
  local dest="$1"
  require_files "$dest" \
    Frog.Client.dll \
    Frog.Client.exe \
    Frog.Core.dll \
    Frog.Application.dll \
    hostfxr.dll \
    packaging-manifest.json
}

verify_editor_layout() {
  local dest="$1"
  require_files "$dest" \
    Frog.Editor.dll \
    Frog.Editor.exe \
    Frog.Persistence.PostgreSql.dll \
    Frog.Core.dll \
    hostfxr.dll \
    appsettings.Local.json.example \
    packaging-manifest.json
}

copy_demo_docs() {
  local dest="$1"
  local licenses="${ROOT}/docs/progress/phase-10-beta-release/demo-world/LICENSES.md"
  local demo="${ROOT}/docs/progress/phase-10-beta-release/DEMO_WORLD.md"
  mkdir -p "${dest}/demo-world"
  cp "$licenses" "${dest}/demo-world/LICENSES.md"
  cp "$demo" "${dest}/DEMO_WORLD.md"
}

archive_layout() {
  local dest="$1" name="$2"
  local archives="${OUTPUT_ROOT}/archives"
  mkdir -p "$archives"
  python3 - "$dest" "$archives" "$name" <<'PY'
import hashlib, json, sys, zipfile
from pathlib import Path
dest = Path(sys.argv[1])
archives = Path(sys.argv[2])
name = sys.argv[3]
zip_path = archives / f"{name}.zip"
if zip_path.exists():
    zip_path.unlink()
with zipfile.ZipFile(zip_path, "w", compression=zipfile.ZIP_DEFLATED) as zf:
    for path in dest.rglob("*"):
        if not path.is_file():
            continue
        arc = path.relative_to(dest.parent).as_posix()
        info = zipfile.ZipInfo.from_file(path, arc)
        info.compress_type = zipfile.ZIP_DEFLATED
        mode = path.stat().st_mode
        if path.name == "Frog.Server" or path.suffix == ".sh" or (mode & 0o111):
            mode = (mode & ~0o777) | 0o755
        info.external_attr = (mode & 0xFFFF) << 16
        zf.writestr(info, path.read_bytes())
digest = hashlib.sha256(zip_path.read_bytes()).hexdigest()
sums = archives / "SHA256SUMS"
lines = []
if sums.exists():
    lines = [ln for ln in sums.read_text(encoding="utf-8").splitlines() if ln.strip() and not ln.endswith("  " + zip_path.name)]
lines.append(f"{digest}  {zip_path.name}")
sums.write_text("\n".join(lines) + "\n", encoding="utf-8")
manifest = dest / "packaging-manifest.json"
data = json.loads(manifest.read_text(encoding="utf-8"))
data["archive"] = zip_path.name
data["archiveSha256"] = digest
manifest.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
print(f"archive {zip_path} sha256={digest}")
PY
}

publish_one() {
  local name="$1" project="$2" rid="$3" tfm="$4"
  local dest="${OUTPUT_ROOT}/${name}"
  local project_path="${ROOT}/${project}"
  [[ -f "$project_path" ]] || die "project not found: ${project_path}"

  if [[ -e "$dest" && "$FORCE" -ne 1 ]]; then
    if find "$dest" -mindepth 1 -maxdepth 1 | read -r _; then
      die "output exists (pass --force to overwrite): ${dest}"
    fi
  fi

  rm -rf "$dest"
  mkdir -p "$dest"

  echo "==> publishing ${name} (${project}, ${rid}, ${CONFIGURATION})"
  dotnet publish "$project_path" \
    -c "$CONFIGURATION" \
    -r "$rid" \
    --self-contained true \
    -o "$dest" \
    -p:PublishSingleFile=false \
    -p:DebugType=None \
    -p:DebugSymbols=false

  write_manifest "$dest" "$name" "$project" "$rid" "$tfm"
  copy_demo_docs "$dest"

  case "$name" in
    server-linux-x64|server-win-x64) verify_server_layout "$dest" "$rid" ;;
    client-win-x64) verify_client_layout "$dest" ;;
    editor-win-x64) verify_editor_layout "$dest" ;;
  esac

  archive_layout "$dest" "$name"
  echo "OK ${dest}"
}

mapfile -t RESOLVED < <(expand_targets "${TARGETS[@]}" | awk 'NF && !seen[$0]++')

for name in "${RESOLVED[@]}"; do
  case "$name" in
    server-linux-x64) publish_one "$name" "Frog.Server/Frog.Server.csproj" "linux-x64" "net8.0" ;;
    server-win-x64) publish_one "$name" "Frog.Server/Frog.Server.csproj" "win-x64" "net8.0" ;;
    client-win-x64) publish_one "$name" "Frog.Client/Frog.Client.csproj" "win-x64" "net8.0-windows" ;;
    editor-win-x64) publish_one "$name" "Frog.Editor/Frog.Editor.csproj" "win-x64" "net8.0-windows" ;;
  esac
done
