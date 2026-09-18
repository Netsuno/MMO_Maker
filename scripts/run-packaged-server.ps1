# Start or stop a published Frog.Server layout. Mirrors scripts/run-packaged-server.sh.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet("start", "stop")]
    [string] $Action,

    [Parameter(Mandatory = $true)]
    [string] $Dir,

    [switch] $Foreground,
    [int] $Port = 0,
    [string] $Bind = "",
    [int] $TimeoutSeconds = 20
)

$ErrorActionPreference = "Stop"
$Dir = (Resolve-Path $Dir).Path
$pidFile = Join-Path $Dir "frog-server.pid"
$logFile = Join-Path $Dir "frog-server.log"
$shutdownFile = $(if ($env:FROG_SHUTDOWN_FILE) { $env:FROG_SHUTDOWN_FILE } else { Join-Path $Dir ".frog-shutdown-request" })

function Get-ServerStartInfo {
    $exe = Join-Path $Dir "Frog.Server.exe"
    $unix = Join-Path $Dir "Frog.Server"
    $dll = Join-Path $Dir "Frog.Server.dll"
    $info = @{ FileName = ""; Arguments = @() }
    if (Test-Path $exe) {
        $info.FileName = $exe
    } elseif (Test-Path $unix) {
        $info.FileName = $unix
    } elseif (Test-Path $dll) {
        if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
            throw "dotnet not on PATH (framework-dependent publish)"
        }
        $info.FileName = "dotnet"
        $info.Arguments += $dll
    } else {
        throw "no Frog.Server host in $Dir"
    }
    $info.Arguments += @("--contentRoot", $Dir)
    if ($Port -gt 0) { $info.Arguments += "--Server:Port=$Port" }
    if (-not [string]::IsNullOrWhiteSpace($Bind)) { $info.Arguments += "--Server:BindAddress=$Bind" }
    return $info
}

function Assert-Overlay {
    if (-not (Test-Path (Join-Path $Dir "appsettings.json"))) {
        throw "appsettings.json missing in $Dir (wrong directory?)"
    }
    if (-not (Test-Path (Join-Path $Dir "appsettings.Local.json")) -and
        [string]::IsNullOrWhiteSpace($env:FROG_POSTGRES_CONNECTION_STRING)) {
        throw "copy appsettings.Local.json.example to appsettings.Local.json (or set FROG_POSTGRES_CONNECTION_STRING)"
    }
}

if ($Action -eq "start") {
    Assert-Overlay
    if (-not $Foreground -and (Test-Path $pidFile)) {
        $existing = Get-Content $pidFile | Select-Object -First 1
        $proc = Get-Process -Id $existing -ErrorAction SilentlyContinue
        if ($proc) {
            throw "already running (pid $existing); stop first"
        }
    }
    if (Test-Path $shutdownFile) { Remove-Item -Force $shutdownFile }
    $info = Get-ServerStartInfo
    $env:FROG_SHUTDOWN_FILE = $shutdownFile
    if ($Foreground) {
        Set-Location $Dir
        & $info.FileName @($info.Arguments)
        exit $LASTEXITCODE
    }
    $stdout = New-Object System.IO.StreamWriter($logFile, $true)
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $info.FileName
    $psi.Arguments = ($info.Arguments -join " ")
    $psi.WorkingDirectory = $Dir
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true
    $proc = New-Object System.Diagnostics.Process
    $proc.StartInfo = $psi
    $handler = {
        if (-not [string]::IsNullOrEmpty($EventArgs.Data)) {
            Add-Content -Path $logFile -Value $EventArgs.Data
        }
    }
    Register-ObjectEvent -InputObject $proc -EventName OutputDataReceived -Action $handler | Out-Null
    Register-ObjectEvent -InputObject $proc -EventName ErrorDataReceived -Action $handler | Out-Null
    [void]$stdout.Dispose()
    [void]$proc.Start()
    $proc.BeginOutputReadLine()
    $proc.BeginErrorReadLine()
    Set-Content -Path $pidFile -Value $proc.Id
    Write-Host "started pid $($proc.Id) log $logFile"
    return
}

if (Test-Path $pidFile) {
    $pidValue = Get-Content $pidFile | Select-Object -First 1
} else {
    $pidValue = $null
}
New-Item -ItemType File -Force -Path $shutdownFile | Out-Null
if ($pidValue) {
    $proc = Get-Process -Id $pidValue -ErrorAction SilentlyContinue
    if ($proc) {
        try { $proc.CloseMainWindow() | Out-Null } catch { }
        # Same sentinel path as Frog.Server ShutdownFileWatcherService.
    }
}
$deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
while ($pidValue -and (Get-Process -Id $pidValue -ErrorAction SilentlyContinue)) {
    if ([DateTime]::UtcNow -gt $deadline) {
        throw "process $pidValue still running after ${TimeoutSeconds}s (wrote $shutdownFile)"
    }
    Start-Sleep -Seconds 1
}
if (Test-Path $pidFile) { Remove-Item -Force $pidFile }
Write-Host "stopped"
