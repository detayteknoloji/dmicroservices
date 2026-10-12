param([switch]$NoBuild)
$ErrorActionPreference = 'Stop'
$projectFile = Join-Path $PSScriptRoot 'DMicroservices.Elastic.Smoke.csproj'
$composeFile = Join-Path $PSScriptRoot 'Fixture/compose.yml'
$probeDirectory = Join-Path $PSScriptRoot 'bin/Release/net5.0'
if (-not $NoBuild) {
    & dotnet build $projectFile --no-restore -c Release -m:1 -nr:false -p:GeneratePackageOnBuild=false
    if ($LASTEXITCODE -ne 0) { throw 'Elasticsearch compatibility build failed.' }
}
$probeAssembly = Join-Path $probeDirectory 'DMicroservices.Elastic.Smoke.dll'
if (-not (Test-Path -LiteralPath $probeAssembly)) { throw 'Build the smoke project before using -NoBuild.' }
$fixtureId = [Guid]::NewGuid().ToString('N').Substring(0, 12)
$fixtureDirectory = Join-Path $PSScriptRoot ('TestResults/tls-' + $fixtureId)
& dotnet $probeAssembly --prepare-fixture $fixtureDirectory
if ($LASTEXITCODE -ne 0) { throw 'Disposable TLS fixture generation failed.' }
$previousVersion = $env:ELASTIC_COMPAT_VERSION
$previousFixture = $env:ELASTIC_COMPAT_FIXTURE
try {
    $env:ELASTIC_COMPAT_FIXTURE = $fixtureDirectory
    foreach ($version in @('7.16.1', '8.15.0')) {
        $env:ELASTIC_COMPAT_VERSION = $version
        $fixtureProject = 'dmicro-elastic-compat-' + $version.Replace('.', '-') + '-' + $fixtureId
        try {
            & docker compose -p $fixtureProject -f $composeFile up -d --pull never --wait --wait-timeout 120
            if ($LASTEXITCODE -ne 0) { throw "Elasticsearch $version fixture failed to start." }
            $probeMount = $probeDirectory + ':/app:ro'
            $certificateMount = $fixtureDirectory + ':/fixture:ro'
            & docker run --rm --pull never --network ($fixtureProject + '_default') -v $probeMount -v $certificateMount -w /app --env ELASTIC_URI=https://elasticsearch:9200 --env ELASTIC_USERNAME=elastic --env ELASTIC_PASSWORD=compat-elastic --env ELASTIC_URI_SSL=/fixture/ca.crt --env IS_FILE_LOG=false --env ELASTIC_BOOTSTRAP_METHOD=None --env "LOG_INDEX_FORMAT=compat-$fixtureId-{0:yyyy.MM.dd}" --env "COMPAT_EXPECTED_ELASTIC=$version" --env COMPAT_VERIFY_HELPER=1 --env SMOKE_VERIFY_TIMEOUT_SECONDS=60 mcr.microsoft.com/dotnet/aspnet:5.0 dotnet DMicroservices.Elastic.Smoke.dll
            if ($LASTEXITCODE -ne 0) { throw "Elasticsearch compatibility scenario failed: $version" }
            Write-Output "PASS ELASTIC COMPATIBILITY: $version; Linux .NET 5, HTTPS, Basic Auth, private CA, logger routes/flush, NEST helper."
        }
        catch {
            & docker compose -p $fixtureProject -f $composeFile logs --no-color --tail 80 elasticsearch
            throw
        }
        finally {
            & docker compose -p $fixtureProject -f $composeFile down --volumes
            if ($LASTEXITCODE -ne 0) { Write-Warning "Fixture cleanup failed; project: $fixtureProject" }
        }
    }
}
finally {
    $env:ELASTIC_COMPAT_VERSION = $previousVersion
    $env:ELASTIC_COMPAT_FIXTURE = $previousFixture
}
