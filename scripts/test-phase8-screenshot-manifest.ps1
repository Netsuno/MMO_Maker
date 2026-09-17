# Synthetic fixture tests for the Phase 8 screenshot manifest gate.
# Does not run WinForms smokes. Safe on Linux pwsh and Windows CI.
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"
$verify = Join-Path $PSScriptRoot "verify-phase8-screenshot-manifest.ps1"
$python = $null
foreach ($name in @("python3", "python", "py")) {
    $cmd = Get-Command $name -ErrorAction SilentlyContinue
    if ($cmd) {
        $python = $cmd.Source
        break
    }
}

function Write-Png([string]$Path, [int]$Width, [int]$Height, [int]$R, [int]$G, [int]$B) {
    if (-not $python) {
        throw "python3/python is required to synthesize PNG fixtures."
    }
    & $python -c @"
import struct, zlib, sys
path, w, h, r, g, b = sys.argv[1], int(sys.argv[2]), int(sys.argv[3]), int(sys.argv[4]), int(sys.argv[5]), int(sys.argv[6])
def chunk(tag, data):
    return struct.pack('>I', len(data)) + tag + data + struct.pack('>I', zlib.crc32(tag + data) & 0xffffffff)
raw = b''.join(b'\x00' + bytes([r, g, b]) * w for _ in range(h))
ihdr = struct.pack('>IIBBBBB', w, h, 8, 2, 0, 0, 0)
png = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', ihdr) + chunk(b'IDAT', zlib.compress(raw)) + chunk(b'IEND', b'')
open(path, 'wb').write(png)
"@ $Path $Width $Height $R $G $B
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to write PNG $Path"
    }
}

function New-TempRoot {
    $root = Join-Path ([System.IO.Path]::GetTempPath()) ("p8-manifest-" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Force -Path $root | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $root "docs/progress/phase-08-quests-events-advanced-creation") | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $root "artifacts/phase-08-gameplay-client") | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $root "artifacts/phase-08-editor") | Out-Null
    return $root
}

function Get-Sha([string]$Path) {
    return (Get-FileHash -Algorithm SHA256 -Path $Path).Hash.ToLowerInvariant()
}

function Write-Manifest([string]$Root, $Rows) {
    $path = Join-Path $Root "docs/progress/phase-08-quests-events-advanced-creation/SCREENSHOT_MANIFEST.md"
    $lines = @(
        "# fixture",
        "",
        "## Client",
        "",
        "| Filename | Description | Dimensions | Gate | SHA-256 | Implementation SHA | CI URL |",
        "| --- | --- | --- | --- | --- | --- | --- |"
    )
    foreach ($row in ($Rows | Where-Object { $_.Section -eq "client" })) {
        $lines += "| ``$($row.File)`` | $($row.Desc) | $($row.Dims) | $($row.Gate) | $($row.Sha) | fixture | n/a |"
    }
    $lines += @(
        "",
        "## Editor",
        "",
        "| Filename | Description | Dimensions | Gate | SHA-256 | Implementation SHA | CI URL |",
        "| --- | --- | --- | --- | --- | --- | --- |"
    )
    foreach ($row in ($Rows | Where-Object { $_.Section -eq "editor" })) {
        $lines += "| ``$($row.File)`` | $($row.Desc) | $($row.Dims) | $($row.Gate) | $($row.Sha) | fixture | n/a |"
    }
    Set-Content -Path $path -Value $lines -Encoding utf8
}

function Invoke-Verify([string]$Root) {
    try {
        & $verify -RepoRoot $Root
        if ($null -eq $LASTEXITCODE) { return 0 }
        return $LASTEXITCODE
    }
    catch {
        Write-Host $_
        return 1
    }
}

$failed = 0
function Expect-Exit([string]$Name, [int]$Expected, [scriptblock]$Body) {
    $root = New-TempRoot
    try {
        & $Body $root
        $code = Invoke-Verify $root
        if ($code -ne $Expected) {
            Write-Host "FAIL $Name : exit $code expected $Expected"
            $script:failed++
        }
        else {
            Write-Host "OK   $Name"
        }
    }
    finally {
        Remove-Item -Recurse -Force $root -ErrorAction SilentlyContinue
    }
}

