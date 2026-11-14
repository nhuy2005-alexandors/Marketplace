$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$scriptPath = Join-Path $repoRoot 'scripts\local-acceptance\warm-embedding-cache.ps1'
$tempDirectory = Join-Path ([IO.Path]::GetTempPath()) ('rag-warmup-test-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempDirectory | Out-Null
try {
    $cachePath = Join-Path $tempDirectory 'cache.json'
    $entry = @{
        cacheKey = 'hash:model:2'; contentHash = 'hash'; modelName = 'model'; dimensions = 2
        productId = 1; chunkType = 'summary'; vector = @(0.25, 0.75)
    }
    [IO.File]::WriteAllText($cachePath, (ConvertTo-Json -InputObject @($entry) -Depth 5 -Compress))
    $output = & powershell -NoProfile -ExecutionPolicy Bypass -File $scriptPath -ForecastOnly `
        -CachePath $cachePath -ExpectedChunks 32 -BatchSize 16 -BatchDelaySeconds 3
    if ($LASTEXITCODE -ne 0) { throw "Warmup forecast process failed: $LASTEXITCODE" }
    $text = $output -join "`n"
    if ($text -notmatch 'valid_cached=1; missing=31; estimated_batches=2; pacing_wait_seconds_at_least=3') {
        throw "Unexpected forecast output: $text"
    }
    Write-Output 'PASS: cache validation and warm-up forecast run without network, DB, or API key.'
}
finally {
    Remove-Item -LiteralPath $tempDirectory -Recurse -Force -ErrorAction SilentlyContinue
}
