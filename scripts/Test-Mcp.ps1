param([string]$BaseUrl = 'http://localhost:5080', [switch]$TestKnowledgeProposal)
$ErrorActionPreference = 'Stop'
$credentials = @{}
Get-Content -LiteralPath (Join-Path $PSScriptRoot '../.env') | ForEach-Object {
    if ($_ -match '^([A-Z_]+)=(.+)$') { $credentials[$Matches[1]] = $Matches[2] }
}
if (-not $credentials.ContainsKey('BRAIN_CODEX_TOKEN')) { throw 'Initialize development credentials first.' }
$mcpHeaders = @{
    'X-Agent-Id' = 'codex'; Authorization = "Bearer $($credentials['BRAIN_CODEX_TOKEN'])"
    Accept = 'application/json, text/event-stream'; 'MCP-Protocol-Version' = '2025-11-25'
}
$mcpRequestSequence = 0
function Invoke-BrainMcpRequest([string]$Method, [object]$Parameters) {
    $script:mcpRequestSequence++
    $requestId = $script:mcpRequestSequence
    $body = @{ jsonrpc = '2.0'; id = $requestId; method = $Method; params = $Parameters } | ConvertTo-Json -Depth 20 -Compress
    $response = Invoke-WebRequest "$($BaseUrl.TrimEnd('/'))/mcp" -Method Post -Headers $mcpHeaders -ContentType 'application/json' -Body $body
    if ($response.Headers.ContainsKey('Mcp-Session-Id')) { throw 'Expected stateless MCP transport.' }
    $messages = @()
    if ($response.Headers['Content-Type'][0] -like 'application/json*') { $messages = @($response.Content | ConvertFrom-Json) }
    else {
        # Streamable HTTP may return SSE; concatenate the data lines within each event.
        foreach ($eventBlock in [regex]::Split([string]$response.Content, '\r?\n\r?\n')) {
            $data = @($eventBlock -split '\r?\n' | Where-Object { $_.StartsWith('data:') } | ForEach-Object { $_.Substring(5).TrimStart(' ') }) -join "`n"
            if ($data) { $messages += $data | ConvertFrom-Json }
        }
    }
    $reply = @($messages | Where-Object { $_.id -eq $requestId })
    if ($reply.Count -ne 1) { throw "MCP $Method did not return one matching response." }
    if ($reply[0].error) { throw "MCP $Method protocol error: $($reply[0].error.message)" }
    return $reply[0].result
}
function Invoke-BrainMcpTool([string]$Name, [hashtable]$Arguments) {
    $result = Invoke-BrainMcpRequest 'tools/call' @{ name = $Name; arguments = $Arguments }
    if ($result.isError) { throw "MCP $Name failed: $($result.content.text -join ' ')" }
    if ($null -eq $result.structuredContent) { throw "MCP $Name did not return structured content." }
    return $result.structuredContent
}
$memory = $null
try {
    $initialized = Invoke-BrainMcpRequest 'initialize' @{
        protocolVersion = '2025-11-25'; capabilities = @{}; clientInfo = @{ name = 'brain-development-smoke'; version = '1.0' }
    }
    if ($initialized.serverInfo.name -ne 'homelab-brain') { throw 'Unexpected MCP server identity.' }
    $null = Invoke-WebRequest "$($BaseUrl.TrimEnd('/'))/mcp" -Method Post -Headers $mcpHeaders -ContentType 'application/json' -Body '{"jsonrpc":"2.0","method":"notifications/initialized"}'
    $list = Invoke-BrainMcpRequest 'tools/list' @{}
    $expected = @('brain_search', 'brain_remember', 'brain_search_memories', 'brain_read_document',
        'brain_propose_knowledge_change', 'brain_find_skills', 'brain_get_skill', 'brain_find_capabilities')
    if ($list.tools.Count -ne 8 -or $expected.Where({ $_ -notin $list.tools.name }).Count -gt 0) { throw 'MCP tool discovery is incomplete.' }
    $subject = 'MCP smoke ' + [Guid]::NewGuid().ToString('N')
    $memory = Invoke-BrainMcpTool 'brain_remember' @{ subject = $subject; content = 'Proxmox corosync recovery observation.'; memoryType = 'observation' }
    if ($memory.agentId -ne 'codex' -or $memory.visibility -ne 'private') { throw 'MCP memory identity/default privacy failed.' }
    $memories = Invoke-BrainMcpTool 'brain_search_memories' @{ query = 'corosync recovery'; subject = $subject }
    if ($memory.id -notin $memories.memories.id) { throw 'MCP memory search failed.' }
    $document = Invoke-BrainMcpTool 'brain_read_document' @{ path = 'Infrastructure/Proxmox/nacht-02.md' }
    if (-not $document.revisionToken -or -not $document.content) { throw 'MCP canonical read failed.' }
    $skills = Invoke-BrainMcpTool 'brain_find_skills' @{ query = 'plan an application deployment' }
    if ('plan-deployment' -notin $skills.skills.id -or 'deploy-service' -in $skills.skills.id) { throw 'MCP skill filtering failed.' }
    $skill = Invoke-BrainMcpTool 'brain_get_skill' @{ skillId = 'plan-deployment' }
    if (-not $skill.instructions) { throw 'MCP skill read failed.' }
    $capabilities = Invoke-BrainMcpTool 'brain_find_capabilities' @{ query = 'deploy an application to production' }
    if ('deploy-service' -in $capabilities.skills.id -or $capabilities.tools.Count -ne 0) { throw 'MCP capability filtering failed.' }
    $unified = Invoke-BrainMcpTool 'brain_search' @{ query = 'corosync'; limit = 100 }
    if ($memory.id -notin $unified.memories.id -or $unified.knowledge.Count -eq 0 -or $unified.failures.Count -ne 0) { throw 'MCP unified search failed.' }
    Write-Host 'Passed: MCP initialization, eight-tool discovery, private memory round trip, canonical/skill reads, filtered discovery and unified search.'
    if ($TestKnowledgeProposal) {
        $path = 'Incidents/brain-mcp-smoke-' + [Guid]::NewGuid().ToString('N') + '.md'
        $proposal = Invoke-BrainMcpTool 'brain_propose_knowledge_change' @{
            path = $path; operation = 'create_document'; content = '# MCP pending proposal'; reason = 'Verify MCP queues changes for separate review.'
        }
        if ($proposal.status -ne 'pending' -or $proposal.agentId -ne 'codex') { throw 'MCP proposal did not await review.' }
        $canonical = Invoke-WebRequest "$BaseUrl/api/knowledge/document?path=$([Uri]::EscapeDataString($path))" -Headers $mcpHeaders -SkipHttpErrorCheck
        if ([int]$canonical.StatusCode -ne 404) { throw 'Pending MCP proposal modified canonical knowledge.' }
        Write-Host "Passed: MCP proposal remains pending without creating a canonical file. Proposal: $($proposal.id)"
    }
} finally {
    if ($null -ne $memory) {
        $null = Invoke-RestMethod "$BaseUrl/api/memories/$($memory.id)" -Method Delete -Headers $mcpHeaders
    }
}
