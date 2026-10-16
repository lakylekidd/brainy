$ErrorActionPreference = 'Stop'
$envPath = Join-Path $PSScriptRoot '../.env'
if (Test-Path -LiteralPath $envPath) { throw '.env already exists. Preserve it or explicitly remove it before regenerating credentials.' }
function New-Token { [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)).ToLowerInvariant() }
function Get-TokenHash([string]$Token) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($Token))).ToLowerInvariant() }
$willemToken = New-Token
$codexToken = New-Token
$writerUid = '1654'
$writerGid = '1654'
if (-not $IsWindows) { $writerUid = (& id -u).Trim(); $writerGid = (& id -g).Trim() }
@"
BRAIN_DATABASE_PASSWORD=$(New-Token)
BRAIN_WILLEM_TOKEN=$willemToken
BRAIN_WILLEM_TOKEN_HASH=$(Get-TokenHash $willemToken)
BRAIN_CODEX_TOKEN=$codexToken
BRAIN_CODEX_TOKEN_HASH=$(Get-TokenHash $codexToken)
BRAIN_WRITER_UID=$writerUid
BRAIN_WRITER_GID=$writerGid
"@ | Set-Content -LiteralPath $envPath
Write-Host 'Created ignored .env with random local credentials. Treat this file as a secret; do not commit it.'
