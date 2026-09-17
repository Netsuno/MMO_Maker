# Updates or verifies Phase 8 screenshot SHA-256 hashes against
# docs/progress/phase-08-quests-events-advanced-creation/SCREENSHOT_MANIFEST.md.
#
# Default (local/dev): rewrite committed SHA-256 (and optional implementation SHA / CI URL)
# from artifacts produced by Windows Phase 8 smokes.
#
# -VerifyOnly (CI): do not rewrite the committed file. Hash PNGs, compare file list + SHA-256
# columns exactly, exit non-zero with a mismatch report.
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$ManifestPath = "docs/progress/phase-08-quests-events-advanced-creation/SCREENSHOT_MANIFEST.md",
    [switch]$VerifyOnly,
    [string]$ImplementationSha = "",
    [string]$CiUrl = "",
    [switch]$CopyManifestToArtifacts
)

$ErrorActionPreference = "Stop"

function Get-PngSha256([string]$Path) {
    return (Get-FileHash -Algorithm SHA256 -Path $Path).Hash.ToLowerInvariant()
}

function Get-PngDimensions([string]$Path) {
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 24) {
        return $null
    }
    # PNG signature + IHDR length/type; width/height are big-endian at offset 16.
    $width = ($bytes[16] * 16777216) + ($bytes[17] * 65536) + ($bytes[18] * 256) + $bytes[19]
    $height = ($bytes[20] * 16777216) + ($bytes[21] * 65536) + ($bytes[22] * 256) + $bytes[23]
    return "{0}×{1}" -f $width, $height
}

function Read-CommittedManifestEntries([string]$ManifestFullPath) {
    $section = $null
    $entries = @()
    foreach ($line in Get-Content -Path $ManifestFullPath) {
        if ($line -match '^##\s+Client\b') {
            $section = "client"
            continue
        }
        if ($line -match '^##\s+Editor\b') {
            $section = "editor"
            continue
        }
        if ($line -notmatch '^\|\s*`([^`]+)`\s*\|') {
            continue
        }
        if ($null -eq $section) {
            continue
        }
        $file = [string]$Matches[1]
        $parts = $line -split '\|'
        if ($parts.Length -lt 5) {
            continue
        }
        $sha = $parts[4].Trim().ToLowerInvariant()
        $entries += [pscustomobject]@{
            Section = $section
            File    = $file
            Sha     = $sha
            Line    = $line
            Parts   = $parts
        }
    }
    return $entries
}

function Get-SectionDirs([string]$Root) {
    return @{
        client = (Join-Path $Root "artifacts/phase-08-gameplay-client")
        editor = (Join-Path $Root "artifacts/phase-08-editor")
    }
}

function Copy-ManifestIntoArtifactDirs([string]$ManifestFullPath, [hashtable]$Dirs) {
    foreach ($section in @("client", "editor")) {
        $dir = $Dirs[$section]
        if (-not (Test-Path $dir)) {
            New-Item -ItemType Directory -Force -Path $dir | Out-Null
        }
        Copy-Item -Path $ManifestFullPath -Destination (Join-Path $dir "SCREENSHOT_MANIFEST.md") -Force
        Write-Host ("Copied manifest → {0}" -f (Join-Path $dir "SCREENSHOT_MANIFEST.md"))
    }
}

$manifestFull = Join-Path $RepoRoot $ManifestPath
if (-not (Test-Path $manifestFull)) {
    Write-Error "Manifest not found: $manifestFull"
    exit 1
}

$dirs = Get-SectionDirs $RepoRoot

if ($CopyManifestToArtifacts) {
    Copy-ManifestIntoArtifactDirs $manifestFull $dirs
}

$committed = @(Read-CommittedManifestEntries $manifestFull)
if ($committed.Count -eq 0) {
    Write-Error "Committed manifest has no screenshot rows: $manifestFull"
    exit 1
}

$actualByKey = @{}
$missing = @()
$mismatches = @()
$matched = 0

