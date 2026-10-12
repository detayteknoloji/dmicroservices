param([switch]$NoBuild)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'DMicroservices.Redis.Compatibility/Run-RedisCompatibility.ps1') -NoBuild:$NoBuild
if ($LASTEXITCODE -ne 0) { throw 'Redis compatibility runner failed.' }
& (Join-Path $PSScriptRoot 'DMicroservices.Elastic.Smoke/Run-ElasticCompatibility.ps1') -NoBuild:$NoBuild
if ($LASTEXITCODE -ne 0) { throw 'Elasticsearch compatibility runner failed.' }
