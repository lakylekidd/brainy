#requires -Version 7.0
$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    if (-not (Test-Path -LiteralPath '.env')) { & (Join-Path $PSScriptRoot 'Initialize-Development.ps1') }
    & (Join-Path $PSScriptRoot 'Initialize-UiAdmin.ps1')
    & (Join-Path $PSScriptRoot 'Initialize-DevelopmentVault.ps1')
    & docker compose up --build --detach --wait --wait-timeout 180
    if ($LASTEXITCODE -ne 0) { throw 'Development startup failed. Inspect docker compose ps and docker compose logs; existing data was preserved.' }
    Write-Host 'Brain UI: http://localhost:5173'
    Write-Host 'Sign in using BRAIN_UI_ADMIN_TOKEN from .env. The API is available at http://localhost:5080.'
} finally {
    Pop-Location
}