foreach ($entry in $committed) {
    $dir = $dirs[$entry.Section]
    $path = Join-Path $dir $entry.File
    $key = "{0}/{1}" -f $entry.Section, $entry.File
    if (-not (Test-Path $path)) {
        $missing += $key
        continue
    }
    $sha = Get-PngSha256 $path
    $dims = Get-PngDimensions $path
    $actualByKey[$key] = [pscustomobject]@{
        Section = $entry.Section
        File    = $entry.File
        Sha     = $sha
        Dims    = $dims
        Path    = $path
    }
    Write-Host ("{0}: {1} {2}" -f $key, $sha, $dims)
    if ($sha -ne $entry.Sha) {
        $mismatches += [pscustomobject]@{
            Key        = $key
            Committed  = $entry.Sha
            Actual     = $sha
        }
    }
    else {
        $matched++
    }
}

$artifactPngs = @()
foreach ($section in @("client", "editor")) {
    $dir = $dirs[$section]
    if (Test-Path $dir) {
        Get-ChildItem -Path $dir -Filter *.png -File | ForEach-Object {
            $artifactPngs += [pscustomobject]@{
                Section = $section
                File    = $_.Name
            }
        }
    }
}

$committedNames = @($committed | ForEach-Object { "{0}/{1}" -f $_.Section, $_.File })
$extra = @()
foreach ($png in $artifactPngs) {
    $key = "{0}/{1}" -f $png.Section, $png.File
    if ($committedNames -notcontains $key) {
        $extra += $key
    }
}

$generatedDir = Join-Path $RepoRoot "artifacts"
New-Item -ItemType Directory -Force -Path $generatedDir | Out-Null
$generatedPath = Join-Path $generatedDir "phase-08-screenshot-manifest.generated.md"
$generatedLines = @(
    "# Phase 8 — generated screenshot hashes (temporary, not committed)",
    "",
    "Produced by scripts/update-phase8-screenshot-manifest.ps1 from smoke PNG artifacts.",
    "",
    "## Client (`artifacts/phase-08-gameplay-client/`)",
    "",
    "| Filename | Dimensions | SHA-256 |",
    "| --- | --- | --- |"
)
foreach ($entry in ($committed | Where-Object { $_.Section -eq "client" })) {
    $key = "client/$($entry.File)"
    if ($actualByKey.ContainsKey($key)) {
        $row = $actualByKey[$key]
        $generatedLines += "| ``$($row.File)`` | $($row.Dims) | $($row.Sha) |"
    }
    else {
        $generatedLines += "| ``$($entry.File)`` | MISSING | MISSING |"
    }
}
$generatedLines += @(
    "",
    "## Editor (`artifacts/phase-08-editor/`)",
    "",
    "| Filename | Dimensions | SHA-256 |",
    "| --- | --- | --- |"
)
foreach ($entry in ($committed | Where-Object { $_.Section -eq "editor" })) {
    $key = "editor/$($entry.File)"
    if ($actualByKey.ContainsKey($key)) {
        $row = $actualByKey[$key]
        $generatedLines += "| ``$($row.File)`` | $($row.Dims) | $($row.Sha) |"
    }
    else {
        $generatedLines += "| ``$($entry.File)`` | MISSING | MISSING |"
    }
}
Set-Content -Path $generatedPath -Value $generatedLines -Encoding utf8
Write-Host "Wrote temporary manifest $generatedPath"

