param([string]$BaseUrl = 'http://localhost:5080', [switch]$TestKnowledgeWrites)
$ErrorActionPreference = 'Stop'
$credentials = @{}
Get-Content -LiteralPath (Join-Path $PSScriptRoot '../.env') | ForEach-Object {
    if ($_ -match '^([A-Z_]+)=(.+)$') { $credentials[$Matches[1]] = $Matches[2] }
}
if (-not $credentials.ContainsKey('BRAIN_CODEX_TOKEN')) { throw 'Initialize development credentials first.' }
$headers = @{ 'X-Agent-Id' = 'codex'; Authorization = "Bearer $($credentials['BRAIN_CODEX_TOKEN'])" }
$ready = $false
for ($attempt = 0; $attempt -lt 30; $attempt++) {
    try {
        $null = Invoke-RestMethod "$BaseUrl/health/ready" -Headers $headers
        $ready = $true
        break
    } catch { Start-Sleep -Seconds 2 }
}
if (-not $ready) { throw 'API did not become ready. Inspect docker compose logs api.' }
$memory = $null
try {
    $memory = Invoke-RestMethod "$BaseUrl/api/memories" -Method Post -Headers $headers -ContentType 'application/json' -Body (@{
        memoryType = 'incident'; subject = 'Brain smoke test'; content = 'Proxmox corosync cluster configuration is missing.'
        importance = 0.9; confidence = 0.95; metadata = @{ smokeTest = $true }
    } | ConvertTo-Json)
    $read = Invoke-RestMethod "$BaseUrl/api/memories/$($memory.id)" -Headers $headers
    if ($read.content -ne $memory.content -or $read.agentId -ne 'codex') { throw 'Memory round trip failed.' }
    $results = @(Invoke-RestMethod "$BaseUrl/api/memories/search" -Method Post -Headers $headers -ContentType 'application/json' -Body (@{
        query = 'Proxmox corosync cluster'; subject = 'Brain smoke test'; agentId = 'codex'; limit = 100
    } | ConvertTo-Json))
    if ($memory.id -notin $results.id) { throw 'Vector search did not return the stored memory.' }
    $schema = Invoke-RestMethod "$BaseUrl/openapi/v1.json" -Headers $headers
    if (-not $schema.paths.'/api/memories/search') { throw 'OpenAPI document is missing memory search.' }
    $adminHeaders = @{ 'X-Agent-Id' = 'willem-de-jong'; Authorization = "Bearer $($credentials['BRAIN_WILLEM_TOKEN'])" }
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        $scan = Invoke-RestMethod "$BaseUrl/api/knowledge/scan" -Method Post -Headers $adminHeaders
        if (-not $scan.alreadyRunning) { break }
        Start-Sleep -Seconds 1
    }
    if ($scan.alreadyRunning) { throw 'Knowledge scan remained busy.' }
    if ($scan.failures.Count -gt 0) { throw 'Vault scan reported failures.' }
    $hits = @(Invoke-RestMethod "$BaseUrl/api/knowledge/search" -Method Post -Headers $headers -ContentType 'application/json' -Body (@{
        query = 'Proxmox corosync cluster recovery'; pathPrefix = 'Infrastructure'; limit = 20
    } | ConvertTo-Json))
    if ('Infrastructure/Proxmox/nacht-02.md' -notin $hits.source) { throw 'Sample knowledge was not indexed.' }
    $document = Invoke-RestMethod "$BaseUrl/api/knowledge/document?path=Infrastructure/Proxmox/nacht-02.md" -Headers $headers
    if (-not $document.revisionToken -or -not $document.indexCurrent) { throw 'Document read did not return a current indexed revision.' }
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        $registryScan = Invoke-RestMethod "$BaseUrl/api/registry/scan" -Method Post -Headers $adminHeaders
        if (-not $registryScan.alreadyRunning) { break }
        Start-Sleep -Seconds 1
    }
    if ($registryScan.alreadyRunning) { throw 'Registry scan remained busy.' }
    $discoveryBody = @{ query = 'I need to deploy an application to production'; limit = 10 } | ConvertTo-Json
    $allowed = Invoke-RestMethod "$BaseUrl/api/capabilities/search" -Method Post -Headers $headers -ContentType 'application/json' -Body $discoveryBody
    if ('plan-deployment' -notin $allowed.skills.id -or 'deploy-service' -in $allowed.skills.id -or $allowed.tools.Count -ne 0) { throw 'Limited-agent capability filtering failed.' }
    $production = Invoke-RestMethod "$BaseUrl/api/capabilities/search" -Method Post -Headers $adminHeaders -ContentType 'application/json' -Body $discoveryBody
    if ('deploy-service' -notin $production.skills.id -or @('gitlab', 'coolify', 'infisical').Where({ $_ -notin $production.tools.id }).Count -gt 0) { throw 'Authorized deployment discovery failed.' }
    $skill = Invoke-RestMethod "$BaseUrl/api/skills/plan-deployment" -Headers $headers
    if (-not $skill.instructions -or -not $schema.paths.'/api/capabilities/search') { throw 'Skill read or registry OpenAPI failed.' }
    Write-Host 'Passed: authenticated memory/knowledge round trips, pgvector search, permission-filtered skills/tools discovery and OpenAPI.'
    & (Join-Path $PSScriptRoot 'Test-Mcp.ps1') -BaseUrl $BaseUrl
    if ($TestKnowledgeWrites) {
        $writePath = 'Incidents/brain-write-smoke-' + [Guid]::NewGuid().ToString('N') + '.md'
        $proposal = Invoke-RestMethod "$BaseUrl/api/knowledge/proposals" -Method Post -Headers $headers -ContentType 'application/json' -Body (@{
            path = $writePath; operation = 'create_document'; content = "# Brain Writer smoke test`nCreated through an approved knowledge proposal."
            reason = 'Development smoke test of the proposal queue, Git history and Brain Writer.'
        } | ConvertTo-Json)
        if ($proposal.status -ne 'pending') { throw 'New proposals must await review.' }
        $preview = Invoke-RestMethod "$BaseUrl/api/knowledge/proposals/$($proposal.id)/preview" -Headers $adminHeaders
        if (-not $preview.revisionMatches) { throw 'New document preview failed.' }
        $approved = Invoke-RestMethod "$BaseUrl/api/knowledge/proposals/$($proposal.id)/approve" -Method Post -Headers $adminHeaders -ContentType 'application/json' -Body (@{
            version = $proposal.version; reason = 'Approve this isolated development smoke test.'
        } | ConvertTo-Json)
        for ($attempt = 0; $attempt -lt 60; $attempt++) {
            $applied = Invoke-RestMethod "$BaseUrl/api/knowledge/proposals/$($proposal.id)" -Headers $headers
            if ($applied.status -in @('applied', 'conflict', 'rejected')) { break }
            Start-Sleep -Seconds 1
        }
        if ($applied.status -ne 'applied' -or -not $applied.gitCommit) { throw "Writer did not apply the proposal: $($applied.status) $($applied.lastError). Check WriterEnabled and worker logs." }
        $written = Invoke-RestMethod "$BaseUrl/api/knowledge/document?path=$([Uri]::EscapeDataString($writePath))" -Headers $headers
        if ($written.content -ne $proposal.content) { throw 'Writer output did not match the approved content.' }
        Write-Host 'Passed: pending proposal, review preview, explicit approval, background writer, canonical read and recorded Git commit.'
    }
} finally {
    if ($null -ne $memory) { $null = Invoke-RestMethod "$BaseUrl/api/memories/$($memory.id)" -Method Delete -Headers $headers }
}
