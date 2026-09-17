# Updates or verifies Phase 8 smoke PNGs against
# docs/progress/phase-08-quests-events-advanced-creation/SCREENSHOT_MANIFEST.md.
#
# Gate policy (R2-6 follow-up):
# - exact-sha: file present + exact WxH + exact SHA-256. Used only for frames that
#   matched across consecutive Windows CI runs (client 02–05 panel crops, editor 01).
# - present-dims: file present + dimension spec (exact WxH, ≥WxH, or W1–W2×H1–H2).
#   SHA-256 is recorded in the generated table for diagnostics but is NOT gated.
#   Used for full-window / tab-shell frames whose pixels drift (log timestamps,
#   Guid.NewGuid in editor meta/list, caret/focus) even when smokes pass 24/24.
#
# Distinct-frame checks always run on actual artifact bytes (client 01≠02, 03≠04).
#
# Default (local/dev): rewrite committed exact-sha SHA-256 (and optional implementation
# SHA / CI URL) from artifacts. Does not rewrite present-dims SHA cells or dimension specs.
#
# -VerifyOnly (CI): do not rewrite the committed file. Compare file list + per-row gate.
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$ManifestPath = "docs/progress/phase-08-quests-events-advanced-creation/SCREENSHOT_MANIFEST.md",
    [switch]$VerifyOnly,
    [string]$ImplementationSha = "",
    [string]$CiUrl = "",
    [switch]$CopyManifestToArtifacts
)

$ErrorActionPreference = "Stop"

$script:KnownGates = @("exact-sha", "present-dims")
$script:UngatedShaTokens = @("—", "-", "n/a", "na", "not-gated", "")

function Get-PngSha256([string]$Path) {
    return (Get-FileHash -Algorithm SHA256 -Path $Path).Hash.ToLowerInvariant()
}

function Get-PngInfo([string]$Path) {
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 24) {
        return $null
    }
    $sig = [byte[]](137, 80, 78, 71, 13, 10, 26, 10)
    for ($i = 0; $i -lt 8; $i++) {
        if ($bytes[$i] -ne $sig[$i]) {
            return $null
        }
    }
    # PNG signature + IHDR length/type; width/height are big-endian at offset 16.
    $width = ($bytes[16] * 16777216) + ($bytes[17] * 65536) + ($bytes[18] * 256) + $bytes[19]
    $height = ($bytes[20] * 16777216) + ($bytes[21] * 65536) + ($bytes[22] * 256) + $bytes[23]
    return [pscustomobject]@{
        Width  = [int]$width
        Height = [int]$height
        Dims   = "{0}×{1}" -f $width, $height
        Bytes  = $bytes.Length
    }
}

function Parse-DimensionSpec([string]$Spec) {
    $s = $Spec.Trim()
    if ($s -match '^≥\s*(\d+)\s*[×xX]\s*(\d+)$' -or $s -match '^>=\s*(\d+)\s*[×xX]\s*(\d+)$') {
        return [pscustomobject]@{
            Mode      = "min"
            MinWidth  = [int]$Matches[1]
            MinHeight = [int]$Matches[2]
            MaxWidth  = [int]::MaxValue
            MaxHeight = [int]::MaxValue
            Raw       = $s
        }
    }
    if ($s -match '^(\d+)\s*[–-]\s*(\d+)\s*[×xX]\s*(\d+)\s*[–-]\s*(\d+)$') {
        return [pscustomobject]@{
            Mode      = "range"
            MinWidth  = [int]$Matches[1]
            MaxWidth  = [int]$Matches[2]
            MinHeight = [int]$Matches[3]
            MaxHeight = [int]$Matches[4]
            Raw       = $s
        }
    }
    if ($s -match '^(\d+)\s*[×xX]\s*(\d+)$') {
        $w = [int]$Matches[1]
        $h = [int]$Matches[2]
        return [pscustomobject]@{
            Mode      = "exact"
            MinWidth  = $w
            MaxWidth  = $w
            MinHeight = $h
            MaxHeight = $h
            Raw       = $s
        }
    }
    return $null
}

