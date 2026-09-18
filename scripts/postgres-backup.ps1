# Frog PostgreSQL backup (custom format). Mirrors scripts/postgres-backup.sh.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Output,

    [string] $Connection = $(if ($env:FROG_POSTGRES_CONNECTION_STRING) { $env:FROG_POSTGRES_CONNECTION_STRING } elseif ($env:FROG_POSTGRES_TEST_CONNECTION_STRING) { $env:FROG_POSTGRES_TEST_CONNECTION_STRING } else { "" }),

    [string] $ServerHost = "",
    [int] $Port = 0,
    [string] $User = "",
    [string] $Password = "",
    [string] $Database = "",
    [switch] $Force,
    [switch] $VerboseDump
)

$ErrorActionPreference = "Stop"

function Parse-FrogNpgsqlConnection([string] $ConnectionString) {
    $result = @{
        Host = $(if ($env:PGHOST) { $env:PGHOST } else { "127.0.0.1" })
        Port = $(if ($env:PGPORT) { [int]$env:PGPORT } else { 5432 })
        User = $(if ($env:PGUSER) { $env:PGUSER } else { "" })
        Password = $(if ($env:PGPASSWORD) { $env:PGPASSWORD } else { "" })
        Database = $(if ($env:PGDATABASE) { $env:PGDATABASE } else { "" })
    }
    if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
        return $result
    }
    $parts = if ($ConnectionString -like "*;*") { $ConnectionString.Split(";") } else { $ConnectionString.Split(" ", [System.StringSplitOptions]::RemoveEmptyEntries) }
    foreach ($part in $parts) {
        $trim = $part.Trim()
        if ($trim -notlike "*=*") { continue }
        $key = $trim.Substring(0, $trim.IndexOf("=")).Trim().ToLowerInvariant() -replace "\s", ""
        $value = $trim.Substring($trim.IndexOf("=") + 1)
        switch ($key) {
            { $_ -in @("host", "server") } { $result.Host = $value }
            "port" { $result.Port = [int]$value }
            { $_ -in @("database", "db", "dbname") } { $result.Database = $value }
            { $_ -in @("username", "user", "userid", "uid") } { $result.User = $value }
            { $_ -in @("password", "pwd") } { $result.Password = $value }
        }
    }
    return $result
}

$pgDump = Get-Command pg_dump -ErrorAction SilentlyContinue
if (-not $pgDump) {
    throw "pg_dump not on PATH. Install PostgreSQL 16 client tools."
}

$parsed = Parse-FrogNpgsqlConnection $Connection
if ($ServerHost) { $parsed.Host = $ServerHost }
if ($Port -gt 0) { $parsed.Port = $Port }
if ($User) { $parsed.User = $User }
if ($Password) { $parsed.Password = $Password }
if ($Database) { $parsed.Database = $Database }

if (-not $parsed.Host -or -not $parsed.User -or -not $parsed.Database) {
    throw "host, user, and database are required (--Connection or flags)."
}

if ((Test-Path -LiteralPath $Output) -and -not $Force) {
    throw "output exists (pass -Force to overwrite): $Output"
}

$dir = Split-Path -Parent $Output
if ($dir) {
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
}

$env:PGPASSWORD = $parsed.Password
$dumpArgs = @(
    "--format=custom",
    "--compress=9",
    "--no-owner",
    "--no-acl",
    "--file=$Output",
    "--schema=auth",
    "--schema=content",
    "--schema=ops",
    "--schema=player",
    "--schema=world",
    "--schema=public",
    "--host=$($parsed.Host)",
    "--port=$($parsed.Port)",
    "--username=$($parsed.User)",
    $parsed.Database
)
if ($VerboseDump) { $dumpArgs = @("--verbose") + $dumpArgs }

Write-Host "dumping $($parsed.Database)@$($parsed.Host):$($parsed.Port) -> $Output"
& $pgDump.Source @dumpArgs
if ($LASTEXITCODE -ne 0) {
    throw "pg_dump failed with exit $LASTEXITCODE"
}
Write-Host "backup ok: $Output"
