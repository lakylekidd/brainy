param([string]$VaultPath = (Join-Path $PSScriptRoot '../.local/vault'))
$ErrorActionPreference = 'Stop'
$resolvedVault = [IO.Path]::GetFullPath($VaultPath)
if (Test-Path -LiteralPath $resolvedVault) {
    if (Test-Path -LiteralPath (Join-Path $resolvedVault '.git') -PathType Container) {
        Write-Host "Development vault already exists at $resolvedVault; preserving all existing files."
        return
    }
    throw 'The target already exists without a dedicated .git directory. Choose an empty new location; no files were overwritten.'
}
New-Item -ItemType Directory -Path $resolvedVault | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../sample-vault/Infrastructure') -Destination $resolvedVault -Recurse
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../sample-vault/Services') -Destination $resolvedVault -Recurse
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../sample-vault/Procedures') -Destination $resolvedVault -Recurse
Set-Content -LiteralPath (Join-Path $resolvedVault '.gitattributes') -Value '*.md -text'
function Invoke-VaultGit([string[]]$GitArguments) {
    & git -C $resolvedVault -c core.autocrlf=false -c commit.gpgsign=false -c "core.hooksPath=$resolvedVault/.git/HEAD" @GitArguments
    if ($LASTEXITCODE -ne 0) { throw 'Development vault Git initialization failed; inspect the new directory before retrying.' }
}
Invoke-VaultGit @('init', '--initial-branch=main')
Invoke-VaultGit @('config', 'core.autocrlf', 'false')
Invoke-VaultGit @('add', '--all')
Invoke-VaultGit @('-c', 'user.name=Homelab Brain', '-c', 'user.email=brain@localhost', 'commit', '-m', 'docs: initialize development knowledge vault')
Write-Host "Initialized dedicated development knowledge repository at $resolvedVault"