# Happy path: exact-sha match + present-dims hash drift still passes + distinct 01/02 and 03/04.
Expect-Exit "happy present-dims drift" 0 {
    param($root)
    $client = Join-Path $root "artifacts/phase-08-gameplay-client"
    $editor = Join-Path $root "artifacts/phase-08-editor"
    Write-Png (Join-Path $client "01-phase8-tab.png") 352 480 10 20 30
    Write-Png (Join-Path $client "02-dialogue-choices.png") 324 150 40 50 60
    Write-Png (Join-Path $client "03-quest-journal.png") 324 80 70 80 90
    Write-Png (Join-Path $client "04-environment.png") 324 150 15 25 35
    Write-Png (Join-Path $client "05-craft-panel.png") 324 150 45 55 65
    Write-Png (Join-Path $client "06-reconnect-usable.png") 352 480 80 10 10
    Write-Png (Join-Path $editor "01-phase8-content-browse.png") 996 679 1 2 3
    Write-Png (Join-Path $editor "02-dialogue-structured-edit.png") 996 679 4 5 6
    $sha02 = Get-Sha (Join-Path $client "02-dialogue-choices.png")
    $sha03 = Get-Sha (Join-Path $client "03-quest-journal.png")
    $sha04 = Get-Sha (Join-Path $client "04-environment.png")
    $sha05 = Get-Sha (Join-Path $client "05-craft-panel.png")
    $shaEd = Get-Sha (Join-Path $editor "01-phase8-content-browse.png")
    Write-Manifest $root @(
        @{ Section = "client"; File = "01-phase8-tab.png"; Desc = "tab"; Dims = "300–400×250–700"; Gate = "present-dims"; Sha = "deadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef" }
        @{ Section = "client"; File = "02-dialogue-choices.png"; Desc = "dlg"; Dims = "324×150"; Gate = "exact-sha"; Sha = $sha02 }
        @{ Section = "client"; File = "03-quest-journal.png"; Desc = "q"; Dims = "324×80"; Gate = "exact-sha"; Sha = $sha03 }
        @{ Section = "client"; File = "04-environment.png"; Desc = "env"; Dims = "324×150"; Gate = "exact-sha"; Sha = $sha04 }
        @{ Section = "client"; File = "05-craft-panel.png"; Desc = "craft"; Dims = "324×150"; Gate = "exact-sha"; Sha = $sha05 }
        @{ Section = "client"; File = "06-reconnect-usable.png"; Desc = "re"; Dims = "300–400×250–700"; Gate = "present-dims"; Sha = "—" }
        @{ Section = "editor"; File = "01-phase8-content-browse.png"; Desc = "browse"; Dims = "996×679"; Gate = "exact-sha"; Sha = $shaEd }
        @{ Section = "editor"; File = "02-dialogue-structured-edit.png"; Desc = "edit"; Dims = "996×679"; Gate = "present-dims"; Sha = "—" }
    )
}

# exact-sha mismatch fails
Expect-Exit "exact-sha mismatch" 1 {
    param($root)
    $client = Join-Path $root "artifacts/phase-08-gameplay-client"
    Write-Png (Join-Path $client "01-phase8-tab.png") 352 480 1 1 1
    Write-Png (Join-Path $client "02-dialogue-choices.png") 324 150 2 2 2
    $sha01 = Get-Sha (Join-Path $client "01-phase8-tab.png")
    Write-Manifest $root @(
        @{ Section = "client"; File = "01-phase8-tab.png"; Desc = "tab"; Dims = "352×480"; Gate = "exact-sha"; Sha = $sha01 }
        @{ Section = "client"; File = "02-dialogue-choices.png"; Desc = "dlg"; Dims = "324×150"; Gate = "exact-sha"; Sha = ("0" * 64) }
    )
}

