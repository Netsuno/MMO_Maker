# P10-6 Windows proof: publish self-contained client+editor, copy zips *outside*
# the git tree, strip SDK from PATH, launch Frog.Client.exe / Frog.Editor.exe
# with --smoke-launch, require exit code 0.
[CmdletBinding()]
param(
    [string] $PublishRoot = "",
    [string] $OutsideRoot = "",
    [switch] $SkipPublish,
    [int] $TimeoutSeconds = 45
)

$ErrorActionPreference = "Stop"

function Get-RepoRoot {
    return (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

$root = Get-RepoRoot
$stamp = Get-Date -Format "yyyyMMddTHHmmss"
if ([string]::IsNullOrWhiteSpace($PublishRoot)) {
    $PublishRoot = Join-Path $env:TEMP "frog-p10-6-publish-$stamp-$PID"
}
if ([string]::IsNullOrWhiteSpace($OutsideRoot)) {
    $OutsideRoot = Join-Path $env:TEMP "frog-p10-6-outside-$stamp-$PID"
}

$repoFull = [IO.Path]::GetFullPath($root)
$pubFull = [IO.Path]::GetFullPath($PublishRoot)
$outFull = [IO.Path]::GetFullPath($OutsideRoot)
if ($pubFull.StartsWith($repoFull, [StringComparison]::OrdinalIgnoreCase)) {
    throw "PublishRoot must be outside the git tree, got $pubFull"
}
if ($outFull.StartsWith($repoFull, [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutsideRoot must be outside the git tree, got $outFull"
}

New-Item -ItemType Directory -Force -Path $PublishRoot, $OutsideRoot | Out-Null

if (-not $SkipPublish) {
    Write-Host "==> publish client-win-x64 + editor-win-x64 -> $PublishRoot"
    & (Join-Path $root "scripts/publish-frog.ps1") `
        -Target client-win-x64, editor-win-x64 `
        -OutputRoot $PublishRoot `
        -Force
    if ($LASTEXITCODE -ne 0) {
        throw "publish-frog.ps1 failed with exit $LASTEXITCODE"
    }
}

function Copy-AndExtract([string] $Name) {
    $zip = Join-Path $PublishRoot "archives/$Name.zip"
    if (-not (Test-Path $zip)) {
        throw "missing archive $zip"
    }
    Copy-Item $zip (Join-Path $OutsideRoot "$Name.zip")
    $dest = Join-Path $OutsideRoot $Name
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    Expand-Archive -LiteralPath $zip -DestinationPath $dest -Force
    return (Join-Path $dest $Name)
}

$clientDir = Copy-AndExtract "client-win-x64"
$editorDir = Copy-AndExtract "editor-win-x64"

foreach ($pair in @(
        @{ Dir = $clientDir; Exe = "Frog.Client.exe"; Extra = @("Frog.Client.dll", "hostfxr.dll") },
        @{ Dir = $editorDir; Exe = "Frog.Editor.exe"; Extra = @("Frog.Editor.dll", "hostfxr.dll", "Frog.Persistence.PostgreSql.dll") }
    )) {
    $exe = Join-Path $pair.Dir $pair.Exe
    if (-not (Test-Path $exe)) {
        throw "missing $($pair.Exe) in $($pair.Dir)"
    }
    foreach ($extra in $pair.Extra) {
        if (-not (Test-Path (Join-Path $pair.Dir $extra))) {
            throw "missing $extra in $($pair.Dir)"
        }
    }
}

function Get-Sha256([string] $Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

$clientExe = Join-Path $clientDir "Frog.Client.exe"
$editorExe = Join-Path $editorDir "Frog.Editor.exe"
$clientSha = Get-Sha256 $clientExe
$editorSha = Get-Sha256 $editorExe
Write-Host "client.exe=$clientExe sha256=$clientSha"
Write-Host "editor.exe=$editorExe sha256=$editorSha"

function Get-PathWithoutDotnet {
    $parts = @()
    foreach ($dir in ($env:PATH -split ';')) {
        if ([string]::IsNullOrWhiteSpace($dir)) { continue }
        $dotnet = Join-Path $dir "dotnet.exe"
        if (Test-Path $dotnet) { continue }
        $parts += $dir
    }
    return ($parts -join ';')
}

function Invoke-SmokeLaunch([string] $ExePath, [string] $Label) {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $ExePath
    $psi.Arguments = "--smoke-launch"
    $psi.WorkingDirectory = [IO.Path]::GetDirectoryName($ExePath)
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true
    $psi.Environment["PATH"] = Get-PathWithoutDotnet
    $psi.Environment.Remove("DOTNET_ROOT") | Out-Null
    $proc = New-Object System.Diagnostics.Process
    $proc.StartInfo = $psi
    $null = $proc.Start()
    if (-not $proc.WaitForExit($TimeoutSeconds * 1000)) {
        try { $proc.Kill($true) } catch { }
        throw "$Label did not exit within ${TimeoutSeconds}s: $ExePath"
    }
    if ($proc.ExitCode -ne 0) {
        $stdout = $proc.StandardOutput.ReadToEnd()
        $stderr = $proc.StandardError.ReadToEnd()
        throw "$Label exited $($proc.ExitCode)`n$stdout`n$stderr"
    }
    Write-Host "OK $Label exit 0 (PATH without SDK)"
}

Invoke-SmokeLaunch $clientExe "Frog.Client.exe"
Invoke-SmokeLaunch $editorExe "Frog.Editor.exe"

$proof = Join-Path $OutsideRoot "LAUNCH_PROOF.txt"
@(
    "P10-6 Windows packaged EXE launch proof"
    "outsideRoot=$OutsideRoot"
    "client.exe=$clientExe"
    "client.exe.sha256=$clientSha"
    "editor.exe=$editorExe"
    "editor.exe.sha256=$editorSha"
    "smoke-launch=exit 0 (SDK stripped from PATH)"
    "PROUVE: process start from zip extracted outside the git tree"
) | Set-Content -LiteralPath $proof -Encoding utf8
Write-Host "OK launch proof written to $proof"
Get-Content -LiteralPath $proof
