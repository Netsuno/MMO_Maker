# Updates Phase 8 screenshot manifest SHA-256 hashes from CI artifacts after smoke runs.
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$ManifestPath = "docs/progress/phase-08-quests-events-advanced-creation/SCREENSHOT_MANIFEST.md"
)

$ErrorActionPreference = "Stop"
$manifestFull = Join-Path $RepoRoot $ManifestPath
if (-not (Test-Path $manifestFull)) {
    Write-Warning "Manifest not found: $manifestFull"
    exit 0
}

$dirs = @(
    @{ Dir = (Join-Path $RepoRoot "artifacts/phase-08-gameplay-client"); Prefix = "gameplay" },
    @{ Dir = (Join-Path $RepoRoot "artifacts/phase-08-editor"); Prefix = "editor" }
)

$hashes = @{}
foreach ($entry in $dirs) {
    if (-not (Test-Path $entry.Dir)) {
        continue
    }
    Get-ChildItem -Path $entry.Dir -Filter *.png -File | ForEach-Object {
        $sha = (Get-FileHash -Algorithm SHA256 -Path $_.FullName).Hash.ToLowerInvariant()
        $hashes[$_.Name] = $sha
        Write-Host ("{0}: {1}" -f $_.Name, $sha)
    }
}

if ($hashes.Count -eq 0) {
    Write-Warning "No Phase 8 screenshots found to hash."
    exit 0
}

$lines = Get-Content -Path $manifestFull
$updated = foreach ($line in $lines) {
    if ($line -match '^\|\s*`([^`]+)`\s*\|') {
        $file = $Matches[1]
        if ($hashes.ContainsKey($file)) {
            # Replace SHA-256 column placeholder _(CI)_ or existing hex with fresh hash
            $parts = $line -split '\|'
            if ($parts.Length -ge 5) {
                $parts[4] = " $($hashes[$file]) "
                ($parts -join '|').TrimEnd()
                continue
            }
        }
    }
    $line
}

Set-Content -Path $manifestFull -Value $updated -Encoding utf8
Write-Host "Updated $manifestFull with $($hashes.Count) SHA-256 hash(es)."
