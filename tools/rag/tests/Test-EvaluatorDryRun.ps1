$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$toolsPath = Join-Path $repoRoot 'tools\rag'
. (Join-Path $toolsPath 'AtomicJsonCache.ps1')
. (Join-Path $toolsPath 'EvaluationResilience.ps1')

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

$tempRoot = Join-Path $PSScriptRoot ('temp-evaluator-e2e-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($tempRoot)
$model = 'gemini-embedding-001'
$dimensions = 2
$catalogCachePath = Join-Path $tempRoot 'catalog-cache.json'
$queryCachePath = Join-Path $tempRoot 'query-cache.json'
$selectionPath = Join-Path $tempRoot 'selection.json'
$fixturePath = Join-Path $tempRoot 'fixture.json'

try {
    $chunks = @(
        @{ productId=1; type='summary'; content='Red apple fruit'; hash='FAKE-CATALOG-1'; vector=@(1.0,0.0) },
        @{ productId=2; type='summary'; content='Blue bike vehicle'; hash='FAKE-CATALOG-2'; vector=@(0.0,1.0) },
        @{ productId=3; type='summary'; content='Green tea beverage'; hash='FAKE-CATALOG-3'; vector=@(1.0,1.0) }
    )
    $catalogEntries = foreach ($chunk in $chunks) {
        @{ cacheKey="$($chunk.hash):$model`:$dimensions"; contentHash=$chunk.hash; modelName=$model; dimensions=$dimensions; productId=$chunk.productId; chunkType=$chunk.type; vector=$chunk.vector }
    }
    Write-AtomicCacheFile -Path $catalogCachePath -Content (ConvertTo-Json -InputObject @($catalogEntries) -Depth 5)
    $cacheSanity = Get-Content -LiteralPath $catalogCachePath -Raw | ConvertFrom-Json
    Assert-True ($cacheSanity.Count -eq 3 -and $cacheSanity[0].cacheKey -eq "$($chunks[0].hash):$model`:$dimensions") 'Synthetic catalog cache fixture is invalid.'

    $development = @(
        @{ id='dev-1'; type='known'; query='red apple'; expected_product_ids=@(1) },
        @{ id='dev-2'; type='known'; query='blue bike'; expected_product_ids=@(2) },
        @{ id='dev-3'; type='no_result'; query='unicorn query'; expected_product_ids=@() }
    )
    $authors = @(
        @{ id='author-1'; type='paraphrase'; query='green tea'; expected_product_ids=@(3) },
        @{ id='author-2'; type='paraphrase'; query='red apple'; expected_product_ids=@(1) }
    )
    $queryEntries = foreach ($row in @($development) + @($authors)) {
        $vector = switch ($row.id) {
            'dev-1' { @(1.0,0.0) }
            'dev-2' { @(0.0,1.0) }
            'dev-3' { @(0.0,0.0) }
            'author-1' { @(1.0,1.0) }
            'author-2' { @(1.0,0.0) }
        }
        $key = Get-QueryVectorCacheKey $row.query $model $dimensions
        @{ cacheKey=$key; contentHash=($key -split ':')[0]; modelName=$model; dimensions=$dimensions; vector=$vector }
    }
    Write-AtomicCacheFile -Path $queryCachePath -Content (ConvertTo-Json -InputObject @($queryEntries) -Depth 5)
    $fixture = @{
        modelName=$model; dimensions=$dimensions; catalogCachePath=$catalogCachePath; queryCachePath=$queryCachePath
        selectionPath=$selectionPath; chunks=$chunks; development=$development; authorFixtures=$authors
    }
    Write-AtomicCacheFile -Path $fixturePath -Content (ConvertTo-Json -InputObject $fixture -Depth 8)

    $powershell = (Get-Command powershell.exe).Source
    $evaluatorPath = Join-Path $toolsPath 'evaluate-v2.ps1'
    $scenarios = @(
        @{ Name='lexical-no-timing'; Args=@('-LexicalOnly') },
        @{ Name='lexical-with-timing'; Args=@('-LexicalOnly','-DryRunTrackRankTiming') },
        @{ Name='hybrid-no-timing'; Args=@('-DevelopmentOnly') },
        @{ Name='hybrid-with-timing'; Args=@('-DevelopmentOnly','-DryRunTrackRankTiming') }
    )
    foreach ($scenario in $scenarios) {
        $arguments = @('-NoProfile','-ExecutionPolicy','Bypass','-File',$evaluatorPath,'-RepoRoot',$repoRoot,'-RagInputs',$tempRoot,'-DryRunFromCache','-DryRunDataPath',$fixturePath) + $scenario.Args
        $output = (& $powershell @arguments 2>&1 | Out-String)
        if ($LASTEXITCODE -ne 0) { throw "Evaluator dry-run '$($scenario.Name)' failed: $output" }
        Assert-True ($output.Contains('missing_chunks=0 missing_queries=0 estimated_api_calls=0')) "Forecast was not cache-only for $($scenario.Name)."
        if ($scenario.Name.StartsWith('lexical')) { Assert-True ($output.Contains('Recall@5=') -and $output.Contains('MRR@10=')) "Lexical metrics missing for $($scenario.Name)." }
        else {
            Assert-True ($output.Contains('DEV tuned hybrid') -and $output.Contains('AUTHOR_WRITTEN')) "Hybrid metric table missing for $($scenario.Name)."
            $selection = Get-Content -LiteralPath $selectionPath -Raw | ConvertFrom-Json
            Assert-True ($selection.fixtureVersion -eq 'DRY_RUN_FIXTURE' -and $selection.developmentCount -eq 3) "Selection file was not written for $($scenario.Name)."
        }
        if ($scenario.Name.EndsWith('with-timing')) { Assert-True ($output -match 'RankTimingEnabled=True;RankSamples=[1-9]') "Rank timing did not record samples for $($scenario.Name)." }
        else { Assert-True ($output -match 'RankTimingEnabled=False;RankSamples=0') "Rank timing unexpectedly ran for $($scenario.Name)." }
    }

    Write-Output 'EvaluatorDryRunE2E=PASS;PowerShell=5.1;Scenarios=4;Modes=lexical,hybrid;RankTiming=on,off;ExternalCalls=0;Database=0;SelectionWritten=TRUE'
}
finally {
    if (Test-Path -LiteralPath $tempRoot) { Remove-Item -LiteralPath $tempRoot -Recurse -Force }
}
