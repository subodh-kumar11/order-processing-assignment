$ErrorActionPreference = 'Stop'
if (-not (Get-Command node -ErrorAction SilentlyContinue)) { throw 'Install Node.js 24 to run the integration tests.' }
& (Join-Path $PSScriptRoot 'Build.ps1')
& node --test (Join-Path (Split-Path $PSScriptRoot -Parent) 'tests/api.test.mjs')
if ($LASTEXITCODE -ne 0) { throw 'Integration tests failed.' }
