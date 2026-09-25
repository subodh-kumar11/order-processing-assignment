param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install the .NET 10 SDK first.' }
$names = @('APPDATA', 'DOTNET_CLI_HOME', 'NUGET_PACKAGES', 'DOTNET_CLI_TELEMETRY_OPTOUT', 'DOTNET_ADD_GLOBAL_TOOLS_TO_PATH', 'DOTNET_GENERATE_ASPNET_CERTIFICATE')
$saved = @{}
foreach ($name in $names) { $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
Push-Location $projectRoot
try {
    $env:APPDATA = Join-Path $projectRoot '.local/appdata'
    $env:DOTNET_CLI_HOME = Join-Path $projectRoot '.local/sdk-home'
    $env:NUGET_PACKAGES = Join-Path $projectRoot '.local/packages'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    & dotnet restore OrderProcessing.csproj --configfile NuGet.Config
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
    & dotnet build OrderProcessing.csproj -c $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}
finally {
    Pop-Location
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process') }
}
