param(
    [string]$BaseUrl = 'http://localhost:5215',
    [string]$CachePath = (Join-Path (Split-Path $PSScriptRoot -Parent | Split-Path -Parent) 'obj\rag-embedding-cache.json'),
    [ValidateRange(1,100000)][int]$ExpectedChunks = 1395,
    [ValidateRange(1,128)][int]$BatchSize = 16,
    [ValidateRange(0,60)][int]$BatchDelaySeconds = 3,
    [ValidateRange(1,120)][int]$PollSeconds = 10,
    [ValidateRange(1,180)][int]$TimeoutMinutes = 45,
    [switch]$ForecastOnly
)

$ErrorActionPreference = 'Stop'

function Get-ValidCacheCount([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return 0 }
    try { $entries = @(Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -ErrorAction Stop) }
    catch { Write-Warning 'Cache JSON is unreadable; forecast treats it as empty.'; return 0 }
    $keys = @{}
    foreach ($entry in $entries) {
        $valid = $entry.contentHash -and $entry.modelName -and [int]$entry.dimensions -gt 0 -and
            $entry.cacheKey -eq ('{0}:{1}:{2}' -f $entry.contentHash, $entry.modelName, $entry.dimensions) -and
            @($entry.vector).Count -eq [int]$entry.dimensions
        if ($valid) { $keys[[string]$entry.cacheKey] = $true }
    }
    return $keys.Count
}

$cached = Get-ValidCacheCount $CachePath
$missing = [Math]::Max(0, $ExpectedChunks - $cached)
$batches = [int][Math]::Ceiling($missing / [double]$BatchSize)
$paceEstimate = [Math]::Max(0, $batches - 1) * $BatchDelaySeconds
$minutesEstimate = [Math]::Round($paceEstimate / 60.0, 1)
Write-Output ('Warmup forecast: expected_chunks={0}; valid_cached={1}; missing={2}; estimated_batches={3}; pacing_wait_seconds_at_least={4} (~{5} min). Estimate excludes provider latency and 429 backoff.' -f $ExpectedChunks, $cached, $missing, $batches, $paceEstimate, $minutesEstimate)

if ($ForecastOnly) { exit 0 }
if ($missing -eq 0) { Write-Output 'Cache forecast is complete for the configured expected chunk count.'; exit 0 }
Write-Output 'Start the API with Gemini:EmbeddingWarmupEnabled=true and the same cache path. This script only polls readiness; the API hosted service performs the paced, Retry-After-aware embedding work.'

$deadline = [DateTimeOffset]::UtcNow.AddMinutes($TimeoutMinutes)
$uri = $BaseUrl.TrimEnd('/') + '/api/assistant/readiness'
while ([DateTimeOffset]::UtcNow -lt $deadline) {
    try {
        $status = Invoke-RestMethod -Method Get -Uri $uri -TimeoutSec 8
        if ($status.hybridReady -eq $true) { Write-Output 'Embedding index is ready (hybrid).'; exit 0 }
        Write-Output ('Still warming; retrievalMode={0}. Checking again in {1}s.' -f $status.retrievalMode, $PollSeconds)
        Start-Sleep -Seconds $PollSeconds
    }
    catch {
        $retrySeconds = $PollSeconds
        try {
            $response = $_.Exception.Response
            if ($response -and [int]$response.StatusCode -eq 429 -and $response.Headers.RetryAfter -and $response.Headers.RetryAfter.Delta) {
                $retrySeconds = [Math]::Max($PollSeconds, [int][Math]::Ceiling($response.Headers.RetryAfter.Delta.TotalSeconds))
            }
        } catch { }
        Write-Output ('Readiness unavailable or rate-limited; retrying in {0}s.' -f $retrySeconds)
        Start-Sleep -Seconds $retrySeconds
    }
}
throw ('Timed out waiting for embedding readiness after {0} minutes. The API remains on lexical fallback while warming.' -f $TimeoutMinutes)
