param([switch]$NoBuild)
$ErrorActionPreference = 'Stop'
$projectFile = Join-Path $PSScriptRoot 'DMicroservices.Redis.Compatibility.csproj'
$composeFile = Join-Path $PSScriptRoot 'Fixture/compose.yml'
$probeDirectory = Join-Path $PSScriptRoot 'bin/Release/net5.0'
if (-not $NoBuild) {
    & dotnet build $projectFile --no-restore -c Release -m:1 -nr:false -p:GeneratePackageOnBuild=false
    if ($LASTEXITCODE -ne 0) { throw 'Redis compatibility build failed.' }
}
if (-not (Test-Path -LiteralPath (Join-Path $probeDirectory 'DMicroservices.Redis.Compatibility.dll'))) {
    throw 'Build the Redis compatibility project before using -NoBuild.'
}
$fixtureProject = 'dmicro-redis-compat-' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
try {
    & docker compose -p $fixtureProject -f $composeFile up -d --pull never
    if ($LASTEXITCODE -ne 0) { throw 'Disposable Redis fixture failed to start.' }
    $probeMount = $probeDirectory + ':/app:ro'
    foreach ($mode in @('standalone', 'sentinel')) {
        $redisAddress = if ($mode -eq 'standalone') { 'standalone:6379,password=compat-redis' } else {
            'sentinel:26379,serviceName=compat-master,password=compat-redis'
        }
        $probeArgument = if ($mode -eq 'standalone') { '--redis-probe' } else { '--sentinel-probe' }
        & docker run --rm --pull never --network ($fixtureProject + '_default') -v $probeMount -w /app --env REDIS_TEST_ENABLE=1 --env REDIS_TEST_ALLOW_FAILOVER=1 --env "REDIS_URL=$redisAddress" --env REDIS_SENTINEL_PASS=compat-sentinel mcr.microsoft.com/dotnet/aspnet:5.0 dotnet DMicroservices.Redis.Compatibility.dll $probeArgument
        if ($LASTEXITCODE -ne 0) { throw "Redis compatibility scenario failed: $mode" }
    }
}
finally {
    & docker compose -p $fixtureProject -f $composeFile down --volumes
    if ($LASTEXITCODE -ne 0) { Write-Warning "Fixture cleanup failed; project: $fixtureProject" }
}
