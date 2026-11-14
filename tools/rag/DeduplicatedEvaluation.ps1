function Get-EvaluationUniqueRows([object[]]$Rows, [string]$SplitName = '') {
    $groups = [ordered]@{}
    foreach ($row in $Rows) {
        $normalized = [ECommerce.Infrastructure.AI.RetrievalScoring]::Normalize([string]$row.query)
        if ([string]::IsNullOrWhiteSpace($normalized)) { throw "Query '$($row.id)' normalizes to empty." }
        if (-not $groups.Contains($normalized)) { $groups[$normalized] = New-Object 'System.Collections.Generic.List[object]' }
        $groups[$normalized].Add($row)
    }
    $unique = New-Object 'System.Collections.Generic.List[object]'
    foreach ($key in $groups.Keys) {
        $items = $groups[$key].ToArray()
        $expectedSets = @($items | ForEach-Object { (Get-ExpectedProductIds $_ | Sort-Object -Unique) -join ',' } | Sort-Object -Unique)
        if ($expectedSets.Count -gt 1) { throw "Conflicting Expected Product IDs after normalization in split '$SplitName': ids=$(@($items | ForEach-Object id) -join ',')." }
        $first = $items[0]
        $copy = $first | Select-Object *
        $copy | Add-Member -NotePropertyName normalized_query -NotePropertyValue $key -Force
        $copy | Add-Member -NotePropertyName raw_row_count -NotePropertyValue $items.Count -Force
        $unique.Add($copy)
    }
    if($unique.Count -eq 0){return @()}
    return ,$unique.ToArray()
}

function Get-ExpectedProductIds([object]$Row) {
    if($null -eq $Row -or $null -eq $Row.PSObject.Properties['expected_product_ids']){return @()}
    return @($Row.expected_product_ids | ForEach-Object { [int]$_ })
}

function Assert-NoEvaluationLeakage([object[]]$LeftRows, [object[]]$RightRows, [string]$LeftName = 'development', [string]$RightName = 'holdout') {
    $left = @{}; foreach ($row in $LeftRows) { $left[[ECommerce.Infrastructure.AI.RetrievalScoring]::Normalize([string]$row.query)] = [string]$row.id }
    $leaks = New-Object 'System.Collections.Generic.List[string]'
    foreach ($row in $RightRows) {
        $normalized = [ECommerce.Infrastructure.AI.RetrievalScoring]::Normalize([string]$row.query)
        if ($left.ContainsKey($normalized)) { $leaks.Add("$($left[$normalized])/$($row.id)") }
    }
    if ($leaks.Count) { throw "Evaluation leakage between $LeftName and $RightName after runtime normalization: $($leaks -join '; ')." }
    return 0
}

function Get-DeduplicatedMetrics([object[]]$Rows, [hashtable]$Vectors, [object[]]$Chunks, [bool]$Hybrid, [double]$Threshold = 0.59, [double]$Weight = 0.25) {
    $answerable = @($Rows | Where-Object { (Get-ExpectedProductIds $_).Count -gt 0 })
    $noResult = @($Rows | Where-Object { (Get-ExpectedProductIds $_).Count -eq 0 })
    if ($Rows.Count -eq 0) { return [pscustomobject]@{ RawRows=0; UniqueQueries=0; Answerable=0; NoResult=0; RecallAt5=$null; MRRAt10=$null; NoResultFalsePositiveRate=$null; AnswerableCoverage=$null } }
    $recall = 0.0; $mrr = 0.0; $visibleNoResult = 0; $covered = 0
    foreach ($row in $Rows) {
        $decision = $null
        if ('ECommerce.Infrastructure.AI.AssistantIntentClassifier' -as [type]) { $decision = [ECommerce.Infrastructure.AI.AssistantIntentClassifier]::Classify([string]$row.query) }
        if ($decision -and $decision.Intent -in @('blocked','account_action','greeting')) { continue }
        if ($decision -and $decision.Intent -eq 'out_of_scope') {
            $lexicalGate = $false
            foreach ($chunk in $Chunks) { if ([ECommerce.Infrastructure.AI.RetrievalScoring]::LexicalScore([string]$decision.LexicalGateQuery,[string]$chunk.Content) -gt 0) { $lexicalGate=$true; break } }
            if (-not $lexicalGate) { continue }
        }
        $vector = if ($Hybrid) { [single[]]$Vectors[[string]$row.id] } else { [single[]]@() }
        $ranked = [ECommerce.Infrastructure.AI.RetrievalScoring]::RankForEvaluation([string]$row.query, $vector, [ECommerce.Infrastructure.AI.RetrievalScoringChunk[]]$Chunks, $Hybrid, $Threshold, $Weight, 8)
        $shown = @($ranked | Select-Object -First 4)
        $expected = Get-ExpectedProductIds $row
        if ($expected.Count -eq 0) { if ($shown.Count -gt 0) { $visibleNoResult++ }; continue }
        $top5 = @($ranked | Select-Object -First 5 | ForEach-Object { [int]$_.ProductId })
        $hitCount = @($top5 | Where-Object { $expected -contains $_ }).Count
        $recall += $hitCount / [double]$expected.Count
        if (@($shown | Where-Object { $expected -contains [int]$_.ProductId }).Count -gt 0) { $covered++ }
        $top10 = @($ranked | Select-Object -First 10 | ForEach-Object { [int]$_.ProductId })
        for ($index=0; $index -lt $top10.Count; $index++) { if ($expected -contains $top10[$index]) { $mrr += 1.0 / ($index + 1); break } }
    }
    [pscustomobject]@{
        RawRows=($Rows | Measure-Object -Property raw_row_count -Sum).Sum
        UniqueQueries=$Rows.Count; Answerable=$answerable.Count; NoResult=$noResult.Count
        RecallAt5=if($answerable.Count){$recall/$answerable.Count}else{$null}
        MRRAt10=if($answerable.Count){$mrr/$answerable.Count}else{$null}
        NoResultFalsePositiveRate=if($noResult.Count){$visibleNoResult/[double]$noResult.Count}else{$null}
        AnswerableCoverage=if($answerable.Count){$covered/[double]$answerable.Count}else{$null}
    }
}

function Get-OwnerReviewedRows([object[]]$Rows) {
    @($Rows | Where-Object { $_.owner_confirmed -eq $true })
}
