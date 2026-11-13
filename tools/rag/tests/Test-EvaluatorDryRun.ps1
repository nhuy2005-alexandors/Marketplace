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
    $authorK05 = @(
        @{ id='author-k05-1'; type='paraphrase'; query='portable visual machine'; expected_product_ids=@(1) },
        @{ id='author-k05-2'; type='paraphrase'; query='untethered play controls'; expected_product_ids=@(2) }
    )
    $noEvidence = @(
        @{id='noe-test-1';query='qwerty mystery phrase';expected_product_ids=@();label_predicate='nonsense'},
        @{id='noe-test-2';query='red apple';expected_product_ids=@();label_predicate='random-overlap'}
    )
    $queryEntries = foreach ($row in @($development) + @($authors) + @($authorK05) + @($noEvidence)) {
        $vector = switch ($row.id) {
            'dev-1' { @(1.0,0.0) }
            'dev-2' { @(0.0,1.0) }
            'dev-3' { @(0.0,0.0) }
            'author-1' { @(1.0,1.0) }
            'author-2' { @(1.0,0.0) }
            'author-k05-1' { @(1.0,0.0) }
            'author-k05-2' { @(0.0,1.0) }
            'noe-test-1' { @(0.0,0.0) }
            'noe-test-2' { @(1.0,0.0) }
        }
        $key = Get-QueryVectorCacheKey $row.query $model $dimensions
        @{ cacheKey=$key; contentHash=($key -split ':')[0]; modelName=$model; dimensions=$dimensions; vector=$vector }
    }
    Write-AtomicCacheFile -Path $queryCachePath -Content (ConvertTo-Json -InputObject @($queryEntries) -Depth 5)
    $fixture = @{
        modelName=$model; dimensions=$dimensions; catalogCachePath=$catalogCachePath; queryCachePath=$queryCachePath
        selectionPath=$selectionPath; chunks=$chunks; development=$development; authorFixtures=$authors; authorK05Fixtures=$authorK05; noEvidenceFixtures=$noEvidence
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

    $selection = @{ fixtureVersion='DRY_RUN_FIXTURE'; developmentCount=3; threshold=0.59; semanticWeight=0.25 }
    Write-AtomicCacheFile -Path $selectionPath -Content (ConvertTo-Json -InputObject $selection -Depth 4)
    $arguments = @('-NoProfile','-ExecutionPolicy','Bypass','-File',$evaluatorPath,'-RepoRoot',$repoRoot,'-RagInputs',$tempRoot,'-DryRunFromCache','-DryRunDataPath',$fixturePath,'-AuthorK05Only')
    $output = (& $powershell @arguments 2>&1 | Out-String)
    if ($LASTEXITCODE -ne 0) { throw "Evaluator K-05-only dry-run failed: $output" }
    Assert-True ($output.Contains('AUTHOR_K05 lexical') -and $output.Contains('UsedForTuning=FALSE') -and $output.Contains('AuthorK05Only=TRUE')) 'K-05-only report did not produce side-by-side lexical/hybrid results.'

    $diagnosticKey = Get-QueryVectorCacheKey $authorK05[0].query $model $dimensions
    $oneCachedVector = @($queryEntries | Where-Object cacheKey -eq $diagnosticKey)
    Write-AtomicCacheFile -Path $queryCachePath -Content (ConvertTo-Json -InputObject $oneCachedVector -Depth 5)
    $arguments = @('-NoProfile','-ExecutionPolicy','Bypass','-File',$evaluatorPath,'-RepoRoot',$repoRoot,'-RagInputs',$tempRoot,'-DryRunFromCache','-DryRunDataPath',$fixturePath,'-AuthorK05Only','-Diagnostics')
    $output = (& $powershell @arguments 2>&1 | Out-String)
    if ($LASTEXITCODE -ne 0) { throw "Evaluator K-05 diagnostics dry-run failed: $output" }
    Assert-True ($output.Contains('K05_DIAG id=author-k05-1') -and $output.Contains('K05_DIAG id=author-k05-2')) 'K-05 diagnostic rows are missing.'
    Assert-True ($output.Contains('hybrid_results=n/a') -and $output.Contains('MissingQueryVectors=1') -and $output.Contains('Network=0;Database=0')) 'K-05 cache-miss diagnostics were not marked safely.'

    Write-AtomicCacheFile -Path $queryCachePath -Content (ConvertTo-Json -InputObject @($queryEntries) -Depth 5)
    $arguments = @('-NoProfile','-ExecutionPolicy','Bypass','-File',$evaluatorPath,'-RepoRoot',$repoRoot,'-RagInputs',$tempRoot,'-DryRunFromCache','-DryRunDataPath',$fixturePath,'-NoEvidenceOnly')
    $output = (& $powershell @arguments 2>&1 | Out-String)
    if ($LASTEXITCODE -ne 0) { throw "No-evidence-only dry-run failed: $output" }
    Assert-True ($output.Contains('NO_EVIDENCE_FORECAST queries=2 missing_vectors=0') -and $output.Contains('NO_EVIDENCE_METRICS empty=1/2 empty_rate=50.00%')) 'No-evidence cache-only metric output is incorrect.'

    $arguments = @('-NoProfile','-ExecutionPolicy','Bypass','-File',$evaluatorPath,'-RepoRoot',$repoRoot,'-RagInputs',$tempRoot,'-DryRunFromCache','-DryRunDataPath',$fixturePath,'-ExplainQuery','qwerty mystery phrase')
    $output = (& $powershell @arguments 2>&1 | Out-String)
    if ($LASTEXITCODE -ne 0) { throw "Explain-query dry-run failed: $output" }
    Assert-True ($output.Contains('EXPLAIN_SUMMARY') -and $output.Contains('ranking unchanged')) 'Explain-query diagnosis did not report its hypothesis.'

    $modeCases=@(
        @{Name='ExplainQuery';Args=@('-ExplainQuery','red apple');Expected='EXPLAIN_SUMMARY'},
        @{Name='NoEvidenceOnly';Args=@('-NoEvidenceOnly');Expected='NO_EVIDENCE_METRICS'},
        @{Name='AuthorK05Only';Args=@('-AuthorK05Only');Expected='AUTHOR_K05 lexical'},
        @{Name='DryRunFromCache';Args=@('-DevelopmentOnly');Expected='SelectedDevThreshold'},
        @{Name='CacheForecastOnly';Args=@('-CacheForecastOnly');Expected='CacheForecastOnly mode=synthetic'}
    )
    foreach($modeCase in $modeCases){
        foreach($cacheState in @('full','partial','empty')){
            $targetQuery=if($modeCase.Name -eq 'ExplainQuery'){'red apple'}elseif($modeCase.Name -eq 'NoEvidenceOnly'){$noEvidence[0].query}elseif($modeCase.Name -eq 'AuthorK05Only'){$authorK05[0].query}else{$development[0].query}
            $targetKey=Get-QueryVectorCacheKey $targetQuery $model $dimensions
            $stateEntries=@($queryEntries)
            if($cacheState -eq 'partial'){$stateEntries=@($queryEntries|Where-Object cacheKey -ne $targetKey)}elseif($cacheState -eq 'empty'){$stateEntries=@()}
            Write-AtomicCacheFile -Path $queryCachePath -Content (ConvertTo-Json -InputObject @($stateEntries) -Depth 5)
            $catalogState=@($catalogEntries)
            if($modeCase.Name -eq 'CacheForecastOnly'){
                if($cacheState -eq 'partial'){$catalogState=@($catalogEntries|Select-Object -Skip 1)}elseif($cacheState -eq 'empty'){$catalogState=@()}
                Write-AtomicCacheFile -Path $catalogCachePath -Content (ConvertTo-Json -InputObject @($catalogState) -Depth 5)
            }else{Write-AtomicCacheFile -Path $catalogCachePath -Content (ConvertTo-Json -InputObject @($catalogEntries) -Depth 5)}
            $queryBefore=[IO.File]::ReadAllText($queryCachePath);$catalogBefore=[IO.File]::ReadAllText($catalogCachePath)
            $common=@('-NoProfile','-ExecutionPolicy','Bypass','-File',$evaluatorPath,'-RepoRoot',$repoRoot,'-RagInputs',$tempRoot,'-DryRunFromCache','-DryRunDataPath',$fixturePath)+$modeCase.Args
            $withoutArgs=$common
            $priorPreference=$ErrorActionPreference;$ErrorActionPreference='Continue';$withoutOutput=(& $powershell @withoutArgs 2>&1|Out-String);$withoutExit=$LASTEXITCODE;$ErrorActionPreference=$priorPreference
            if($withoutExit -ne 0){throw "$($modeCase.Name) $cacheState without permission returned $($withoutExit): $withoutOutput"}
            if($cacheState -eq 'full'){
                if(-not $withoutOutput.Contains($modeCase.Expected)){throw "$($modeCase.Name) full-cache output was missing expected result: $withoutOutput"}
            }elseif(-not $withoutOutput.Contains('CACHE_MISS_FORECAST') -and $modeCase.Name -ne 'CacheForecastOnly'){
                throw "$($modeCase.Name) $cacheState did not print a cache-miss forecast: $withoutOutput"
            }
            if($cacheState -ne 'full' -and $modeCase.Name -eq 'CacheForecastOnly' -and (-not $withoutOutput.Contains('missing_chunks=') -or -not $withoutOutput.Contains('missing_queries='))){throw "CacheForecastOnly $cacheState did not report missing cache counts: $withoutOutput"}
            if($cacheState -ne 'full' -and $modeCase.Name -ne 'CacheForecastOnly' -and (-not $withoutOutput.Contains('missing_vectors=') -or -not $withoutOutput.Contains('estimated_batches=') -or -not $withoutOutput.Contains('estimated_api_calls='))){throw "$($modeCase.Name) omitted detailed forecast counts: $withoutOutput"}
            if([IO.File]::ReadAllText($queryCachePath) -cne $queryBefore){throw "${($modeCase.Name)} wrote query cache without AllowEmbeddingCall."}
            if($modeCase.Name -ne 'CacheForecastOnly' -and $cacheState -ne 'full'){
                $withArgs=$common+@('-AllowEmbeddingCall','-UseFakeEmbedder')
                $priorPreference=$ErrorActionPreference;$ErrorActionPreference='Continue';$withOutput=(& $powershell @withArgs 2>&1|Out-String);$withExit=$LASTEXITCODE;$ErrorActionPreference=$priorPreference
                if($withExit -ne 0 -or -not $withOutput.Contains($modeCase.Expected)){throw "$($modeCase.Name) $cacheState fake embed run failed ($withExit): $withOutput"}
                if([IO.File]::ReadAllText($queryCachePath) -ceq $queryBefore){throw "$($modeCase.Name) did not cache fake embeddings after explicit permission."}
            }elseif($modeCase.Name -eq 'CacheForecastOnly'){
                $withArgs=$common+@('-AllowEmbeddingCall','-UseFakeEmbedder')
                $withOutput=(& $powershell @withArgs 2>&1|Out-String);$withExit=$LASTEXITCODE
                if($withExit -ne 0 -or -not $withOutput.Contains($modeCase.Expected)){throw "CacheForecastOnly $cacheState with permission failed: $withOutput"}
                if([IO.File]::ReadAllText($queryCachePath) -cne $queryBefore -or [IO.File]::ReadAllText($catalogCachePath) -cne $catalogBefore){throw 'CacheForecastOnly wrote cache despite its read-only contract.'}
            }
            if($cacheState -eq 'full' -and $modeCase.Name -ne 'CacheForecastOnly'){
                $withArgs=$common+@('-AllowEmbeddingCall','-UseFakeEmbedder')
                $priorPreference=$ErrorActionPreference;$ErrorActionPreference='Continue';$withOutput=(& $powershell @withArgs 2>&1|Out-String);$withExit=$LASTEXITCODE;$ErrorActionPreference=$priorPreference
                if($withExit -ne 0 -or -not $withOutput.Contains($modeCase.Expected)){throw "$($modeCase.Name) full-cache allowed run failed: $withOutput"}
                if([IO.File]::ReadAllText($queryCachePath) -cne $queryBefore){throw "$($modeCase.Name) called embedder despite a full cache."}
            }
            if($modeCase.Name -eq 'CacheForecastOnly'){Write-AtomicCacheFile -Path $catalogCachePath -Content (ConvertTo-Json -InputObject @($catalogEntries) -Depth 5)}
        }
    }

    Write-Output 'EvaluatorDryRunE2E=PASS;PowerShell=5.1;Modes=lexical,hybrid,author-k05-only,diagnostics,no-evidence,explain,cache-forecast;CacheStates=full,partial,empty;FakeEmbedder=allowed-only;ExternalCalls=0;Database=0'
}
finally {
    if (Test-Path -LiteralPath $tempRoot) { Remove-Item -LiteralPath $tempRoot -Recurse -Force }
}
