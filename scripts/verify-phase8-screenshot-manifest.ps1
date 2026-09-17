# CI gate: exact SHA-256 + file-list compare of Phase 8 smoke PNGs against the
# committed SCREENSHOT_MANIFEST.md. Never rewrites the committed file.
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$ManifestPath = "docs/progress/phase-08-quests-events-advanced-creation/SCREENSHOT_MANIFEST.md",
    [switch]$CopyManifestToArtifacts
)

$ErrorActionPreference = "Stop"
$update = Join-Path $PSScriptRoot "update-phase8-screenshot-manifest.ps1"
& $update -RepoRoot $RepoRoot -ManifestPath $ManifestPath -VerifyOnly -CopyManifestToArtifacts:$CopyManifestToArtifacts
exit $LASTEXITCODE
