param(
    [Parameter(Mandatory = $true)][string]$PackagePath,
    [Parameter(Mandatory = $true)][ValidatePattern('^[0-9a-fA-F]{64}$')][string]$Sha256,
    [string]$DestinationDirectory = (Join-Path $env:LOCALAPPDATA 'HomelabBrain\bin')
)

$source = (Resolve-Path -LiteralPath $PackagePath).Path
if ([IO.Path]::GetFileName($source) -ne 'brain.exe') { throw 'Expected a published brain.exe package.' }
$actual = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
if ($actual -ne $Sha256.ToUpperInvariant()) { throw 'Brain Client package checksum mismatch.' }
$destination = [IO.Path]::GetFullPath($DestinationDirectory)
[IO.Directory]::CreateDirectory($destination) | Out-Null
$target = Join-Path $destination 'brain.exe'
$temporary = Join-Path $destination ('brain.' + [guid]::NewGuid().ToString('N') + '.tmp')
try {
    Copy-Item -LiteralPath $source -Destination $temporary
    Move-Item -LiteralPath $temporary -Destination $target -Force
} finally {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
}
Write-Output "Installed Brain Client at $target"
