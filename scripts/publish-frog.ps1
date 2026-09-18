# Publish Frog.Server (win-x64 / linux-x64) and WinForms Frog.Client / Frog.Editor (win-x64).
# Mirrors scripts/publish-frog.sh. Self-contained, not single-file.
[CmdletBinding()]
param(
    [string[]] $Target = @(),
    [string] $OutputRoot = "",
    [string] $Configuration = "Release",
    [switch] $Force,
    [switch] $List
)

$ErrorActionPreference = "Stop"

function Show-Usage {
    @"
Usage: publish-frog.ps1 -Target NAME [-Target NAME ...]

Repeatable dotnet publish layouts for Phase 10 packaging (P10-6 self-contained).

Targets:
  server-linux-x64   Frog.Server, RID linux-x64 (layout only on Windows)
  server-win-x64     Frog.Server, RID win-x64
  client-win-x64     Frog.Client, RID win-x64
  editor-win-x64     Frog.Editor, RID win-x64
  all                all four layouts

Options:
  -OutputRoot DIR        Default: <repo>/artifacts/publish
  -Configuration NAME    Default: Release
  -Force                 Overwrite an existing target directory
  -List                  Print targets and exit
"@
}

if ($List) {
    @("server-linux-x64", "server-win-x64", "client-win-x64", "editor-win-x64", "all") | ForEach-Object { $_ }
    return
}

if (-not $Target -or $Target.Count -eq 0) {
    throw "pass -Target (see scripts/publish-frog.sh --help)"
}

$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $root "artifacts/publish"
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet not on PATH (install SDK 8.0.424)"
}

function Expand-Targets([string[]] $names) {
    $resolved = New-Object System.Collections.Generic.List[string]
    foreach ($t in $names) {
        switch ($t) {
            "all" {
                $resolved.Add("server-linux-x64")
                $resolved.Add("server-win-x64")
                $resolved.Add("client-win-x64")
                $resolved.Add("editor-win-x64")
            }
            { $_ -in @("server-linux-x64", "server-win-x64", "client-win-x64", "editor-win-x64") } {
                $resolved.Add($t)
            }
            default { throw "unknown -Target: $t" }
        }
    }
    return $resolved | Select-Object -Unique
}

function Get-ProtocolVersion {
    $file = Join-Path $root "Frog.Core/Constants/FrogWireProtocol.cs"
    $line = Select-String -Path $file -Pattern 'public const ushort Version = ([0-9]+);' | Select-Object -First 1
    if ($line -and $line.Matches.Count -gt 0) {
        return [int]$line.Matches[0].Groups[1].Value
    }
    return 10
}

function Get-GitTip {
    try {
        return (git -C $root rev-parse HEAD 2>$null)
    } catch {
        return "unknown"
    }
}

function Write-Manifest([string] $dest, [string] $name, [string] $project, [string] $rid, [string] $tfm) {
    $manifest = [ordered]@{
        product = "Frog"
        layout = $name
        project = $project
        targetFramework = $tfm
        runtimeIdentifier = $rid
        configuration = $Configuration
        selfContained = $true
        singleFile = $false
        sdk = (dotnet --version)
        protocolVersion = (Get-ProtocolVersion)
        gitSha = (Get-GitTip)
        publishedUtc = [DateTime]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
    }
    $json = $manifest | ConvertTo-Json -Depth 4
    Set-Content -Path (Join-Path $dest "packaging-manifest.json") -Value $json -Encoding utf8
}

function Assert-Files([string] $dest, [string[]] $files) {
    foreach ($f in $files) {
        $path = Join-Path $dest $f
        if (-not (Test-Path $path)) {
            throw "missing $f in $dest"
        }
    }
}

function Assert-NoLocalOverlay([string] $dest) {
    if (Test-Path (Join-Path $dest "appsettings.Local.json")) {
        throw "publish output must not contain appsettings.Local.json (secrets). Copy the overlay after publish."
    }
}

function Assert-ServerLayout([string] $dest, [string] $rid) {
    $files = @(
        "Frog.Server.dll",
        "Frog.Server.runtimeconfig.json",
        "Frog.Server.deps.json",
        "Frog.Persistence.PostgreSql.dll",
        "Npgsql.dll",
        "Npgsql.EntityFrameworkCore.PostgreSQL.dll",
        "Microsoft.EntityFrameworkCore.dll",
        "Microsoft.EntityFrameworkCore.Relational.dll",
        "EFCore.NamingConventions.dll",
        "appsettings.json",
        "appsettings.Local.json.example",
        "packaging-manifest.json"
    )
    if ($rid -eq "win-x64") { $files += "Frog.Server.exe"; $files += "hostfxr.dll" } else { $files += "Frog.Server"; $files += "libhostfxr.so" }
    Assert-Files $dest $files
    Assert-NoLocalOverlay $dest
    $deps = Get-Content -Raw (Join-Path $dest "Frog.Server.deps.json")
    if ($deps -notmatch "Npgsql.EntityFrameworkCore.PostgreSQL") {
        throw "Frog.Server.deps.json does not reference Npgsql.EntityFrameworkCore.PostgreSQL"
    }
}