if ($VerifyOnly) {
    $failed = $false
    if ($missing.Count -gt 0 -or $mismatches.Count -gt 0) {
        $failed = $true
        Write-Host ""
        Write-Host "Phase 8 screenshot manifest verification FAILED."
        Write-Host ("Committed file: {0}" -f $manifestFull)
        Write-Host ("Compared SHA-256 columns and file list ({0} committed row(s), {1} matched)." -f $committed.Count, $matched)
        if ($missing.Count -gt 0) {
            Write-Host ""
            Write-Host "Missing (committed, not in artifacts):"
            foreach ($item in $missing) {
                Write-Host ("  {0}" -f $item)
            }
        }
        if ($mismatches.Count -gt 0) {
            Write-Host ""
            Write-Host "Hash mismatches:"
            foreach ($item in $mismatches) {
                Write-Host ("  {0}" -f $item.Key)
                Write-Host ("    committed: {0}" -f $item.Committed)
                Write-Host ("    actual:    {0}" -f $item.Actual)
            }
        }
    }
    if ($extra.Count -gt 0) {
        Write-Host ""
        Write-Host "Extra PNGs in artifacts (not in committed manifest; ignored for exact SHA compare):"
        foreach ($item in $extra) {
            Write-Host ("  {0}" -f $item)
        }
    }
    $distinctPairs = @(
        @{ A = "client/01-phase8-tab.png"; B = "client/02-dialogue-choices.png" },
        @{ A = "client/03-quest-journal.png"; B = "client/04-environment.png" }
    )
    foreach ($pair in $distinctPairs) {
        if ($actualByKey.ContainsKey($pair.A) -and $actualByKey.ContainsKey($pair.B)) {
            if ($actualByKey[$pair.A].Sha -eq $actualByKey[$pair.B].Sha) {
                $failed = $true
                Write-Host ""
                Write-Host ("Claimed-different client frames are not hash-distinct: {0} SHA == {1} SHA ({2})" -f $pair.A, $pair.B, $actualByKey[$pair.A].Sha)
            }
        }
    }
    if ($failed) {
        Write-Error "Phase 8 screenshot manifest does not match smoke artifacts."
        exit 1
    }
    Write-Host ""
    Write-Host ("Phase 8 screenshot manifest verification OK ({0} file(s), exact SHA-256 match)." -f $committed.Count)
    exit 0
}

if ($missing.Count -gt 0) {
    Write-Warning ("Skipping update for missing artifact(s): {0}" -f ($missing -join ", "))
}

foreach ($pair in @(
        @{ A = "client/01-phase8-tab.png"; B = "client/02-dialogue-choices.png" },
        @{ A = "client/03-quest-journal.png"; B = "client/04-environment.png" }
    )) {
    if ($actualByKey.ContainsKey($pair.A) -and $actualByKey.ContainsKey($pair.B) -and
        $actualByKey[$pair.A].Sha -eq $actualByKey[$pair.B].Sha) {
        Write-Error ("Refusing to update manifest: claimed-different frames share SHA-256 {0} ({1} / {2})" -f $actualByKey[$pair.A].Sha, $pair.A, $pair.B)
        exit 1
    }
}

$impl = $ImplementationSha.Trim()
$ci = $CiUrl.Trim()
$lines = Get-Content -Path $manifestFull
$updated = foreach ($line in $lines) {
    if ($line -match '^\|\s*`([^`]+)`\s*\|') {
        $file = $Matches[1]
        $parts = $line -split '\|'
        if ($parts.Length -ge 5) {
            $actual = $null
            foreach ($section in @("client", "editor")) {
                $key = "{0}/{1}" -f $section, $file
                if ($actualByKey.ContainsKey($key)) {
                    $actual = $actualByKey[$key]
                    break
                }
            }
            if ($null -ne $actual) {
                $parts[4] = " $($actual.Sha) "
                if ($parts.Length -ge 4 -and $actual.Dims) {
                    $parts[3] = " $($actual.Dims) "
                }
                if ($impl -and $parts.Length -ge 6) {
                    $short = if ($impl.Length -gt 7) { $impl.Substring(0, 7) } else { $impl }
                    $parts[5] = " $short "
                }
                if ($ci -and $parts.Length -ge 7) {
                    $parts[6] = " $ci "
                }
                ($parts -join '|').TrimEnd()
                continue
            }
        }
    }
    $line
}

Set-Content -Path $manifestFull -Value $updated -Encoding utf8
Write-Host "Updated $manifestFull with $($actualByKey.Count) SHA-256 hash(es)."
