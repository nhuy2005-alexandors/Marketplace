param([string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)
$ErrorActionPreference='Stop'
Add-Type -Path (Join-Path $RepoRoot 'src/ECommerce.Infrastructure/AI/RetrievalScoring.cs')
. (Join-Path $RepoRoot 'tools/rag/DeduplicatedEvaluation.ps1')

function Assert-True([bool]$Value,[string]$Message){if(-not $Value){throw "ASSERTION FAILED: $Message"}}
function Assert-Throws([scriptblock]$Action,[string]$Pattern){try{& $Action;throw 'Expected exception was not raised.'}catch{if($_.Exception.Message -notmatch $Pattern){throw}}}

$same=@(
    [pscustomobject]@{id='a';query='Máy Tính';expected_product_ids=@(1)},
    [pscustomobject]@{id='b';query='may tinh';expected_product_ids=@(1)},
    [pscustomobject]@{id='c';query='  MÁY   TÍNH ';expected_product_ids=@(1)}
)
$unique=Get-EvaluationUniqueRows $same 'test'
Assert-True ($unique.Count -eq 1 -and $unique[0].raw_row_count -eq 3) 'case, accents and whitespace dedupe by runtime Normalize'
$conflict=@($same[0],([pscustomobject]@{id='different-label';query='may tinh';expected_product_ids=@(2)}))
Assert-Throws { Get-EvaluationUniqueRows $conflict 'test' } 'Conflicting Expected Product IDs'
Assert-Throws { Assert-NoEvaluationLeakage @($same[0]) @($same[1]) } 'Evaluation leakage'
Assert-True ((Assert-NoEvaluationLeakage @($same[0]) @([pscustomobject]@{id='other';query='may tinh xach tay'})) -eq 0) 'non-identical normalized queries do not leak'
$sourcePath=Join-Path (Split-Path $RepoRoot -Parent) 'Marketplace-khang-ai_rag/RAG_inputs/RAG_EVALUATION_QUERIES_V2.jsonl'
$sourceRows=@(Get-Content $sourcePath|ForEach-Object{$_|ConvertFrom-Json})
$sourceDev=Get-EvaluationUniqueRows @($sourceRows|Where-Object split -eq 'development') 'development'
$sourceHold=Get-EvaluationUniqueRows @($sourceRows|Where-Object split -eq 'holdout') 'holdout'
$sourceAll=Get-EvaluationUniqueRows $sourceRows 'all'
Assert-True ($sourceRows.Count -eq 120 -and $sourceAll.Count -eq 109 -and $sourceDev.Count -eq 88 -and $sourceHold.Count -eq 21) ("V2 raw/unique split counts actual=$($sourceRows.Count)/$($sourceAll.Count) dev=$($sourceDev.Count) holdout=$($sourceHold.Count)")
Assert-True ((Assert-NoEvaluationLeakage $sourceDev $sourceHold) -eq 0) 'real V2 development/holdout leakage check'
$sourceDuplicates=@(($sourceRows|Group-Object{[ECommerce.Infrastructure.AI.RetrievalScoring]::Normalize([string]$_.query)})|Where-Object Count -gt 1)
Assert-True ($sourceDuplicates.Count -eq 11 -and (@($sourceDuplicates|Where-Object{@($_.Group.type|Sort-Object -Unique).Count -gt 1}).Count -eq 0)) 'V2 duplicates confined within type labels'

$chunks=@([ECommerce.Infrastructure.AI.RetrievalScoringChunk]@{ProductId=1;Type='summary';Content='Alpha laptop';Vector=[single[]]@(1,0)})
$vectors=@{a=[single[]]@(1,0);b=[single[]]@(1,0)}
$metricRows=@([pscustomobject]@{id='a';query='Alpha laptop';expected_product_ids=@(1);raw_row_count=2},[pscustomobject]@{id='b';query='missing';expected_product_ids=@();raw_row_count=1})
$metric=Get-DeduplicatedMetrics $metricRows $vectors $chunks $true 0.59 0.25
Assert-True ($metric.RawRows -eq 3 -and $metric.UniqueQueries -eq 2) 'raw and unique counts remain separate'
Assert-True ($metric.RecallAt5 -eq 1 -and $metric.MRRAt10 -eq 1 -and $metric.AnswerableCoverage -eq 1) 'answerable recall, MRR and final visible coverage'
Assert-True ($metric.NoResultFalsePositiveRate -eq 1) 'no-result false-positive rate uses final shown top four'
Assert-True ((Get-OwnerReviewedRows @([pscustomobject]@{owner_confirmed=$false})).Count -eq 0) 'owner mode empty dataset'
$reviewed=@(Get-OwnerReviewedRows @([pscustomobject]@{owner_confirmed=$false},[pscustomobject]@{owner_confirmed=$true;id='reviewed'}))
Assert-True ($reviewed.Count -eq 1 -and $reviewed[0].id -eq 'reviewed') 'owner mode only includes confirmed rows'
$evalPath=Join-Path $RepoRoot 'tools/rag/evaluate-v2.ps1'
$dryData=Join-Path $RepoRoot 'obj/rag-dryrun-data.json'
$emptyOutput=& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $evalPath -OwnerReviewedOnly -DryRunFromCache -DryRunDataPath $dryData -RepoRoot $RepoRoot 2>&1 | Out-String
Assert-True ($LASTEXITCODE -eq 0 -and $emptyOutput.Contains('chưa có dữ liệu')) 'OwnerReviewedOnly empty mode returns no-data and exits zero'
$inputPath=Join-Path (Split-Path $RepoRoot -Parent) 'Marketplace-khang-ai_rag/RAG_inputs/RAG_EVALUATION_QUERIES_V2.jsonl'
$known=@(Get-Content $inputPath|ForEach-Object{$_|ConvertFrom-Json}|Where-Object split -eq 'development'|Select-Object -First 1)[0]
$tempOwner=Join-Path ([IO.Path]::GetTempPath()) ('owner-reviewed-test-'+[Guid]::NewGuid().ToString('N')+'.jsonl')
try{
    $fakeOwner=[pscustomobject]@{id='OWNER-TEST-001';query=$known.query;scenario='test';expected_product_ids=@($known.expected_product_ids);owner_confirmed=$true;confirmed_by='test';confirmed_at='2026-11-14'}
    [IO.File]::WriteAllText($tempOwner,(ConvertTo-Json -InputObject $fakeOwner -Compress)+[Environment]::NewLine,[Text.UTF8Encoding]::new($false))
    $fakeOutput=& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $evalPath -OwnerReviewedOnly -DryRunFromCache -DryRunDataPath $dryData -RepoRoot $RepoRoot -OwnerReviewedQueriesPath $tempOwner 2>&1 | Out-String
    Assert-True ($LASTEXITCODE -eq 0 -and $fakeOutput.Contains('unique_queries=1') -and $fakeOutput.Contains('separated=TRUE')) 'OwnerReviewedOnly fake confirmed row is evaluated independently'
}finally{if(Test-Path -LiteralPath $tempOwner){Remove-Item -LiteralPath $tempOwner -Force}}
Write-Output 'Test-DeduplicatedEvaluation=PASS;dedupe=3;metric-formulas=4;owner-reviewed=4;network=0;database=0'