function Assert-ClientLayout([string] $dest) {
    Assert-Files $dest @(
        "Frog.Client.dll",
        "Frog.Client.exe",
        "Frog.Core.dll",
        "Frog.Application.dll",
        "hostfxr.dll",
        "packaging-manifest.json"
    )
}

function Assert-EditorLayout([string] $dest) {
    Assert-Files $dest @(
        "Frog.Editor.dll",
        "Frog.Editor.exe",
        "Frog.Persistence.PostgreSql.dll",
        "Frog.Core.dll",
        "hostfxr.dll",
        "appsettings.Local.json.example",
        "packaging-manifest.json"
    )
}

function Copy-DemoDocs([string] $dest) {
    $demoDir = Join-Path $dest "demo-world"
    New-Item -ItemType Directory -Force -Path $demoDir | Out-Null
    Copy-Item (Join-Path $root "docs/progress/phase-10-beta-release/demo-world/LICENSES.md") (Join-Path $demoDir "LICENSES.md")
    Copy-Item (Join-Path $root "docs/progress/phase-10-beta-release/DEMO_WORLD.md") (Join-Path $dest "DEMO_WORLD.md")
}

function Write-Archive([string] $dest, [string] $name) {
    $archives = Join-Path $OutputRoot "archives"
    New-Item -ItemType Directory -Force -Path $archives | Out-Null
    $zipPath = Join-Path $archives "$name.zip"
    if (Test-Path $zipPath) {
        Remove-Item -Force $zipPath
    }
    Compress-Archive -Path $dest -DestinationPath $zipPath -Force
    $hash = (Get-FileHash -Algorithm SHA256 $zipPath).Hash.ToLowerInvariant()
    $sums = Join-Path $archives "SHA256SUMS"
    $lines = @()
    if (Test-Path $sums) {
        $lines = Get-Content $sums | Where-Object { $_ -and -not $_.EndsWith("  $name.zip") }
    }
    $lines += "$hash  $name.zip"
    Set-Content -Path $sums -Value $lines -Encoding utf8
    $manifestPath = Join-Path $dest "packaging-manifest.json"
    $data = Get-Content -Raw $manifestPath | ConvertFrom-Json
    $data | Add-Member -NotePropertyName archive -NotePropertyValue "$name.zip" -Force
    $data | Add-Member -NotePropertyName archiveSha256 -NotePropertyValue $hash -Force
    ($data | ConvertTo-Json -Depth 6) | Set-Content -Path $manifestPath -Encoding utf8
    Write-Host "archive $zipPath sha256=$hash"
}

function Publish-One([string] $name, [string] $project, [string] $rid, [string] $tfm) {
    $dest = Join-Path $OutputRoot $name
    $projectPath = Join-Path $root $project
    if (-not (Test-Path $projectPath)) {
        throw "project not found: $projectPath"
    }
    if ((Test-Path $dest) -and -not $Force) {
        $existing = Get-ChildItem -Force $dest -ErrorAction SilentlyContinue
        if ($existing) {
            throw "output exists (pass -Force to overwrite): $dest"
        }
    }
    if (Test-Path $dest) {
        Remove-Item -Recurse -Force $dest
    }
    New-Item -ItemType Directory -Force -Path $dest | Out-Null

    Write-Host "==> publishing $name ($project, $rid, $Configuration)"
    & dotnet publish $projectPath `
        -c $Configuration `
        -r $rid `
        --self-contained true `
        -o $dest `
        -p:PublishSingleFile=false `
        -p:DebugType=None `
        -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for $name (exit $LASTEXITCODE)"
    }

    Write-Manifest $dest $name $project $rid $tfm
    Copy-DemoDocs $dest
    switch ($name) {
        { $_ -in @("server-linux-x64", "server-win-x64") } { Assert-ServerLayout $dest $rid }
        "client-win-x64" { Assert-ClientLayout $dest }
        "editor-win-x64" { Assert-EditorLayout $dest }
    }
    Write-Archive $dest $name
    Write-Host "OK $dest"
}

$resolved = Expand-Targets $Target
foreach ($name in $resolved) {
    switch ($name) {
        "server-linux-x64" { Publish-One $name "Frog.Server/Frog.Server.csproj" "linux-x64" "net8.0" }
        "server-win-x64" { Publish-One $name "Frog.Server/Frog.Server.csproj" "win-x64" "net8.0" }
        "client-win-x64" { Publish-One $name "Frog.Client/Frog.Client.csproj" "win-x64" "net8.0-windows" }
        "editor-win-x64" { Publish-One $name "Frog.Editor/Frog.Editor.csproj" "win-x64" "net8.0-windows" }
    }
}
