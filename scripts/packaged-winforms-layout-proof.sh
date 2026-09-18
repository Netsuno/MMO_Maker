#!/usr/bin/env bash
# P10-6 Linux proof: publish self-contained Windows client+editor, copy the zips
# *outside* the git tree, assert EXE + hostfxr + SHA-256 + package structure.
# Does NOT claim a WinForms/WPF process start on Linux. wine is attempted when
# present and recorded honestly; a wine failure is not a pass.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
STAMP="$(date -u +%Y%m%dT%H%M%SZ)-$$"
PUBLISH_ROOT="${FROG_P106_PUBLISH_ROOT:-/tmp/frog-p10-6-publish-${STAMP}}"
OUTSIDE_ROOT="${FROG_P106_OUTSIDE_ROOT:-/tmp/frog-p10-6-outside-${STAMP}}"
PROOF_FILE="${FROG_P106_PROOF_FILE:-${OUTSIDE_ROOT}/LAYOUT_PROOF.txt}"
TRY_WINE=1

USAGE='Usage: packaged-winforms-layout-proof.sh [options]

  Publish client-win-x64 + editor-win-x64 (self-contained), copy archives
  outside the repository, unzip, assert Frog.*.exe + hostfxr.dll + SHA-256.

  Linux cannot launch WinForms/WPF. This script is the layout proof.
  Windows launch proof: scripts/packaged-winforms-smoke.ps1

Options:
  --publish-root DIR   Default: /tmp/frog-p10-6-publish-<stamp>
  --outside-root DIR   Default: /tmp/frog-p10-6-outside-<stamp>
  --proof-file PATH    Default: <outside-root>/LAYOUT_PROOF.txt
  --skip-wine          Do not invoke wine even if it is on PATH
  -h, --help
'

die() {
  echo "error: $*" >&2
  exit 1
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --publish-root) PUBLISH_ROOT="${2:-}"; shift 2 ;;
    --outside-root) OUTSIDE_ROOT="${2:-}"; shift 2 ;;
    --proof-file) PROOF_FILE="${2:-}"; shift 2 ;;
    --skip-wine) TRY_WINE=0; shift ;;
    -h|--help) printf '%s' "$USAGE"; exit 0 ;;
    *) die "unknown argument: $1" ;;
  esac
done

