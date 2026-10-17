#requires -Version 7.0
$ErrorActionPreference = 'Stop'
$envPath = Join-Path $PSScriptRoot '..\.env'
if (-not (Test-Path -LiteralPath $envPath)) { throw 'Initialize development credentials first.' }
$settings = @{}
Get-Content -LiteralPath $envPath | ForEach-Object {
    if ($_ -match '^([A-Z_]+)=(.*)$') {
        if ($settings.ContainsKey($Matches[1])) { throw "Duplicate .env key: $($Matches[1])" }
        $settings[$Matches[1]] = $Matches[2]
    }
}
$keys = @('BRAIN_UI_ADMIN_TOKEN', 'BRAIN_UI_ADMIN_TOKEN_HASH')
$present = @($keys | Where-Object { $settings.ContainsKey($_) })
if ($present.Count -eq 1) { throw 'Incomplete UI administrator credentials. Preserve .env and repair both UI token settings explicitly.' }
if ($present.Count -eq 2) {
    $actualHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
        [Text.Encoding]::UTF8.GetBytes($settings.BRAIN_UI_ADMIN_TOKEN)))
    if ($settings.BRAIN_UI_ADMIN_TOKEN.Length -lt 32 -or $settings.BRAIN_UI_ADMIN_TOKEN.Length -gt 1024 -or
        $actualHash -cne $settings.BRAIN_UI_ADMIN_TOKEN_HASH.ToUpperInvariant()) {
        throw 'UI administrator credential digest is invalid. No settings were changed.'
    }
    Write-Host 'Preserving existing UI administrator credentials.'
    return
}
$token = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)).ToLowerInvariant()
$hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
    [Text.Encoding]::UTF8.GetBytes($token))).ToLowerInvariant()
Add-Content -LiteralPath $envPath -Value "`nBRAIN_UI_ADMIN_TOKEN=$token`nBRAIN_UI_ADMIN_TOKEN_HASH=$hash"
Write-Host 'Added a distinct UI administrator token to ignored .env; all existing credentials were preserved.'
