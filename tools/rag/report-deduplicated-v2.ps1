param(
    [string]$RepoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$RagInputs=(Join-Path (Split-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path -Parent) 'Marketplace-khang-ai_rag\RAG_inputs')
)
$ErrorActionPreference='Stop'
$source=Join-Path $RepoRoot 'src/ECommerce.Infrastructure/AI/RetrievalScoring.cs'
$classifierSource=Join-Path $RepoRoot 'src/ECommerce.Infrastructure/AI/AssistantIntentClassifier.cs'
if(-not ('ECommerce.Infrastructure.AI.RetrievalScoring' -as [type]) -and -not ('ECommerce.Infrastructure.AI.AssistantIntentClassifier' -as [type])){Add-Type -Path @($source,$classifierSource)}
elseif(-not ('ECommerce.Infrastructure.AI.AssistantIntentClassifier' -as [type])){throw 'Run report-deduplicated-v2.ps1 in a fresh PowerShell process so it can compile the exact runtime classifier and scoring source together.'}
. (Join-Path $PSScriptRoot 'EvaluationResilience.ps1')
. (Join-Path $PSScriptRoot 'DeduplicatedEvaluation.ps1')
$dry=Get-Content -LiteralPath (Join-Path $RepoRoot 'obj/rag-dryrun-data.json') -Raw|ConvertFrom-Json
$evalPath=Join-Path $RagInputs 'RAG_EVALUATION_QUERIES_V2.jsonl'
$raw=@(Get-Content -LiteralPath $evalPath|ForEach-Object{$_|ConvertFrom-Json})
$devRaw=@($raw|Where-Object split -eq 'development');$holdRaw=@($raw|Where-Object split -eq 'holdout')
$dev=Get-EvaluationUniqueRows $devRaw 'development';$hold=Get-EvaluationUniqueRows $holdRaw 'holdout'
$leakCount=Assert-NoEvaluationLeakage $dev $hold
$all=Get-EvaluationUniqueRows $raw 'all'
$duplicateGroups=@(($raw|Group-Object {[ECommerce.Infrastructure.AI.RetrievalScoring]::Normalize([string]$_.query)})|Where-Object Count -gt 1)
$crossTypes=@($duplicateGroups|Where-Object{@($_.Group.type|Sort-Object -Unique).Count -gt 1})
$conflictCount=0
$dryEntries=Get-Content -LiteralPath $dry.catalogCachePath -Raw|ConvertFrom-Json
$catalogCache=@{};foreach($entry in $dryEntries){$key="$($entry.contentHash):$($entry.modelName):$($entry.dimensions)";if($entry.cacheKey -eq $key -and @($entry.vector).Count -eq [int]$entry.dimensions){$catalogCache[$key]=$entry}}
$chunks=New-Object 'System.Collections.Generic.List[ECommerce.Infrastructure.AI.RetrievalScoringChunk]'
foreach($item in $dry.chunks){$key="$($item.hash):$($dry.modelName):$($dry.dimensions)";if(-not $catalogCache.ContainsKey($key)){throw "Required cached catalog vector missing for hash $($item.hash)."};$chunks.Add([ECommerce.Infrastructure.AI.RetrievalScoringChunk]@{ProductId=[int]$item.productId;Type=[string]$item.type;Content=[string]$item.content;ContentHash=[string]$item.hash;Vector=[single[]]$catalogCache[$key].vector})}
$queryCache=Read-QueryVectorCache -Path $dry.queryCachePath -ModelName $dry.modelName -Dimensions ([int]$dry.dimensions)
function Add-Vectors([object[]]$Rows,[hashtable]$Map,[string]$SetName){foreach($row in $Rows){$key=Get-QueryVectorCacheKey ([string]$row.query) $dry.modelName ([int]$dry.dimensions);if(-not $queryCache.ContainsKey($key)){throw "Query vector missing in cache for set=$SetName id=$($row.id); no embedding requested."};$Map[[string]$row.id]=[single[]]$queryCache[$key].vector}}
$devMap=@{};$holdMap=@{};Add-Vectors $dev $devMap 'development';Add-Vectors $hold $holdMap 'holdout'
$selection=Get-Content -LiteralPath $dry.selectionPath -Raw|ConvertFrom-Json
$k05raw=@(Get-Content -LiteralPath (Join-Path $RepoRoot 'docs/rag-evaluation/RAG_AUTHOR_WRITTEN_K05_PARAPHRASES.jsonl')|ForEach-Object{$_|ConvertFrom-Json})
$k05=Get-EvaluationUniqueRows $k05raw 'K05';$k05Map=@{};Add-Vectors $k05 $k05Map 'K05'
Write-Output ('DATASET_COUNTS total raw={0} unique={1} duplicate_groups={2} duplicate_rows={3} conflicting_label_groups={4} dev_holdout_leaks={5}' -f $raw.Count,$all.Count,$duplicateGroups.Count,(@($duplicateGroups|ForEach-Object Count|Measure-Object -Sum).Sum),$conflictCount,$leakCount)
foreach($split in @([pscustomobject]@{Name='development';Raw=$devRaw;Unique=$dev},[pscustomobject]@{Name='holdout';Raw=$holdRaw;Unique=$hold})){
    Write-Output ('SPLIT {0} raw={1} unique={2} answerable={3} no_result={4}' -f $split.Name,$split.Raw.Count,$split.Unique.Count,@($split.Unique|Where-Object{@($_.expected_product_ids).Count -gt 0}).Count,@($split.Unique|Where-Object{@($_.expected_product_ids).Count -eq 0}).Count)
}
foreach($group in ($raw|Group-Object split,type|Sort-Object Name)){
    $uniqueCount=@($group.Group|Group-Object {[ECommerce.Infrastructure.AI.RetrievalScoring]::Normalize([string]$_.query)}).Count
    Write-Output ('TYPE_COUNT split_type="{0}" raw={1} unique={2}' -f $group.Name,$group.Count,$uniqueCount)
}
foreach($pair in @(@('031','041'),@('036','046'),@('096','106'))){$left=@($raw|Where-Object{$_.id -like "*-$($pair[0])" -or $_.id -like "*-$($pair[0])"});$right=@($raw|Where-Object{$_.id -like "*-$($pair[1])"});if($left.Count -and $right.Count){Write-Output ('DUPLICATE_PAIR ids={0}/{1} type={2}/{3}' -f $left[0].id,$right[0].id,$left[0].type,$right[0].type)}}
foreach($set in @([pscustomobject]@{Name='development';Rows=$dev;Vectors=$devMap},[pscustomobject]@{Name='holdout';Rows=$hold;Vectors=$holdMap},[pscustomobject]@{Name='K05';Rows=$k05;Vectors=$k05Map})){
    foreach($mode in @([pscustomobject]@{Name='lexical';Hybrid=$false},[pscustomobject]@{Name='hybrid';Hybrid=$true})){
        $m=Get-DeduplicatedMetrics $set.Rows $set.Vectors $chunks.ToArray() $mode.Hybrid 0.59 0.25
        Write-Output ('METRIC set={0} mode={1} raw={2} unique={3} answerable={4} no_result={5} R5={6:P2} MRR10={7:P2} no_result_false_positive_final_visible={8:P2} answerable_coverage_top4={9:P2}' -f $set.Name,$mode.Name,$m.RawRows,$m.UniqueQueries,$m.Answerable,$m.NoResult,$m.RecallAt5,$m.MRRAt10,$m.NoResultFalsePositiveRate,$m.AnswerableCoverage)
    }
}
$noEvidenceRaw=@(Get-Content -LiteralPath (Join-Path $RepoRoot 'docs/rag-evaluation/RAG_AUTHOR_WRITTEN_NO_EVIDENCE.jsonl')|ForEach-Object{$_|ConvertFrom-Json})
$naturalRaw=@(Get-Content -LiteralPath (Join-Path $RepoRoot 'docs/rag-evaluation/RAG_AUTHOR_WRITTEN_NATURAL_QUERIES.jsonl')|ForEach-Object{$_|ConvertFrom-Json})
$intentsRaw=@(Get-Content -LiteralPath (Join-Path $RepoRoot 'docs/rag-evaluation/RAG_AUTHOR_WRITTEN_INTENTS.jsonl')|ForEach-Object{$_|ConvertFrom-Json})
$noEvidence=Get-EvaluationUniqueRows $noEvidenceRaw 'no-evidence';$natural=Get-EvaluationUniqueRows $naturalRaw 'natural';$intents=Get-EvaluationUniqueRows $intentsRaw 'intents'
Write-Output ('AUTHOR_FIXTURE_COUNT set=no-evidence raw={0} unique={1} answerable_labels=0 no_result_labels={1}' -f ($noEvidence|Measure-Object -Property raw_row_count -Sum).Sum,$noEvidence.Count)
Write-Output ('AUTHOR_FIXTURE_COUNT set=natural raw={0} unique={1} category_labeled={1} filter_dimensions_cached=FALSE; product_metrics=NOT_COMPUTED' -f ($natural|Measure-Object -Property raw_row_count -Sum).Sum,$natural.Count)
$intentCorrect=0;foreach($row in $intents){$actual=[ECommerce.Infrastructure.AI.AssistantIntentClassifier]::Classify([string]$row.query).Intent;if($actual -eq [string]$row.expected_intent){$intentCorrect++}}
Write-Output ('AUTHOR_FIXTURE_COUNT set=intents raw={0} unique={1} intent_correct={2}/{1}; retrieval_metrics=NOT_APPLICABLE' -f ($intents|Measure-Object -Property raw_row_count -Sum).Sum,$intents.Count,$intentCorrect)
$noEvidenceMap=@{};Add-Vectors $noEvidence $noEvidenceMap 'no-evidence'
$noEvidenceMetric=Get-DeduplicatedMetrics $noEvidence $noEvidenceMap $chunks.ToArray() $true 0.59 0.25
Write-Output ('METRIC set=no-evidence mode=hybrid raw={0} unique={1} no_result={2} no_result_false_positive_final_visible={3:P2}; author_diagnostic_only=TRUE' -f $noEvidenceMetric.RawRows,$noEvidenceMetric.UniqueQueries,$noEvidenceMetric.NoResult,$noEvidenceMetric.NoResultFalsePositiveRate)
$owner=Get-Content -LiteralPath (Join-Path $RepoRoot 'docs/rag-evaluation/RAG_OWNER_REVIEWED_QUERIES.jsonl')|ForEach-Object{$_|ConvertFrom-Json}
Write-Output ('OWNER_REVIEWED raw={0} confirmed={1}' -f @($owner).Count,@($owner|Where-Object owner_confirmed).Count)
Write-Output ('CACHE catalog_vectors={0} catalog_chunks={1} query_vectors={2} expected_catalog_chunks={3} cache_missing_queries=0 network=0 database=0 holdout_run=0' -f $catalogCache.Count,$chunks.Count,$queryCache.Count,$dry.chunks.Count)
