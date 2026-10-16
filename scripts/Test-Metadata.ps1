param([string]$BaseUrl = 'http://localhost:5080', [switch]$ApplySampleMetadata)
$ErrorActionPreference = 'Stop'
$credentials = @{}
Get-Content -LiteralPath (Join-Path $PSScriptRoot '../.env') | ForEach-Object {
    if ($_ -match '^([A-Z_]+)=(.+)$') { $credentials[$Matches[1]] = $Matches[2] }
}
$headers = @{ 'X-Agent-Id' = 'willem-de-jong'; Authorization = "Bearer $($credentials['BRAIN_WILLEM_TOKEN'])" }
for ($attempt = 0; $attempt -lt 30; $attempt++) {
    $scan = Invoke-RestMethod "$BaseUrl/api/knowledge/metadata/scan" -Method Post -Headers $headers
    if (-not $scan.alreadyRunning) { break }
    Start-Sleep -Seconds 1
}
if ($scan.alreadyRunning -or $scan.failed -gt 0) { throw 'Metadata scan remained busy or failed.' }
$issues = @(Invoke-RestMethod "$BaseUrl/api/knowledge/metadata/issues" -Headers $headers | ForEach-Object { foreach ($item in $_) { $item } })
$proposals = @(Invoke-RestMethod "$BaseUrl/api/knowledge/proposals?type=metadata_enrichment&status=pending&limit=100" -Headers $headers | ForEach-Object { foreach ($item in $_) { $item } })
foreach ($proposal in $proposals) {
    if ($proposal.type -ne 'metadata_enrichment' -or -not $proposal.metadataSchemaVersion -or -not $proposal.revisionToken) { throw 'Invalid generated proposal provenance.' }
}
$samplePath = 'Infrastructure/Proxmox/nacht-02.md'
$sample = @($proposals | Where-Object { $_.path -eq $samplePath }) | Select-Object -First 1
$before = Invoke-RestMethod "$BaseUrl/api/knowledge/document?path=$([Uri]::EscapeDataString($samplePath))" -Headers $headers
if ($sample) {
    $preview = Invoke-RestMethod "$BaseUrl/api/knowledge/proposals/$($sample.id)/preview" -Headers $headers
    if (-not $preview.revisionMatches -or $preview.before -ne $before.content) { throw 'Metadata preview does not match the current document.' }
    $patch = $sample.content | ConvertFrom-Json
    if (-not $patch.fields.metadata_version -or -not $patch.schema_hash) { throw 'Metadata proposal must contain a structured patch.' }
    $unchanged = Invoke-RestMethod "$BaseUrl/api/knowledge/document?path=$([Uri]::EscapeDataString($samplePath))" -Headers $headers
    if ($unchanged.revisionToken -ne $before.revisionToken) { throw 'Pending metadata proposal changed canonical knowledge.' }
}
Write-Host "Passed: metadata scan, persisted issues, typed pending proposals and review preview. Issues: $($issues.Count); pending metadata proposals: $($proposals.Count)."
if ($ApplySampleMetadata) {
    if (-not $sample) { throw 'No current pending metadata proposal exists for the sample document; it may already be enriched.' }
    $null = Invoke-RestMethod "$BaseUrl/api/knowledge/proposals/$($sample.id)/approve" -Method Post -Headers $headers -ContentType 'application/json' -Body (@{
        version = $sample.version; reason = 'Explicit development smoke test approval of sample metadata.'
    } | ConvertTo-Json)
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        $applied = Invoke-RestMethod "$BaseUrl/api/knowledge/proposals/$($sample.id)" -Headers $headers
        if ($applied.status -in @('applied', 'conflict', 'rejected')) { break }
        Start-Sleep -Seconds 1
    }
    if ($applied.status -ne 'applied' -or -not $applied.gitCommit) { throw "Metadata writer failed: $($applied.status) $($applied.lastError)" }
    $after = Invoke-RestMethod "$BaseUrl/api/knowledge/document?path=$([Uri]::EscapeDataString($samplePath))" -Headers $headers
    if ($after.content -ne $preview.after -or $after.revisionToken -eq $before.revisionToken) { throw 'Applied metadata does not match reviewed preview.' }
    Write-Host 'Passed: explicitly reviewed metadata applied by the worker, with a Git commit and matching canonical content.'
}
