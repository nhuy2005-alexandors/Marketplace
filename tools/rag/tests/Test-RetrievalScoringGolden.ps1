param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path,
    [string]$RagInputs = (Join-Path (Split-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path -Parent) 'Marketplace-khang-ai_rag\RAG_inputs'),
    [switch]$PrintFingerprint
)
$ErrorActionPreference='Stop'
. (Join-Path $RepoRoot 'tools/rag/EvaluationResilience.ps1')
$source=Join-Path $RepoRoot 'src/ECommerce.Infrastructure/AI/RetrievalScoring.cs'
if(-not ('ECommerce.Infrastructure.AI.RetrievalScoring' -as [type])){Add-Type -Path $source}
$dry=Get-Content -LiteralPath (Join-Path $RepoRoot 'obj/rag-dryrun-data.json') -Raw|ConvertFrom-Json
$docEntries=Get-Content -LiteralPath $dry.catalogCachePath -Raw|ConvertFrom-Json
$docCache=@{};foreach($entry in $docEntries){$key="$($entry.contentHash):$($entry.modelName):$($entry.dimensions)";if($entry.cacheKey -eq $key -and @($entry.vector).Count -eq [int]$entry.dimensions){$docCache[$key]=$entry}}
$chunks=New-Object 'System.Collections.Generic.List[ECommerce.Infrastructure.AI.RetrievalScoringChunk]'
foreach($item in $dry.chunks){$key="$($item.hash):$($dry.modelName):$($dry.dimensions)";if(-not $docCache.ContainsKey($key)){throw "Golden scoring requires cached catalog vector for $($item.hash)."};$chunks.Add([ECommerce.Infrastructure.AI.RetrievalScoringChunk]@{ProductId=[int]$item.productId;Type=[string]$item.type;Content=[string]$item.content;ContentHash=[string]$item.hash;Vector=[single[]]$docCache[$key].vector})}
$queryCache=Read-QueryVectorCache -Path $dry.queryCachePath -ModelName $dry.modelName -Dimensions ([int]$dry.dimensions)
$evaluationRows=@(Get-Content -LiteralPath (Join-Path $RagInputs 'RAG_EVALUATION_QUERIES_V2.jsonl')|ForEach-Object{$_|ConvertFrom-Json})
$dev=@($evaluationRows|Where-Object split -eq 'development')
$holdout=@($evaluationRows|Where-Object split -eq 'holdout')
$k05=@(Get-Content -LiteralPath (Join-Path $RepoRoot 'docs/rag-evaluation/RAG_AUTHOR_WRITTEN_K05_PARAPHRASES.jsonl')|ForEach-Object{$_|ConvertFrom-Json})
$noEvidence=@(Get-Content -LiteralPath (Join-Path $RepoRoot 'docs/rag-evaluation/RAG_AUTHOR_WRITTEN_NO_EVIDENCE.jsonl')|ForEach-Object{$_|ConvertFrom-Json})
$sets=@([pscustomobject]@{Name='dev';Rows=$dev},[pscustomobject]@{Name='holdout';Rows=$holdout},[pscustomobject]@{Name='k05';Rows=$k05},[pscustomobject]@{Name='noevidence';Rows=$noEvidence})
$builder=[Text.StringBuilder]::new();$queryCount=0
foreach($set in $sets){foreach($row in $set.Rows){
    $query=[string]$row.query;$key=Get-QueryVectorCacheKey $query $dry.modelName ([int]$dry.dimensions)
    if(-not $queryCache.ContainsKey($key)){throw "Golden scoring requires cached query vector ($($set.Name):$($row.id)); no embedding was attempted."}
    $vector=[single[]]$queryCache[$key].vector;$sku='';$isSku=[ECommerce.Infrastructure.AI.RetrievalScoring]::TryResolveSkuIntent($query,$chunks.ToArray(),[ref]$sku)
    [void]$builder.AppendLine(("Q|{0}|{1}" -f $set.Name,[string]$row.id))
    $ranked=[ECommerce.Infrastructure.AI.RetrievalScoring]::RankForEvaluation($query,$vector,$chunks.ToArray(),$true,0.59,0.25,8)
    foreach($hit in $ranked){[void]$builder.AppendLine(("R|{0}|{1}|{2}|{3}|{4}" -f $hit.ProductId,$hit.Score.ToString('R',[Globalization.CultureInfo]::InvariantCulture),$hit.LexicalScore.ToString('R',[Globalization.CultureInfo]::InvariantCulture),$hit.SemanticScore.ToString('R',[Globalization.CultureInfo]::InvariantCulture),$isSku))}
    foreach($group in ($chunks.ToArray()|Group-Object ProductId|Sort-Object {[int]$_.Name})){$productChunks=[ECommerce.Infrastructure.AI.RetrievalScoringChunk[]]$group.Group;$score=[ECommerce.Infrastructure.AI.RetrievalScoring]::ScoreProduct($query,$vector,$productChunks,$true,0.59,0.25,$isSku,$sku);$indexes=(@($score.IncludedChunkIndexes)-join ',');[void]$builder.AppendLine(("S|{0}|{1}|{2}|{3}|{4}|{5}|{6}" -f $score.ProductId,$score.Included,$score.LexicalScore.ToString('R',[Globalization.CultureInfo]::InvariantCulture),$score.SemanticScore.ToString('R',[Globalization.CultureInfo]::InvariantCulture),$score.CombinedScore.ToString('R',[Globalization.CultureInfo]::InvariantCulture),$indexes,$isSku))}
    $queryCount++
}}
$sha=[Security.Cryptography.SHA256]::Create();try{$hash=[BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($builder.ToString()))).Replace('-','')}finally{$sha.Dispose()}
if($PrintFingerprint){Write-Output ("RetrievalScoringGolden fingerprint={0} queries={1} chunks={2} dimensions={3} threshold=0.59 weight=0.25" -f $hash,$queryCount,$chunks.Count,$dry.dimensions);exit 0}
$expected='96E0DADE54F01CC1713C55AE185B29961CC8C5E169C5425AE59021B9A83FB634'
if($hash -ne $expected){throw "Retrieval ranking golden changed. Expected=$expected Actual=$hash"}
Write-Output ("RetrievalScoringGolden=PASS;Fingerprint={0};Queries={1};Development=96;HoldoutRankingFingerprintOnly=24;K05=30;NoEvidence=25;HoldoutMetrics=NOT_RUN;Network=0;Database=0" -f $hash,$queryCount)