function Test-DimensionSpec($Spec, [int]$Width, [int]$Height) {
    if ($null -eq $Spec) {
        return "unparsed dimension spec"
    }
    if ($Width -lt $Spec.MinWidth -or $Width -gt $Spec.MaxWidth -or
        $Height -lt $Spec.MinHeight -or $Height -gt $Spec.MaxHeight) {
        switch ($Spec.Mode) {
            "exact" { return ("expected exact {0}×{1}" -f $Spec.MinWidth, $Spec.MinHeight) }
            "min" { return ("expected ≥{0}×{1}" -f $Spec.MinWidth, $Spec.MinHeight) }
            default { return ("expected {0}–{1}×{2}–{3}" -f $Spec.MinWidth, $Spec.MaxWidth, $Spec.MinHeight, $Spec.MaxHeight) }
        }
    }
    return $null
}

function Test-IsUngatedSha([string]$Sha) {
    $t = $Sha.Trim().ToLowerInvariant()
    return $script:UngatedShaTokens -contains $t
}

function Read-CommittedManifestEntries([string]$ManifestFullPath) {
    $section = $null
    $entries = @()
    $lineNo = 0
    foreach ($line in Get-Content -Path $ManifestFullPath) {
        $lineNo++
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
        if ($parts.Length -lt 6) {
            throw "Manifest row at line $lineNo is too short (need Filename|Description|Dimensions|Gate|SHA-256): $line"
        }
        $dimsRaw = $parts[3].Trim()
        $gate = $parts[4].Trim().ToLowerInvariant()
        $sha = $parts[5].Trim().ToLowerInvariant()
        if ($script:KnownGates -notcontains $gate) {
            throw "Manifest row ${file}: unknown Gate '$gate' (expected exact-sha or present-dims)."
        }
        $dimSpec = Parse-DimensionSpec $dimsRaw
        if ($null -eq $dimSpec) {
            throw "Manifest row ${file}: unparsed Dimensions '$dimsRaw'."
        }
        if ($gate -eq "exact-sha") {
            if ($dimSpec.Mode -ne "exact") {
                throw "Manifest row ${file}: exact-sha gate requires exact WxH dimensions, got '$dimsRaw'."
            }
            if ((Test-IsUngatedSha $sha) -or ($sha -notmatch '^[0-9a-f]{64}$')) {
                throw "Manifest row ${file}: exact-sha gate requires a 64-char SHA-256, got '$sha'."
            }
        }
        $entries += [pscustomobject]@{
            Section = $section
            File    = $file
            Dims    = $dimsRaw
            DimSpec = $dimSpec
            Gate    = $gate
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
$hashMismatches = @()
$dimMismatches = @()
$invalidPngs = @()
$hashDriftNotes = @()
$matchedExactSha = 0
$matchedPresentDims = 0

foreach ($entry in $committed) {
    $dir = $dirs[$entry.Section]
    $path = Join-Path $dir $entry.File
    $key = "{0}/{1}" -f $entry.Section, $entry.File
    if (-not (Test-Path $path)) {
        $missing += $key
        continue
    }
    $info = Get-PngInfo $path
    if ($null -eq $info -or $info.Bytes -lt 24 -or $info.Width -lt 1 -or $info.Height -lt 1) {
        $invalidPngs += $key
        continue
    }
    $sha = Get-PngSha256 $path
    $actualByKey[$key] = [pscustomobject]@{
        Section = $entry.Section
        File    = $entry.File
        Sha     = $sha
        Dims    = $info.Dims
        Width   = $info.Width
        Height  = $info.Height
        Path    = $path
        Gate    = $entry.Gate
    }
    Write-Host ("{0}: {1} {2} gate={3}" -f $key, $sha, $info.Dims, $entry.Gate)

    $dimErr = Test-DimensionSpec $entry.DimSpec $info.Width $info.Height
    if ($dimErr) {
        $dimMismatches += [pscustomobject]@{
            Key       = $key
            Actual    = $info.Dims
            Expected  = $entry.Dims
            Detail    = $dimErr
        }
    }

    if ($entry.Gate -eq "exact-sha") {
        if ($sha -ne $entry.Sha) {
            $hashMismatches += [pscustomobject]@{
                Key       = $key
                Committed = $entry.Sha
                Actual    = $sha
            }
        }
        else {
            $matchedExactSha++
        }
    }
    else {
        $matchedPresentDims++
        if (-not (Test-IsUngatedSha $entry.Sha) -and $sha -ne $entry.Sha) {
            $hashDriftNotes += [pscustomobject]@{
                Key       = $key
                Committed = $entry.Sha
                Actual    = $sha
            }
        }
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
    "Gate policy: exact-sha rows must match SHA-256; present-dims rows record SHA for diagnostics only.",
    "",
    "## Client (`artifacts/phase-08-gameplay-client/`)",
    "",
    "| Filename | Dimensions | Gate | SHA-256 |",
    "| --- | --- | --- | --- |"
)
foreach ($entry in ($committed | Where-Object { $_.Section -eq "client" })) {
    $key = "client/$($entry.File)"
    if ($actualByKey.ContainsKey($key)) {
        $row = $actualByKey[$key]
        $generatedLines += "| ``$($row.File)`` | $($row.Dims) | $($entry.Gate) | $($row.Sha) |"
    }
    else {
        $generatedLines += "| ``$($entry.File)`` | MISSING | $($entry.Gate) | MISSING |"
    }
}
$generatedLines += @(
    "",
    "## Editor (`artifacts/phase-08-editor/`)",
    "",
    "| Filename | Dimensions | Gate | SHA-256 |",
    "| --- | --- | --- | --- |"
)
foreach ($entry in ($committed | Where-Object { $_.Section -eq "editor" })) {
    $key = "editor/$($entry.File)"
    if ($actualByKey.ContainsKey($key)) {
        $row = $actualByKey[$key]
        $generatedLines += "| ``$($row.File)`` | $($row.Dims) | $($entry.Gate) | $($row.Sha) |"
    }
    else {
        $generatedLines += "| ``$($entry.File)`` | MISSING | $($entry.Gate) | MISSING |"
    }
}
Set-Content -Path $generatedPath -Value $generatedLines -Encoding utf8
Write-Host "Wrote temporary manifest $generatedPath"

function Test-DistinctPairs([hashtable]$ActualByKey, [switch]$FailOnMissing) {
    $failures = @()
    $distinctPairs = @(
        @{ A = "client/01-phase8-tab.png"; B = "client/02-dialogue-choices.png" },
        @{ A = "client/03-quest-journal.png"; B = "client/04-environment.png" }
    )
    foreach ($pair in $distinctPairs) {
        $hasA = $ActualByKey.ContainsKey($pair.A)
        $hasB = $ActualByKey.ContainsKey($pair.B)
        if (-not $hasA -or -not $hasB) {
            if ($FailOnMissing) {
                $failures += ("Claimed-different frames missing for distinctness check: {0} / {1}" -f $pair.A, $pair.B)
            }
            continue
        }
        if ($ActualByKey[$pair.A].Sha -eq $ActualByKey[$pair.B].Sha) {
            $failures += ("Claimed-different client frames are not hash-distinct: {0} SHA == {1} SHA ({2})" -f $pair.A, $pair.B, $ActualByKey[$pair.A].Sha)
        }
    }
    return $failures
}

if ($VerifyOnly) {
    $failed = $false
    $distinctFailures = @(Test-DistinctPairs $actualByKey -FailOnMissing:$false)
    if ($missing.Count -gt 0 -or $hashMismatches.Count -gt 0 -or $dimMismatches.Count -gt 0 -or
        $invalidPngs.Count -gt 0 -or $distinctFailures.Count -gt 0) {
        $failed = $true
        Write-Host ""
        Write-Host "Phase 8 screenshot manifest verification FAILED."
        Write-Host ("Committed file: {0}" -f $manifestFull)
        Write-Host ("Gates: {0} exact-sha matched, {1} present-dims present, {2} committed row(s)." -f $matchedExactSha, $matchedPresentDims, $committed.Count)
        if ($missing.Count -gt 0) {
            Write-Host ""
            Write-Host "Missing (committed, not in artifacts):"
            foreach ($item in $missing) {
                Write-Host ("  {0}" -f $item)
            }
        }
        if ($invalidPngs.Count -gt 0) {
            Write-Host ""
            Write-Host "Invalid / truncated PNGs:"
            foreach ($item in $invalidPngs) {
                Write-Host ("  {0}" -f $item)
            }
        }
        if ($dimMismatches.Count -gt 0) {
            Write-Host ""
            Write-Host "Dimension mismatches:"
            foreach ($item in $dimMismatches) {
                Write-Host ("  {0}" -f $item.Key)
                Write-Host ("    committed: {0} ({1})" -f $item.Expected, $item.Detail)
                Write-Host ("    actual:    {0}" -f $item.Actual)
            }
        }
        if ($hashMismatches.Count -gt 0) {
            Write-Host ""
            Write-Host "exact-sha hash mismatches:"
            foreach ($item in $hashMismatches) {
                Write-Host ("  {0}" -f $item.Key)
                Write-Host ("    committed: {0}" -f $item.Committed)
                Write-Host ("    actual:    {0}" -f $item.Actual)
            }
        }
        foreach ($msg in $distinctFailures) {
            Write-Host ""
            Write-Host $msg
        }
    }
    if ($hashDriftNotes.Count -gt 0) {
        Write-Host ""
        Write-Host "present-dims SHA drift (diagnostic only, not a failure):"
        foreach ($item in $hashDriftNotes) {
            Write-Host ("  {0}" -f $item.Key)
            Write-Host ("    last recorded: {0}" -f $item.Committed)
            Write-Host ("    actual:        {0}" -f $item.Actual)
        }
    }
    if ($extra.Count -gt 0) {
        Write-Host ""
        Write-Host "Extra PNGs in artifacts (not in committed manifest; ignored):"
        foreach ($item in $extra) {
            Write-Host ("  {0}" -f $item)
        }
    }
    if ($failed) {
        Write-Error "Phase 8 screenshot manifest does not match smoke artifacts."
        exit 1
    }
    Write-Host ""
    Write-Host ("Phase 8 screenshot manifest verification OK ({0} file(s): {1} exact-sha, {2} present-dims; distinct-frame checks passed)." -f $committed.Count, $matchedExactSha, $matchedPresentDims)
    exit 0
}

if ($missing.Count -gt 0) {
    Write-Warning ("Skipping update for missing artifact(s): {0}" -f ($missing -join ", "))
}

$distinctUpdateFailures = @(Test-DistinctPairs $actualByKey -FailOnMissing:$false)
if ($distinctUpdateFailures.Count -gt 0) {
    Write-Error ($distinctUpdateFailures -join " ")
    exit 1
}

$impl = $ImplementationSha.Trim()
$ci = $CiUrl.Trim()
$lines = Get-Content -Path $manifestFull
$updatedExact = 0
$updated = foreach ($line in $lines) {
    if ($line -match '^\|\s*`([^`]+)`\s*\|') {
        $file = $Matches[1]
        $parts = $line -split '\|'
        if ($parts.Length -ge 6) {
            $actual = $null
            foreach ($section in @("client", "editor")) {
                $key = "{0}/{1}" -f $section, $file
                if ($actualByKey.ContainsKey($key)) {
                    $actual = $actualByKey[$key]
                    break
                }
            }
            if ($null -ne $actual) {
                $gate = $parts[4].Trim().ToLowerInvariant()
                if ($gate -eq "exact-sha") {
                    $parts[5] = " $($actual.Sha) "
                    if ($actual.Dims) {
                        $parts[3] = " $($actual.Dims) "
                    }
                    $updatedExact++
                }
                if ($impl -and $parts.Length -ge 7) {
                    $short = if ($impl.Length -gt 7) { $impl.Substring(0, 7) } else { $impl }
                    $parts[6] = " $short "
                }
                if ($ci -and $parts.Length -ge 8) {
                    $parts[7] = " $ci "
                }
                ($parts -join '|').TrimEnd()
                continue
            }
        }
    }
    $line
}

Set-Content -Path $manifestFull -Value $updated -Encoding utf8
Write-Host "Updated $manifestFull ($updatedExact exact-sha hash(es); present-dims SHA/dimensions left unchanged)."
