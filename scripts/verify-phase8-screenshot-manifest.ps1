# CI gate for Phase 8 smoke PNGs vs committed SCREENSHOT_MANIFEST.md.
# Never rewrites the committed file.
#
# Policy (see SCREENSHOT_MANIFEST.md):
# - Required files must exist and be valid PNGs.
# - Dimensions must satisfy the per-row spec (exact WxH, ≥WxH, or W1–W2×H1–H2).
# - exact-sha rows must match SHA-256 (stable panel crops / editor browse).
# - present-dims rows do not gate SHA-256 (full-window / tab-shell pixels drift).
# - Client 01≠02 and 03≠04 must remain hash-distinct.
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$ManifestPath = "docs/progress/phase-08-quests-events-advanced-creation/SCREENSHOT_MANIFEST.md",
    [switch]$CopyManifestToArtifacts
)

$ErrorActionPreference = "Stop"
$update = Join-Path $PSScriptRoot "update-phase8-screenshot-manifest.ps1"
& $update -RepoRoot $RepoRoot -ManifestPath $ManifestPath -VerifyOnly -CopyManifestToArtifacts:$CopyManifestToArtifacts
exit $LASTEXITCODE
