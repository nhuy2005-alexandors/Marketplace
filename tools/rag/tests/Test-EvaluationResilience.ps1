$ErrorActionPreference = 'Stop'
$ragTools = Split-Path $PSScriptRoot -Parent
. (Join-Path $ragTools 'AtomicJsonCache.ps1')
. (Join-Path $ragTools 'EvaluationResilience.ps1')

function Assert-Equal($Expected, $Actual, [string]$Message) {
    if ($Expected -ne $Actual) { throw "$Message Expected=[$Expected] Actual=[$Actual]" }
}

$dailyBody = @'
{"error":{"status":"RESOURCE_EXHAUSTED","message":"synthetic-prompt-must-not-print","details":[{"@type":"type.googleapis.com/google.rpc.QuotaFailure","violations":[{"quotaMetric":"generativelanguage.googleapis.com/embed_requests_per_day","quotaId":"EmbedRequestsPerDay","quotaDimensions":{"model":"gemini-embedding-001"}}]},{"@type":"type.googleapis.com/google.rpc.RetryInfo","retryDelay":"3600s"}]}}
'@
$minuteBody = @'
{"error":{"status":"RESOURCE_EXHAUSTED","details":[{"@type":"type.googleapis.com/google.rpc.QuotaFailure","violations":[{"quotaMetric":"generativelanguage.googleapis.com/embed_requests_per_minute","quotaId":"EmbedRequestsPerMinute","quotaDimensions":{"model":"gemini-embedding-001"}}]},{"@type":"type.googleapis.com/google.rpc.RetryInfo","retryDelay":"0.01s"}]}}
'@

$daily = Get-GeminiQuotaDiagnostic -HttpStatus 429 -Body $dailyBody
Assert-Equal 'per-day' $daily.Classification 'Daily quota classification failed.'
Assert-Equal 'RESOURCE_EXHAUSTED' $daily.Status 'Provider status extraction failed.'
Assert-Equal 3600 $daily.RetryDelaySeconds 'RetryInfo extraction failed.'
if (($daily | Out-String) -match 'synthetic-prompt-must-not-print') { throw 'Quota diagnostic exposed an unapproved body field.' }
Assert-Equal 'possible-daily-quota' (Get-Quota429StopReason $daily 0) 'Daily quota must stop immediately.'

$minute = Get-GeminiQuotaDiagnostic -HttpStatus 429 -Body $minuteBody
Assert-Equal 'per-minute' $minute.Classification 'Per-minute quota classification failed.'
Assert-Equal '' (Get-Quota429StopReason $minute 0) 'Per-minute quota should remain retryable.'
$fakeState = [pscustomobject]@{ Calls = 0 }
$waitCount = 0
$fakeProvider = {
    param($state, $minuteDiagnostic)
    $state.Calls++
    if ($state.Calls -eq 1) { return $minuteDiagnostic }
    return [pscustomobject]@{ HttpStatus = 200; Classification = 'unknown'; RetryDelaySeconds = $null }
}
$first = & $fakeProvider $fakeState $minute
$firstReason = Get-Quota429StopReason $first 0
if (-not $firstReason -and $first.RetryDelaySeconds -gt 0) {
    Start-Sleep -Milliseconds ([int]($first.RetryDelaySeconds * 1000))
    $waitCount++
}
    $second = & $fakeProvider $fakeState $minute
Assert-Equal 2 $fakeState.Calls 'Per-minute fake provider should retry and then succeed.'
Assert-Equal 1 $waitCount 'Retry delay should be honored before continuing.'
Assert-Equal 200 $second.HttpStatus 'Per-minute fake provider did not reach success.'
Assert-Equal 'possible-daily-quota' (Get-Quota429StopReason $minute 2) 'Two completed long 429 waits should stop early.'

$testRoot = Join-Path $PSScriptRoot ('temp-resilience-' + [Guid]::NewGuid().ToString('N'))
$queryCachePath = Join-Path $testRoot 'query-cache.json'
$model = 'gemini-embedding-001'
$dimensions = 2
try {
    [void][IO.Directory]::CreateDirectory($testRoot)
    $query = 'synthetic query only'
    $queryKey = Get-QueryVectorCacheKey $query $model $dimensions
    $entry = @{ cacheKey = $queryKey; contentHash = ($queryKey -split ':')[0]; modelName = $model; dimensions = $dimensions; vector = @([single]0.25, [single]0.75) }
    Write-AtomicCacheFile -Path $queryCachePath -Content (ConvertTo-Json -InputObject @($entry) -Compress)
    $cache = Read-QueryVectorCache -Path $queryCachePath -ModelName $model -Dimensions $dimensions
    $unexpectedEmbed = { throw 'Query cache hit should not call the fake embedder.' }
    $cachedVectors = Resolve-CachedQueryVectors -Queries @($query) -Path $queryCachePath -ModelName $model -Dimensions $dimensions -Cache $cache -EmbedMissing $unexpectedEmbed
    Assert-Equal 0.25 ([single]$cachedVectors[0][0]) 'Query cache hit returned the wrong vector.'

    [IO.File]::WriteAllText($queryCachePath, 'malformed cache content')
    $safeCache = Read-QueryVectorCache -Path $queryCachePath -ModelName $model -Dimensions $dimensions
    Assert-Equal 0 $safeCache.Count 'Corrupt query cache should be ignored.'
    $fakeEmbedder = {
        param($queries)
        $vectors = New-Object 'System.Collections.Generic.List[object]'
        foreach ($unused in $queries) { $vectors.Add([single[]]@(0.5, 0.5)) }
        return ,$vectors.ToArray()
    }
    $rebuilt = Resolve-CachedQueryVectors -Queries @($query) -Path $queryCachePath -ModelName $model -Dimensions $dimensions -Cache $safeCache -EmbedMissing $fakeEmbedder
    Assert-Equal 0.5 ([single]$rebuilt[0][0]) 'Corrupt cache recovery returned the wrong vector.'
    Assert-Equal 1 (Read-QueryVectorCache -Path $queryCachePath -ModelName $model -Dimensions $dimensions).Count 'Corrupt cache was not safely replaced.'
}
finally {
    if (Test-Path -LiteralPath $testRoot) { Remove-Item -LiteralPath $testRoot -Recurse -Force }
}

Write-Output 'EvaluationResilienceTest=PASS;PerDayEarlyStop=TRUE;PerMinuteRetry=TRUE;QueryCacheHit=TRUE;CorruptCacheSafe=TRUE'
