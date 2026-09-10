param(
    [Parameter(Mandatory = $true)]
    [string[]]$LogPaths
)

$ErrorActionPreference = 'Stop'

$patterns = @(
    'Unhandled exception',
    'Client handler task faulted unexpectedly',
    'BackgroundService failed',
    'MSB3026',
    'MSB3027',
    'System\.NullReferenceException',
    'System\.ObjectDisposedException'
)

$violations = @()
foreach ($path in $LogPaths) {
    if (-not (Test-Path -LiteralPath $path)) {
        $violations += "Missing log file: $path"
        continue
    }

    $content = Get-Content -LiteralPath $path -Raw
    foreach ($pattern in $patterns) {
        if ($content -match $pattern) {
            $violations += "${path}: matched '$pattern'"
        }
    }
}

if ($violations.Count -gt 0) {
    Write-Error ("Lifecycle log guard failed:{0}{1}" -f [Environment]::NewLine, ($violations -join [Environment]::NewLine))
    exit 1
}

Write-Host "Lifecycle log guard OK ($($LogPaths.Count) file(s))"
