# Restore a Frog custom-format dump. Mirrors scripts/postgres-restore.sh.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $InputPath,

    [string] $Connection = $(if ($env:FROG_POSTGRES_CONNECTION_STRING) { $env:FROG_POSTGRES_CONNECTION_STRING } elseif ($env:FROG_POSTGRES_TEST_CONNECTION_STRING) { $env:FROG_POSTGRES_TEST_CONNECTION_STRING } else { "" }),

    [string] $ServerHost = "",
    [int] $Port = 0,
    [string] $User = "",
    [string] $Password = "",
    [string] $Database = "",
    [string] $MaintenanceDatabase = "postgres",
    [switch] $CreateDatabase,
    [switch] $Recreate,
    [switch] $SkipVerify,
    [switch] $VerboseRestore
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

function Invoke-FrogPsql([hashtable] $Parsed, [string] $DatabaseName, [string] $Sql) {
    $psql = Get-Command psql -ErrorAction SilentlyContinue
    if (-not $psql) { throw "psql not on PATH." }
    $env:PGPASSWORD = $Parsed.Password
    $out = & $psql.Source --no-psqlrc -v ON_ERROR_STOP=1 -X -tAc $Sql --host $Parsed.Host --port $Parsed.Port --username $Parsed.User -d $DatabaseName
    if ($LASTEXITCODE -ne 0) {
        throw "psql failed with exit $LASTEXITCODE"
    }
    return "$out".Trim()
}

if (-not (Test-Path -LiteralPath $InputPath)) {
    throw "input dump not found: $InputPath"
}

$pgRestore = Get-Command pg_restore -ErrorAction SilentlyContinue
if (-not $pgRestore) {
    throw "pg_restore not on PATH. Install PostgreSQL 16 client tools."
}

$parsed = Parse-FrogNpgsqlConnection $Connection
if ($ServerHost) { $parsed.Host = $ServerHost }
if ($Port -gt 0) { $parsed.Port = $Port }
if ($User) { $parsed.User = $User }
if ($Password) { $parsed.Password = $Password }
if ($Database) { $parsed.Database = $Database }

if (-not $parsed.Host -or -not $parsed.User -or -not $parsed.Database) {
    throw "host, user, and database are required."
}
if ($parsed.Database -notmatch '^[A-Za-z_][A-Za-z0-9_]*$') {
    throw "refusing unsafe database name: $($parsed.Database)"
}

$target = $parsed.Database
if ($Recreate) {
    if ($target -eq $MaintenanceDatabase) {
        throw "refusing to DROP the maintenance database ($target)"
    }
    Write-Host "recreate: dropping $target"
    Invoke-FrogPsql $parsed $MaintenanceDatabase "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '$target' AND pid <> pg_backend_pid();" | Out-Null
    Invoke-FrogPsql $parsed $MaintenanceDatabase "DROP DATABASE IF EXISTS $target;" | Out-Null
    $CreateDatabase = $true
}

$exists = Invoke-FrogPsql $parsed $MaintenanceDatabase "SELECT 1 FROM pg_database WHERE datname = '$target'"
if (-not $exists) {
    if (-not $CreateDatabase) {
        throw "target database $target does not exist (pass -CreateDatabase or -Recreate)"
    }
    Write-Host "creating database $target"
    try {
        Invoke-FrogPsql $parsed $MaintenanceDatabase "CREATE DATABASE $target OWNER $($parsed.User);" | Out-Null
    } catch {
        Invoke-FrogPsql $parsed $MaintenanceDatabase "CREATE DATABASE $target;" | Out-Null
    }
}

$schemaCount = Invoke-FrogPsql $parsed $target "SELECT COUNT(*) FROM pg_namespace WHERE nspname IN ('auth','content','ops','player','world');"
if ($schemaCount -ne "0") {
    throw "target $target already has $schemaCount Frog schema(s). Restore onto an empty database, or pass -Recreate."
}

$env:PGPASSWORD = $parsed.Password
$restoreArgs = @(
    "--no-owner",
    "--no-acl",
    "--exit-on-error",
    "--single-transaction",
    "--dbname=$target",
    "--host=$($parsed.Host)",
    "--port=$($parsed.Port)",
    "--username=$($parsed.User)",
    $InputPath
)
if ($VerboseRestore) { $restoreArgs = @("--verbose") + $restoreArgs }

Write-Host "restoring $InputPath -> $target@$($parsed.Host):$($parsed.Port)"
& $pgRestore.Source @restoreArgs
if ($LASTEXITCODE -ne 0) {
    throw "pg_restore failed with exit $LASTEXITCODE"
}

if (-not $SkipVerify) {
    $verify = Join-Path $PSScriptRoot "postgres-verify.ps1"
    & $verify -Connection $Connection -ServerHost $parsed.Host -Port $parsed.Port -User $parsed.User -Password $parsed.Password -Database $target
}
Write-Host "restore ok: $target"
