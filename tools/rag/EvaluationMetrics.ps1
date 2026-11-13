function Get-Metrics($Rows, $Vectors, $Chunks, [bool]$Hybrid, [double]$Threshold, [double]$Weight,
    [bool]$TrackTiming = $false, [System.Collections.Generic.List[double]]$RankTimes = $null) {
    $relevant = @($Rows | Where-Object { @($_.expected_product_ids).Count -gt 0 })
    $empty = @($Rows | Where-Object { @($_.expected_product_ids).Count -eq 0 })
    $recall = 0.0; $recallCapped = 0.0; $ceiling = 0.0; $mrr = 0.0; $emptyCorrect = 0
    foreach ($row in $Rows) {
        $rankTimer = [Diagnostics.Stopwatch]::StartNew()
        $ranked = [ECommerce.Infrastructure.AI.RetrievalScoring]::RankForEvaluation(
            [string]$row.query, $Vectors[[string]$row.id], [ECommerce.Infrastructure.AI.RetrievalScoringChunk[]]$Chunks,
            $Hybrid, $Threshold, $Weight, 8)
        $rankTimer.Stop()
        if ($TrackTiming) {
            if ($null -eq $RankTimes) { throw 'Rank timing is enabled but no timing collector was provided.' }
            $RankTimes.Add($rankTimer.Elapsed.TotalMilliseconds)
        }
        $ids = @($ranked | Select-Object -First 10 | ForEach-Object ProductId)
        if (@($row.expected_product_ids).Count -gt 0) {
            $expected = @($row.expected_product_ids); $top5 = @($ids | Select-Object -First 5)
            $found = @($top5 | Where-Object { $expected -contains $_ }).Count
            $recall += $found / [double]$expected.Count
            $recallCapped += $found / [double][Math]::Min(5, $expected.Count)
            $ceiling += [Math]::Min(5, $expected.Count) / [double]$expected.Count
            $rank = 0
            for ($i=0; $i -lt $ids.Count; $i++) { if ($expected -contains $ids[$i]) { $rank = $i + 1; break } }
            if ($rank -gt 0) { $mrr += 1.0 / $rank }
        }
        elseif ($ranked.Count -eq 0) { $emptyCorrect++ }
    }
    [pscustomobject]@{
        RecallAt5 = if ($relevant.Count) { $recall / $relevant.Count } else { 0 }
        RecallAt5Capped = if ($relevant.Count) { $recallCapped / $relevant.Count } else { 0 }
        RecallAt5TheoreticalCeiling = if ($relevant.Count) { $ceiling / $relevant.Count } else { 0 }
        MRRAt10 = if ($relevant.Count) { $mrr / $relevant.Count } else { 0 }
        NoResultEmptyAccuracy = if ($empty.Count) { $emptyCorrect / [double]$empty.Count } else { 0 }
        RelevantQueries = $relevant.Count; NoResultQueries = $empty.Count
    }
}

function Get-LatencySummary($Values) {
    $sorted = @($Values | Sort-Object); $count = $sorted.Count
    if (-not $count) { return [pscustomobject]@{ AverageMs=0; P95Ms=0; Count=0 } }
    $p95Index = [Math]::Max(0, [int][Math]::Ceiling(0.95 * $count) - 1)
    [pscustomobject]@{ AverageMs=($sorted | Measure-Object -Average).Average; P95Ms=$sorted[$p95Index]; Count=$count }
}

function Write-TypeMetrics($Rows, $Vectors, $Chunks, [bool]$Hybrid, [double]$Threshold, [double]$Weight,
    [string]$Label, [bool]$TrackTiming = $false, [System.Collections.Generic.List[double]]$RankTimes = $null) {
    foreach ($group in ($Rows | Group-Object type | Sort-Object Name)) {
        $metric = Get-Metrics $group.Group $Vectors $Chunks $Hybrid $Threshold $Weight $TrackTiming $RankTimes
        Write-Host ('{0} type={1} R5(all)={2:P2} R5(capped)={3:P2} ceiling(all)={4:P2} MRR10={5:P2} no_result_empty={6:P2} queries={7}' -f $Label,$group.Name,$metric.RecallAt5,$metric.RecallAt5Capped,$metric.RecallAt5TheoreticalCeiling,$metric.MRRAt10,$metric.NoResultEmptyAccuracy,$group.Count)
    }
}

function Invoke-DevelopmentSelection($Development, $AuthorFixtures, $Vectors, $Chunks, [string]$SelectionPath,
    [string]$FixtureVersion, [bool]$TrackTiming = $false, [System.Collections.Generic.List[double]]$RankTimes = $null) {
    $lexical = Get-Metrics $Development @{} $Chunks $false 0.38 0.5 $TrackTiming $RankTimes
    $grid = New-Object 'System.Collections.Generic.List[object]'
    foreach ($weight in @(0.25, 0.5, 0.75)) {
        foreach ($threshold in @(0.38, 0.45, 0.52, 0.59, 0.66)) {
            $metric = Get-Metrics $Development $Vectors $Chunks $true $threshold $weight $TrackTiming $RankTimes
            $objective = 0.45 * $metric.RecallAt5 + 0.45 * $metric.MRRAt10 + 0.10 * $metric.NoResultEmptyAccuracy
            $grid.Add([pscustomobject]@{ Weight=$weight; Threshold=$threshold; Metrics=$metric; Objective=$objective })
        }
    }
    $selected = $grid | Sort-Object @{Expression='Objective';Descending=$true}, @{Expression={$_.Metrics.NoResultEmptyAccuracy};Descending=$true}, @{Expression='Weight';Descending=$false} | Select-Object -First 1
    $hybrid = Get-Metrics $Development $Vectors $Chunks $true $selected.Threshold $selected.Weight $TrackTiming $RankTimes
    $author = Get-Metrics $AuthorFixtures $Vectors $Chunks $true $selected.Threshold $selected.Weight $TrackTiming $RankTimes
    Write-Host ('DEV lexical Recall@5={0:P2} MRR@10={1:P2} no_result_empty={2:P2}' -f $lexical.RecallAt5,$lexical.MRRAt10,$lexical.NoResultEmptyAccuracy)
    Write-Host ('DEV tuned hybrid threshold={0} semanticWeight={1} Recall@5={2:P2} MRR@10={3:P2} no_result_empty={4:P2}' -f $selected.Threshold,$selected.Weight,$hybrid.RecallAt5,$hybrid.MRRAt10,$hybrid.NoResultEmptyAccuracy)
    Write-Host ('AUTHOR_WRITTEN K03/K05 Recall@5={0:P2} MRR@10={1:P2} no_result_queries=0' -f $author.RecallAt5,$author.MRRAt10)
    Write-TypeMetrics $Development $Vectors $Chunks $true $selected.Threshold $selected.Weight 'DEV_HYBRID' $TrackTiming $RankTimes
    Write-TypeMetrics $AuthorFixtures $Vectors $Chunks $true $selected.Threshold $selected.Weight 'AUTHOR_HYBRID' $TrackTiming $RankTimes
    $selectionData = @{ fixtureVersion=$FixtureVersion; developmentCount=$Development.Count; threshold=$selected.Threshold; semanticWeight=$selected.Weight; objective=$selected.Objective; createdUtc=[DateTime]::UtcNow.ToString('o') }
    Write-AtomicCacheFile -Path $SelectionPath -Content (ConvertTo-Json -InputObject $selectionData -Depth 4)
    return $selected
}