# missing required PNG fails
Expect-Exit "missing file" 1 {
    param($root)
    $client = Join-Path $root "artifacts/phase-08-gameplay-client"
    Write-Png (Join-Path $client "02-dialogue-choices.png") 324 150 2 2 2
    $sha02 = Get-Sha (Join-Path $client "02-dialogue-choices.png")
    Write-Manifest $root @(
        @{ Section = "client"; File = "01-phase8-tab.png"; Desc = "tab"; Dims = "300–400×250–700"; Gate = "present-dims"; Sha = "—" }
        @{ Section = "client"; File = "02-dialogue-choices.png"; Desc = "dlg"; Dims = "324×150"; Gate = "exact-sha"; Sha = $sha02 }
    )
}

# present-dims full-shell 1044×759 rejected by tab range
Expect-Exit "full-shell rejected as tab crop" 1 {
    param($root)
    $client = Join-Path $root "artifacts/phase-08-gameplay-client"
    Write-Png (Join-Path $client "01-phase8-tab.png") 1044 759 3 3 3
    Write-Png (Join-Path $client "02-dialogue-choices.png") 324 150 4 4 4
    $sha02 = Get-Sha (Join-Path $client "02-dialogue-choices.png")
    Write-Manifest $root @(
        @{ Section = "client"; File = "01-phase8-tab.png"; Desc = "tab"; Dims = "300–400×250–700"; Gate = "present-dims"; Sha = "—" }
        @{ Section = "client"; File = "02-dialogue-choices.png"; Desc = "dlg"; Dims = "324×150"; Gate = "exact-sha"; Sha = $sha02 }
    )
}

# present-dims panel 324×150 rejected as tab (too short)
Expect-Exit "panel rejected as tab crop" 1 {
    param($root)
    $client = Join-Path $root "artifacts/phase-08-gameplay-client"
    Write-Png (Join-Path $client "01-phase8-tab.png") 324 150 5 5 5
    Write-Png (Join-Path $client "02-dialogue-choices.png") 324 150 6 6 6
    $sha02 = Get-Sha (Join-Path $client "02-dialogue-choices.png")
    Write-Manifest $root @(
        @{ Section = "client"; File = "01-phase8-tab.png"; Desc = "tab"; Dims = "300–400×250–700"; Gate = "present-dims"; Sha = "—" }
        @{ Section = "client"; File = "02-dialogue-choices.png"; Desc = "dlg"; Dims = "324×150"; Gate = "exact-sha"; Sha = $sha02 }
    )
}

# claimed-different frames with same hash fail
Expect-Exit "01 equals 02 fails distinctness" 1 {
    param($root)
    $client = Join-Path $root "artifacts/phase-08-gameplay-client"
    Write-Png (Join-Path $client "01-phase8-tab.png") 352 480 7 7 7
    Write-Png (Join-Path $client "02-dialogue-choices.png") 352 480 7 7 7
    Write-Manifest $root @(
        @{ Section = "client"; File = "01-phase8-tab.png"; Desc = "tab"; Dims = "300–400×250–700"; Gate = "present-dims"; Sha = "—" }
        @{ Section = "client"; File = "02-dialogue-choices.png"; Desc = "dlg"; Dims = "300–400×250–700"; Gate = "present-dims"; Sha = "—" }
    )
}

# editor present-dims exact size mismatch
Expect-Exit "editor wrong dimensions" 1 {
    param($root)
    $editor = Join-Path $root "artifacts/phase-08-editor"
    Write-Png (Join-Path $editor "01-phase8-content-browse.png") 996 679 8 8 8
    Write-Png (Join-Path $editor "02-dialogue-structured-edit.png") 800 600 9 9 9
    $shaEd = Get-Sha (Join-Path $editor "01-phase8-content-browse.png")
    Write-Manifest $root @(
        @{ Section = "editor"; File = "01-phase8-content-browse.png"; Desc = "browse"; Dims = "996×679"; Gate = "exact-sha"; Sha = $shaEd }
        @{ Section = "editor"; File = "02-dialogue-structured-edit.png"; Desc = "edit"; Dims = "996×679"; Gate = "present-dims"; Sha = "—" }
    )
}

if ($failed -gt 0) {
    Write-Error "$failed fixture test(s) failed."
    exit 1
}
Write-Host "All Phase 8 screenshot manifest fixture tests passed."
exit 0
