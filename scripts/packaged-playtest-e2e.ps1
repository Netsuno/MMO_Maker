# P10-3 Windows: extract published zips *outside* the git tree as sibling
# layouts (editor / client / server), resolve the same paths the editor uses,
# start the packaged server, TCP Hello. Client --smoke-launch is P10-6.
# This is not a 2-PC WAN playtest and not a 30-60 min human session.
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
    $PublishRoot = Join-Path $env:TEMP "frog-p10-3-publish-$stamp-$PID"
}
if ([string]::IsNullOrWhiteSpace($OutsideRoot)) {
    $OutsideRoot = Join-Path $env:TEMP "frog-p10-3-outside-$stamp-$PID"
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
    Write-Host "==> publish client+editor+server-win-x64 -> $PublishRoot"
    & (Join-Path $root "scripts/publish-frog.ps1") `
        -Target client-win-x64, editor-win-x64, server-win-x64 `
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
    $nested = Join-Path $dest $Name
    if (Test-Path $nested) { return $nested }
    return $dest
}

$clientDir = Copy-AndExtract "client-win-x64"
$editorDir = Copy-AndExtract "editor-win-x64"
$serverDir = Copy-AndExtract "server-win-x64"

$resolvedClient = [IO.Path]::GetFullPath((Join-Path $editorDir "..\client-win-x64\Frog.Client.exe"))
$resolvedServer = [IO.Path]::GetFullPath((Join-Path $editorDir "..\server-win-x64\Frog.Server.exe"))
# Sibling layout after extract: outside/client-win-x64/client-win-x64 and
# outside/editor-win-x64/editor-win-x64 — flatten to true siblings for playtest.
$flat = Join-Path $OutsideRoot "siblings"
$flatEditor = Join-Path $flat "editor-win-x64"
$flatClient = Join-Path $flat "client-win-x64"
$flatServer = Join-Path $flat "server-win-x64"
New-Item -ItemType Directory -Force -Path $flatEditor, $flatClient, $flatServer | Out-Null
Copy-Item -Recurse -Force (Join-Path $clientDir "*") $flatClient
Copy-Item -Recurse -Force (Join-Path $editorDir "*") $flatEditor
Copy-Item -Recurse -Force (Join-Path $serverDir "*") $flatServer

$clientExe = Join-Path $flatClient "Frog.Client.exe"
$editorExe = Join-Path $flatEditor "Frog.Editor.exe"
$serverExe = Join-Path $flatServer "Frog.Server.exe"
foreach ($p in @($clientExe, $editorExe, $serverExe)) {
    if (-not (Test-Path $p)) { throw "missing $p" }
}

$fromEditorClient = [IO.Path]::GetFullPath((Join-Path $flatEditor "..\client-win-x64\Frog.Client.exe"))
$fromEditorServer = [IO.Path]::GetFullPath((Join-Path $flatEditor "..\server-win-x64\Frog.Server.exe"))
if ($fromEditorClient -ne [IO.Path]::GetFullPath($clientExe)) {
    throw "sibling client resolve mismatch"
}
if ($fromEditorServer -ne [IO.Path]::GetFullPath($serverExe)) {
    throw "sibling server resolve mismatch"
}

$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
$listener.Stop()

$localCfg = @{
    Server     = @{ Port = $port; BindAddress = "127.0.0.1" }
    MariaDb    = @{ Enabled = $false }
    PostgreSql = @{ Enabled = $false; AllowInMemoryFallback = $true }
} | ConvertTo-Json -Depth 5
Set-Content -LiteralPath (Join-Path $flatServer "appsettings.Local.json") -Value $localCfg -Encoding utf8

$shutdown = Join-Path $flatServer ".frog-shutdown-request"
$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = $serverExe
$psi.Arguments = "--contentRoot `"$flatServer`""
$psi.WorkingDirectory = $flatServer
$psi.UseShellExecute = $false
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.CreateNoWindow = $true
$psi.Environment["FROG_SHUTDOWN_FILE"] = $shutdown
$proc = New-Object System.Diagnostics.Process
$proc.StartInfo = $psi
$null = $proc.Start()

try {
    $ready = $false
    $deadline = [datetime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([datetime]::UtcNow -lt $deadline) {
        if ($proc.HasExited) {
            throw "packaged server exited $($proc.ExitCode)`n$($proc.StandardOutput.ReadToEnd())`n$($proc.StandardError.ReadToEnd())"
        }
        try {
            $tcp = New-Object System.Net.Sockets.TcpClient
            $tcp.Connect("127.0.0.1", $port)
            $stream = $tcp.GetStream()
            $lenBuf = New-Object byte[] 4
            $read = 0
            $stream.ReadTimeout = 8000
            while ($read -lt 4) {
                $n = $stream.Read($lenBuf, $read, 4 - $read)
                if ($n -le 0) { throw "eof header" }
                $read += $n
            }
            $len = [BitConverter]::ToInt32($lenBuf, 0)
            $body = New-Object byte[] $len
            $read = 0
            while ($read -lt $len) {
                $n = $stream.Read($body, $read, $len - $read)
                if ($n -le 0) { throw "eof body" }
                $read += $n
            }
            $tcp.Close()
            if ($body.Length -lt 1 -or $body[0] -ne 1) {
                throw "expected Hello opcode 1, got $($body[0])"
            }
            $ready = $true
            break
        }
        catch {
            Start-Sleep -Milliseconds 200
        }
    }
    if (-not $ready) {
        throw "packaged server from zip did not accept Hello on port $port"
    }
}
finally {
    New-Item -ItemType File -Force -Path $shutdown | Out-Null
    if (-not $proc.WaitForExit(15000)) {
        try { $proc.Kill($true) } catch { }
        throw "packaged server did not stop after shutdown file"
    }
}

$proof = Join-Path $OutsideRoot "PLAYTEST_PROOF.txt"
@(
    "P10-3 Windows packaged playtest from zip (sibling layouts)"
    "outsideRoot=$OutsideRoot"
    "editor.exe=$editorExe"
    "client.exe=$clientExe"
    "server.exe=$serverExe"
    "resolvedFromEditor.client=$fromEditorClient"
    "resolvedFromEditor.server=$fromEditorServer"
    "tcpHello=opcode 1 on 127.0.0.1:$port"
    "PROUVE: server process + Hello from zip extracted outside the git tree"
    "PAS une recette 2 PC / HUD / playtest menu WinForms 30-60 min"
) | Set-Content -LiteralPath $proof -Encoding utf8
Write-Host "OK playtest proof written to $proof"
Get-Content -LiteralPath $proof
