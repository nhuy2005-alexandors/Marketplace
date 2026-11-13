$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
Add-Type -Path (Join-Path $repoRoot 'src/ECommerce.Infrastructure/AI/RetrievalScoring.cs')

function Assert-True([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }

$chunks = New-Object 'System.Collections.Generic.List[ECommerce.Infrastructure.AI.RetrievalScoringChunk]'
$similarities = @(1.0, 0.80, 0.65, 0.58, 0.45, 0.30)
for ($id = 1; $id -le $similarities.Count; $id++) {
    $name = if ($id -eq 1) { 'Red Speaker' } else { "Parity Item $id" }
    $summary = "$name. SKU: DEMO-V2-$id. Brand: Unknown. Category: Parity category."
    $description = "Synthetic parity description $id"
    $similarity = [single]$similarities[$id - 1]
    $orthogonal = [single][Math]::Sqrt(1 - $similarity * $similarity)
    foreach ($record in @(@{Type='summary';Content=$summary},@{Type='description';Content=$description})) {
        $vector = if ($id -eq 1) { [single[]]@(1,0) } else { [single[]]@($similarity,$orthogonal) }
        $chunks.Add([ECommerce.Infrastructure.AI.RetrievalScoringChunk]@{
            ProductId=$id; Type=$record.Type; Content=$record.Content; ContentHash="HASH-$id-$($record.Type)"; Vector=$vector
        })
    }
}

$queryVector = [single[]]@(1,0)
$selected = [ECommerce.Infrastructure.AI.RetrievalScoring]::RankForEvaluation('red speaker',$queryVector,$chunks.ToArray(),$true,0.59,0.25,5)
$rollback = [ECommerce.Infrastructure.AI.RetrievalScoring]::RankForEvaluation('red speaker',$queryVector,$chunks.ToArray(),$true,0.38,0.5,5)
$lexical = [ECommerce.Infrastructure.AI.RetrievalScoring]::RankForEvaluation('red speaker',[single[]]@(),$chunks.ToArray(),$false,0.59,0.25,5)

Assert-True ($selected.Count -eq 3 -and $selected[0].ProductId -eq 1 -and $selected[1].ProductId -eq 2 -and $selected[2].ProductId -eq 3) 'Selected hybrid top-5/threshold ordering differs from expected runtime scoring.'
Assert-True ($rollback.Count -eq 5 -and $rollback[0].ProductId -eq 1 -and $rollback[1].ProductId -eq 2) 'Rollback hybrid ranking differs from expected runtime scoring.'
Assert-True ($lexical.Count -eq 1 -and $lexical[0].ProductId -eq 1) 'Lexical-only ranking differs from expected runtime scoring.'
Assert-True ([Math]::Abs($selected[1].Score - 0.20) -lt 0.00001) 'Combined score differs from the shared scoring formula.'

Write-Output 'RetrievalScoringParity=PASS;PowerShell=5.1;SharedSource=RetrievalScoring.cs;Cases=lexical,hybrid,threshold-selected,rollback;TopK=5;Network=0;Database=0'
