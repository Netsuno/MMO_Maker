<#
.SYNOPSIS
    Applique schema_frog_mariadb_v1.sql sur une base MariaDB / MySQL.

.EXAMPLE
    .\scripts\apply-frog-mariadb-schema.ps1 -ServerHost 192.168.1.76 -Port 4407 -Database mmo_test -User mmo_test -Password "secret"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $ServerHost,

    [int] $Port = 3306,

    [Parameter(Mandatory = $true)]
    [string] $Database,

    [Parameter(Mandatory = $true)]
    [string] $User,

    [Parameter(Mandatory = $true)]
    [string] $Password,

    [string] $SchemaPath = ""
)

$ErrorActionPreference = "Stop"

if (-not $SchemaPath) {
    $SchemaPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\Frog.Server\Database\schema_frog_mariadb_v1.sql"))
}

if (-not (Test-Path -LiteralPath $SchemaPath)) {
    throw "Fichier SQL introuvable: $SchemaPath"
}

$mysql = Get-Command mysql -ErrorAction SilentlyContinue
if (-not $mysql) {
    $mysql = Get-Command mariadb -ErrorAction SilentlyContinue
}

if (-not $mysql) {
    throw "Client 'mysql' ou 'mariadb' introuvable dans le PATH. Installe MariaDB client ou ajoute-le au PATH."
}

$sql = Get-Content -LiteralPath $SchemaPath -Raw -Encoding UTF8
$env:MYSQL_PWD = $Password
try {
    $sql | & $mysql.Source -h $ServerHost -P $Port -u $User --default-character-set=utf8mb4 $Database
    if ($LASTEXITCODE -ne 0) {
        throw "mysql/mariadb a retourné le code $LASTEXITCODE"
    }
}
finally {
    Remove-Item Env:\MYSQL_PWD -ErrorAction SilentlyContinue
}

Write-Host "Schéma appliqué sur ${Database}@${ServerHost}:${Port}."
