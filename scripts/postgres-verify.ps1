# SQL schema/history checks. Mirrors scripts/postgres-verify.sh.
[CmdletBinding()]
param(
    [string] $Connection = $(if ($env:FROG_POSTGRES_CONNECTION_STRING) { $env:FROG_POSTGRES_CONNECTION_STRING } elseif ($env:FROG_POSTGRES_TEST_CONNECTION_STRING) { $env:FROG_POSTGRES_TEST_CONNECTION_STRING } else { "" }),
    [string] $ServerHost = "",
    [int] $Port = 0,
    [string] $User = "",
    [string] $Password = "",
    [string] $Database = ""
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

$psql = Get-Command psql -ErrorAction SilentlyContinue
if (-not $psql) { throw "psql not on PATH." }

$parsed = Parse-FrogNpgsqlConnection $Connection
if ($ServerHost) { $parsed.Host = $ServerHost }
if ($Port -gt 0) { $parsed.Port = $Port }
if ($User) { $parsed.User = $User }
if ($Password) { $parsed.Password = $Password }
if ($Database) { $parsed.Database = $Database }
if (-not $parsed.Host -or -not $parsed.User -or -not $parsed.Database) {
    throw "host, user, and database are required."
}

$env:PGPASSWORD = $parsed.Password
function Invoke-Sql([string] $Sql) {
    $out = & $psql.Source --no-psqlrc -v ON_ERROR_STOP=1 -X -tAc $Sql --host $parsed.Host --port $parsed.Port --username $parsed.User -d $parsed.Database
    if ($LASTEXITCODE -ne 0) { throw "psql failed with exit $LASTEXITCODE" }
    return "$out".Trim()
}

Write-Host "verify $($parsed.Database)@$($parsed.Host):$($parsed.Port)"
$names = Invoke-Sql "SELECT string_agg(nspname, ',' ORDER BY nspname) FROM pg_namespace WHERE nspname IN ('auth','content','ops','player','world');"
if ($names -ne "auth,content,ops,player,world") {
    throw "expected schemas auth,content,ops,player,world; got: $names"
}
$hist = Invoke-Sql @"
SELECT quote_ident(n.nspname) || '.' || quote_ident(c.relname)
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relname = '__EFMigrationsHistory'
ORDER BY CASE n.nspname WHEN 'public' THEN 0 WHEN 'world' THEN 1 ELSE 2 END
LIMIT 1;
"@
if ([string]::IsNullOrWhiteSpace($hist)) {
    throw "__EFMigrationsHistory not found"
}
$count = Invoke-Sql "SELECT COUNT(*) FROM $hist;"
if ([int]$count -lt 1) {
    throw "EF history $hist has no rows"
}
Write-Host "verify ok: schemas=$names history=$hist rows=$count"
Write-Host "next: PostgresDatabaseHealth must report 0 pending (server start or integration test)."
