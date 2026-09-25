param(
    [ValidateRange(1024,65535)][int]$Port = 5080,
    [ValidateRange(1,86400)][int]$ProcessingIntervalSeconds = 300
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'Build.ps1')
$previousInterval = $env:Orders__ProcessingIntervalSeconds
Push-Location $projectRoot
try {
    $env:Orders__ProcessingIntervalSeconds = [string]$ProcessingIntervalSeconds
    Write-Host "API: http://localhost:$Port | health: http://localhost:$Port/health"
    Write-Host "Processing interval: $ProcessingIntervalSeconds seconds. Press Ctrl+C to stop."
    & dotnet bin/Release/net10.0/OrderProcessing.dll --urls "http://localhost:$Port"
    if ($LASTEXITCODE -ne 0) { throw "Application exited with code $LASTEXITCODE" }
}
finally {
    Pop-Location
    $env:Orders__ProcessingIntervalSeconds = $previousInterval
}
