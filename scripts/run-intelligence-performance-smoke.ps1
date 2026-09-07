# Intelligence release performance smoke: 100-branch forecast batch + GUI E2E.
# Requires: dotnet SDK 10, Node + Playwright (frontend/node_modules installed).
# Target: full forecast batch for 100 branches under 5 minutes.

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$backend = Join-Path $root "backend"
$csproj = Join-Path $backend "tests\OnlineSupermarket.Infrastructure.Tests\OnlineSupermarket.Infrastructure.Tests.csproj"
$frontend = Join-Path $root "frontend"

Write-Host "==> Running 100-branch forecast performance smoke"
$perfSw = [System.Diagnostics.Stopwatch]::StartNew()
dotnet test $csproj --filter "FullyQualifiedName~PerformanceSmoke"
if ($LASTEXITCODE -ne 0) { throw "Forecast performance smoke failed." }
$perfSw.Stop()
Write-Host ("Forecast batch (100 branches): {0:mm}:{0:ss}.{0:fff}" -f $perfSw.Elapsed)
if ($perfSw.Elapsed -gt [TimeSpan]::FromMinutes(5)) {
    throw "Forecast batch exceeded the 5 minute target."
}

Write-Host "==> Running inventory->forecast GUI flow"
Push-Location $frontend
try {
    node src/test/run-intelligence-gui-tests.mjs
    if ($LASTEXITCODE -ne 0) { throw "GUI E2E flow failed." }
}
finally {
    Pop-Location
}

Write-Host "==> Performance smoke PASSED"