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

function Write-AuthorK05Diagnostics($Rows, $Vectors, $Chunks, [double]$Threshold, [double]$Weight) {
    $records = New-Object 'System.Collections.Generic.List[object]'
    foreach ($row in $Rows) {
        $query = [string]$row.query
        $expected = @($row.expected_product_ids | ForEach-Object { [int]$_ })
        $lexical = [ECommerce.Infrastructure.AI.RetrievalScoring]::RankForEvaluation(
            $query, [single[]]@(), [ECommerce.Infrastructure.AI.RetrievalScoringChunk[]]$Chunks, $false, $Threshold, $Weight, 8)
        $hasVector = $Vectors.ContainsKey([string]$row.id)
        $hybrid = $null
        $maxRelevant = 'n/a'; $maxIrrelevant = 'n/a'
        $firstRelevantHybrid = 'n/a'; $hybridRelevantCount = 'n/a'; $hybridResultCount = 'n/a'
        $skuToken = ''
        $skuBranch = [ECommerce.Infrastructure.AI.RetrievalScoring]::TryResolveSkuIntent($query, $Chunks, [ref]$skuToken)
        if ($hasVector) {
            $queryVector = [single[]]$Vectors[[string]$row.id]
            $hybrid = [ECommerce.Infrastructure.AI.RetrievalScoring]::RankForEvaluation(
                $query, $queryVector, [ECommerce.Infrastructure.AI.RetrievalScoringChunk[]]$Chunks, $true, $Threshold, $Weight, 8)
            $hybridIds = @($hybrid | ForEach-Object ProductId)
            $hybridRelevantCount = @($hybridIds | Where-Object { $expected -contains [int]$_ }).Count
            $hybridResultCount = $hybrid.Count
            $firstRelevantHybrid = 'none'
            for ($index=0; $index -lt $hybridIds.Count; $index++) { if ($expected -contains [int]$hybridIds[$index]) { $firstRelevantHybrid = [string]($index+1); break } }
            $relevantScores = New-Object 'System.Collections.Generic.List[double]'
            $irrelevantScores = New-Object 'System.Collections.Generic.List[double]'
            foreach ($group in ($Chunks | Group-Object ProductId)) {
                $productChunks = [ECommerce.Infrastructure.AI.RetrievalScoringChunk[]]$group.Group
                $score = [ECommerce.Infrastructure.AI.RetrievalScoring]::ScoreProduct($query, $queryVector, $productChunks, $true,
                    $Threshold, $Weight, [bool]$skuBranch, [string]$skuToken)
                if ($expected -contains [int]$group.Name) { $relevantScores.Add([double]$score.SemanticScore) }
                else { $irrelevantScores.Add([double]$score.SemanticScore) }
            }
            if ($relevantScores.Count) { $maxRelevant = [Math]::Round(($relevantScores | Measure-Object -Maximum).Maximum, 6) }
            if ($irrelevantScores.Count) { $maxIrrelevant = [Math]::Round(($irrelevantScores | Measure-Object -Maximum).Maximum, 6) }
        }
        $lexicalIds = @($lexical | ForEach-Object ProductId)
        $lexicalRelevantCount = @($lexicalIds | Where-Object { $expected -contains [int]$_ }).Count
        $lexicalTop5Correct = @($lexicalIds | Select-Object -First 5 | Where-Object { $expected -contains [int]$_ }).Count
        $firstRelevantLexical = 'none'
        for ($index=0; $index -lt $lexicalIds.Count; $index++) { if ($expected -contains [int]$lexicalIds[$index]) { $firstRelevantLexical = [string]($index+1); break } }
        $compound = [bool]([regex]::IsMatch($query, '\b[\p{L}]+(?:-[\p{L}]+)+\b'))
        $predicate = [string]$row.label_predicate
        $records.Add([pscustomobject]@{
            Id=[string]$row.id; Predicate=$predicate; Compound=$compound; Cached=$hasVector; Sku=$skuBranch
            RelevantCount=$expected.Count; LexicalTop5Correct=$lexicalTop5Correct
            LexicalResults=$lexical.Count; HybridResults=$hybridResultCount
            LexicalFirst=$firstRelevantLexical; HybridFirst=$firstRelevantHybrid
            LexicalCorrect=$lexicalRelevantCount; HybridCorrect=$hybridRelevantCount
            HybridTop5Correct=if($hasVector){@($hybrid | Select-Object -First 5 | Where-Object { $expected -contains [int]$_.ProductId }).Count}else{$null}
            MaxRelevantSemantic=$maxRelevant; MaxIrrelevantSemantic=$maxIrrelevant
        })
        Write-Output ('K05_DIAG id={0} predicate="{1}" vector_cached={2} lexical_results={3} hybrid_results={4} first_correct_rank_lexical={5} first_correct_rank_hybrid={6} correct_returned_lexical={7} correct_returned_hybrid={8} max_semantic_correct={9} max_semantic_incorrect={10} sku_branch={11} hyphenated_compound={12}' -f
            $row.id,$predicate,$hasVector,$lexical.Count,$hybridResultCount,$firstRelevantLexical,$firstRelevantHybrid,$lexicalRelevantCount,$hybridRelevantCount,$maxRelevant,$maxIrrelevant,$skuBranch,$compound)
    }
    foreach ($group in @($records | Group-Object Predicate) + @($records | Group-Object Compound)) {
        if ($group.Name -eq 'True' -or $group.Name -eq 'False') { $label = 'hyphenated_compound=' + $group.Name }
        else { $label = 'predicate=' + $group.Name }
        $hybridRows = @($group.Group | Where-Object Cached)
        $meanRankRows = @($hybridRows | Where-Object { $_.HybridFirst -match '^\d+$' })
        $meanRank = if ($meanRankRows.Count) { [Math]::Round(($meanRankRows | ForEach-Object { [double]$_.HybridFirst } | Measure-Object -Average).Average, 3) } else { 'n/a' }
        $r5All=0.0; $r5Capped=0.0; $ceiling=0.0; $lexR5All=0.0
        foreach($item in $group.Group){
            if($item.RelevantCount -gt 0){$lexR5All += $item.LexicalTop5Correct/[double]$item.RelevantCount}
            if($item.Cached -and $item.RelevantCount -gt 0){
                $r5All += $item.HybridTop5Correct/[double]$item.RelevantCount
                $r5Capped += $item.HybridTop5Correct/[double][Math]::Min(5,$item.RelevantCount)
                $ceiling += [Math]::Min(5,$item.RelevantCount)/[double]$item.RelevantCount
            }
        }
        $hybridDenominator=if($hybridRows.Count){$hybridRows.Count}else{1}
        $diagnosticR5All=if($hybridRows.Count){$r5All/$hybridDenominator}else{'n/a'}
        $diagnosticR5Capped=if($hybridRows.Count){$r5Capped/$hybridDenominator}else{'n/a'}
        $diagnosticCeiling=if($hybridRows.Count){$ceiling/$hybridDenominator}else{'n/a'}
        $diagnosticLexR5=$lexR5All/[double][Math]::Max(1,$group.Count)
        $meanMaxRelevant=if($hybridRows.Count){[Math]::Round(($hybridRows|ForEach-Object{[double]$_.MaxRelevantSemantic}|Measure-Object -Average).Average,4)}else{'n/a'}
        $meanMaxIrrelevant=if($hybridRows.Count){[Math]::Round(($hybridRows|ForEach-Object{[double]$_.MaxIrrelevantSemantic}|Measure-Object -Average).Average,4)}else{'n/a'}
        Write-Output ('K05_DIAG_SUMMARY {0} queries={1} vectors_cached={2} sku_branches={3} lexical_R5all={4:P2} hybrid_R5all={5} hybrid_R5capped={6} ceiling_all={7} lexical_correct_returned={8} hybrid_evaluable={9} hybrid_correct_returned={10} mean_first_correct_hybrid_rank={11} mean_max_semantic_correct={12} mean_max_semantic_incorrect={13}' -f
            $label,$group.Count,@($group.Group|Where-Object Cached).Count,@($group.Group|Where-Object Sku).Count,
            $diagnosticLexR5,$(if($diagnosticR5All -is [double]){$diagnosticR5All.ToString('P2')}else{$diagnosticR5All}),
            $(if($diagnosticR5Capped -is [double]){$diagnosticR5Capped.ToString('P2')}else{$diagnosticR5Capped}),
            $(if($diagnosticCeiling -is [double]){$diagnosticCeiling.ToString('P2')}else{$diagnosticCeiling}),
            ($group.Group|Measure-Object -Property LexicalCorrect -Sum).Sum,$hybridRows.Count,
            ($hybridRows|Measure-Object -Property HybridCorrect -Sum).Sum,$meanRank,$meanMaxRelevant,$meanMaxIrrelevant)
    }
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