git -C "$ROOT" rev-parse --is-inside-work-tree >/dev/null 2>&1 || die "expected a git checkout at ${ROOT}"
case "$PUBLISH_ROOT" in
  "$ROOT"|"$ROOT"/*) die "publish-root must be outside the git tree, got ${PUBLISH_ROOT}" ;;
esac
case "$OUTSIDE_ROOT" in
  "$ROOT"|"$ROOT"/*) die "outside-root must be outside the git tree, got ${OUTSIDE_ROOT}" ;;
esac

command -v python3 >/dev/null 2>&1 || die "python3 required"
command -v sha256sum >/dev/null 2>&1 || die "sha256sum required"

mkdir -p "$PUBLISH_ROOT" "$OUTSIDE_ROOT"
echo "==> publish client-win-x64 + editor-win-x64 → ${PUBLISH_ROOT}"
"${ROOT}/scripts/publish-frog.sh" \
  --target client-win-x64 \
  --target editor-win-x64 \
  --output-root "$PUBLISH_ROOT" \
  --force

copy_and_extract() {
  local name="$1"
  local zip="${PUBLISH_ROOT}/archives/${name}.zip"
  [[ -f "$zip" ]] || die "missing archive ${zip}"
  local dest="${OUTSIDE_ROOT}/${name}"
  mkdir -p "$dest"
  cp "$zip" "${OUTSIDE_ROOT}/${name}.zip"
  python3 - "$zip" "$dest" <<'PY'
import sys, zipfile
from pathlib import Path
zip_path, dest = Path(sys.argv[1]), Path(sys.argv[2])
with zipfile.ZipFile(zip_path) as zf:
    zf.extractall(dest)
PY
}

copy_and_extract client-win-x64
copy_and_extract editor-win-x64

# Archives store files under <layout>/... (see publish-frog.sh archive_layout).
client_dir="${OUTSIDE_ROOT}/client-win-x64/client-win-x64"
editor_dir="${OUTSIDE_ROOT}/editor-win-x64/editor-win-x64"
[[ -d "$client_dir" ]] || die "extracted client layout missing: ${client_dir}"
[[ -d "$editor_dir" ]] || die "extracted editor layout missing: ${editor_dir}"

require() {
  local dir="$1" file="$2"
  [[ -f "${dir}/${file}" ]] || die "missing ${file} in ${dir}"
}

require "$client_dir" Frog.Client.exe
require "$client_dir" Frog.Client.dll
require "$client_dir" hostfxr.dll
require "$client_dir" Frog.Core.dll
require "$client_dir" Frog.Application.dll
require "$client_dir" packaging-manifest.json
require "$client_dir" DEMO_WORLD.md
require "$client_dir" demo-world/LICENSES.md

require "$editor_dir" Frog.Editor.exe
require "$editor_dir" Frog.Editor.dll
require "$editor_dir" hostfxr.dll
require "$editor_dir" Frog.Persistence.PostgreSql.dll
require "$editor_dir" packaging-manifest.json
require "$editor_dir" DEMO_WORLD.md
require "$editor_dir" demo-world/LICENSES.md

if [[ -e "${client_dir}/appsettings.Local.json" ]]; then
  die "client layout must not contain appsettings.Local.json"
fi
if [[ -e "${editor_dir}/appsettings.Local.json" ]]; then
  die "editor layout must not contain appsettings.Local.json"
fi

sha256_file() {
  sha256sum "$1" | awk '{print $1}'
}

client_exe_sha="$(sha256_file "${client_dir}/Frog.Client.exe")"
editor_exe_sha="$(sha256_file "${editor_dir}/Frog.Editor.exe")"
client_zip_sha="$(sha256_file "${OUTSIDE_ROOT}/client-win-x64.zip")"
editor_zip_sha="$(sha256_file "${OUTSIDE_ROOT}/editor-win-x64.zip")"
sums_client="$(awk '/client-win-x64.zip$/ {print $1}' "${PUBLISH_ROOT}/archives/SHA256SUMS")"
sums_editor="$(awk '/editor-win-x64.zip$/ {print $1}' "${PUBLISH_ROOT}/archives/SHA256SUMS")"
[[ "$client_zip_sha" == "$sums_client" ]] || die "SHA256SUMS mismatch for client-win-x64.zip"
[[ "$editor_zip_sha" == "$sums_editor" ]] || die "SHA256SUMS mismatch for editor-win-x64.zip"

git_tip="$(git -C "$ROOT" rev-parse HEAD 2>/dev/null || echo unknown)"
wine_state="ABSENT"
wine_client="skipped"
wine_editor="skipped"

if [[ "$TRY_WINE" -eq 1 ]] && command -v wine >/dev/null 2>&1; then
  wine_state="PRESENT $(wine --version 2>/dev/null | head -n1)"
  set +e
  timeout 20s wine "${client_dir}/Frog.Client.exe" --smoke-launch >/tmp/frog-p10-6-wine-client.log 2>&1
  wine_client_rc=$?
  timeout 20s wine "${editor_dir}/Frog.Editor.exe" --smoke-launch >/tmp/frog-p10-6-wine-editor.log 2>&1
  wine_editor_rc=$?
  set -e
  wine_client="exit ${wine_client_rc} (not a WinForms pass on Linux)"
  wine_editor="exit ${wine_editor_rc} (not a WinForms pass on Linux)"
else
  wine_state="ABSENT — Linux cannot launch WinForms/WPF EXEs; use scripts/packaged-winforms-smoke.ps1 on Windows"
fi

{
  echo "P10-6 Linux layout proof"
  echo "gitSha=${git_tip}"
  echo "publishedUtc=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "publishRoot=${PUBLISH_ROOT}"
  echo "outsideRoot=${OUTSIDE_ROOT}  # not inside ${ROOT}"
  echo
  echo "client.exe=${client_dir}/Frog.Client.exe"
  echo "client.exe.sha256=${client_exe_sha}"
  echo "client.zip=${OUTSIDE_ROOT}/client-win-x64.zip"
  echo "client.zip.sha256=${client_zip_sha}"
  echo "editor.exe=${editor_dir}/Frog.Editor.exe"
  echo "editor.exe.sha256=${editor_exe_sha}"
  echo "editor.zip=${OUTSIDE_ROOT}/editor-win-x64.zip"
  echo "editor.zip.sha256=${editor_zip_sha}"
  echo
  echo "wine=${wine_state}"
  echo "wine.client=${wine_client}"
  echo "wine.editor=${wine_editor}"
  echo
  echo "PROUVE: presence EXE + hostfxr.dll + structure + SHA-256 (zip matches SHA256SUMS), copy hors depot"
  echo "RESTANT Linux: lancement process WinForms/WPF. Preuve Windows: scripts/packaged-winforms-smoke.ps1"
} | tee "$PROOF_FILE"

echo "OK layout proof written to ${PROOF_FILE}"
