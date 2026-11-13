param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$RagInputs = (Join-Path (Split-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path -Parent) 'Marketplace-khang-ai_rag\RAG_inputs'),
    [switch]$LexicalOnly,
    [switch]$DevelopmentOnly,
    [switch]$HoldoutFinal,
    [ValidateRange(0,60)][int]$BatchDelaySeconds = 3,
    [ValidateRange(1,10)][int]$RetryMaxAttempts = 5,
    [ValidateRange(1,90)][int]$RetryMaxDelaySeconds = 90,
    [ValidateRange(1,600)][int]$RetryMaxTotalWaitSeconds = 300,
    [ValidateRange(0,100000)][int]$MaxBatchesPerRun = 0,
    [switch]$DryRunFromCache,
    [string]$DryRunDataPath,
    [switch]$DryRunTrackRankTiming,
    [switch]$CacheForecastOnly,
    [ValidateRange(0,1000000)][int]$ExpectedDocumentChunks = 1395
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Data
. (Join-Path $PSScriptRoot 'AtomicJsonCache.ps1')
. (Join-Path $PSScriptRoot 'EvaluationResilience.ps1')
. (Join-Path $PSScriptRoot 'EvaluationMetrics.ps1')
if ($DevelopmentOnly -and $HoldoutFinal) { throw 'Choose either -DevelopmentOnly or -HoldoutFinal.' }
if (-not $LexicalOnly -and -not $DevelopmentOnly -and -not $HoldoutFinal -and -not $CacheForecastOnly -and -not $DryRunFromCache) { throw 'Specify -DevelopmentOnly, -HoldoutFinal, -CacheForecastOnly, or -DryRunFromCache; holdout is never run by default.' }

if (-not ('RagEvalKernel' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

public sealed class RagEvalChunk
{
    public int ProductId { get; set; }
    public string Type { get; set; }
    public string Content { get; set; }
    public string Hash { get; set; }
    public float[] Vector { get; set; }
}

public sealed class RagEvalHit
{
    public int ProductId { get; set; }
    public double Score { get; set; }
}
public static class RagEvalKernel
{
    private static readonly HashSet<string> StopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "toi", "minh", "ban", "cho", "voi", "la", "co", "khong", "nao", "gi", "nhe", "nha", "va", "cua", "mot",
        "cac", "nhung", "hay", "duoc", "can", "muon", "tim", "mua", "loai", "san", "pham", "hang", "shop", "gia",
        "re", "de", "duoi", "tren", "tam", "khoang", "the", "please", "find", "show", "me", "some", "for", "with", "under", "over"
    };
    public static string Normalize(string value)
    {
        var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                builder.Append(ch == 'đ' ? 'd' : ch);
        return Regex.Replace(Regex.Replace(builder.ToString(), @"[^\p{L}\p{N}]", " "), @"\s+", " ").Trim();
    }
    private static double Lexical(string normalizedQuery, string[] terms, string normalizedContent)
    {
        if (normalizedQuery.Length == 0 || normalizedContent.Length == 0) return 0;
        double score = normalizedContent.IndexOf(normalizedQuery, StringComparison.OrdinalIgnoreCase) >= 0 ? 0.8 : 0;
        var words = new HashSet<string>(normalizedContent.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
        if (terms.Length > 0) score += terms.Count(words.Contains) / (double)terms.Length * 0.7;
        return Math.Min(1, score);
    }
    private static double Cosine(float[] a, float[] b)
    {
        if (a.Length == 0 || a.Length != b.Length) return 0;
        double dot=0, na=0, nb=0;
        for (int i=0;i<a.Length;i++) { dot += a[i]*b[i]; na += a[i]*a[i]; nb += b[i]*b[i]; }
        return na == 0 || nb == 0 ? 0 : dot / (Math.Sqrt(na)*Math.Sqrt(nb));
    }
    public static List<RagEvalHit> Rank(string query, float[] queryVector, RagEvalChunk[] chunks, bool hybrid, double minSemantic, double semanticWeight)
    {
        var sku = Regex.Match(query, @"\b[A-Z0-9]{2,}(?:-[A-Z0-9]{2,}){2,}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (sku.Success)
        {
            var skuHits = chunks.Where(c => c.Type == "summary" && Regex.IsMatch(c.Content, @"\bSKU:\s*" + Regex.Escape(sku.Value) + @"\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                .Select(c => c.ProductId).Distinct().Select(id => new RagEvalHit { ProductId=id, Score=1.0 }).OrderBy(h => h.ProductId).ToList();
            return skuHits;
        }
        var normalizedQuery = Normalize(query);
        var terms = normalizedQuery.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 1 && !StopWords.Contains(t)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var grouped = chunks.GroupBy(c => c.ProductId);
        var hits = new List<RagEvalHit>();
        foreach (var group in grouped)
        {
            double lexical=0, semantic=0;
            foreach (var chunk in group)
            {
                lexical = Math.Max(lexical, Lexical(normalizedQuery, terms, Normalize(chunk.Content)));
                if (hybrid) semantic = Math.Max(semantic, Cosine(queryVector, chunk.Vector));
            }
            var combined = hybrid ? (1-semanticWeight)*lexical + semanticWeight*Math.Max(0, semantic) : lexical;
            if (hybrid && lexical <= 0 && semantic < minSemantic && combined < 0.22) continue;
            if (!hybrid && lexical <= 0) continue;
            hits.Add(new RagEvalHit { ProductId=group.Key, Score=combined });
        }
        return hits.OrderByDescending(h => h.Score).ThenBy(h => h.ProductId).ToList();
    }
}
'@
}

$script:RankTimes = [System.Collections.Generic.List[double]]::new()
$script:TrackRankTiming = $false

if ($CacheForecastOnly) {
    $catalogCachePath = Join-Path $RepoRoot 'obj/rag-embedding-cache.json'
    $queryCachePath = Join-Path $RepoRoot 'obj/rag-query-embedding-cache.json'
    if (-not [IO.File]::Exists($catalogCachePath)) { throw 'Catalog embedding cache is missing.' }
    $catalogEntries = Get-Content -LiteralPath $catalogCachePath -Raw | ConvertFrom-Json -ErrorAction Stop
    $validCatalogCount = @($catalogEntries | Where-Object {
        $_.modelName -eq 'gemini-embedding-001' -and [int]$_.dimensions -eq 768 -and
        $_.cacheKey -eq "$($_.contentHash):$($_.modelName):$($_.dimensions)" -and @($_.vector).Count -eq 768
    } | Group-Object cacheKey).Count
    $missingChunks = [Math]::Max(0,$ExpectedDocumentChunks-$validCatalogCount)
    $fixturePath = Join-Path $RagInputs 'RAG_EVALUATION_QUERIES_V2.jsonl'
    $authorPath = Join-Path $RepoRoot 'docs/rag-evaluation/RAG_AUTHOR_WRITTEN_K03_K05.jsonl'
    $fixtureRows = @(Get-Content -LiteralPath $fixturePath | ForEach-Object { $_ | ConvertFrom-Json })
    $developmentRows = @($fixtureRows | Where-Object split -eq 'development')
    $authorRows = @(Get-Content -LiteralPath $authorPath | ForEach-Object { $_ | ConvertFrom-Json })
    $queryCache = Read-QueryVectorCache -Path $queryCachePath -ModelName 'gemini-embedding-001' -Dimensions 768
    $queryTexts = @($developmentRows.query) + @($authorRows.query)
    $uniqueQueryKeys = @($queryTexts | ForEach-Object { Get-QueryVectorCacheKey ([string]$_) 'gemini-embedding-001' 768 } | Sort-Object -Unique)
    $missingQueries = @($uniqueQueryKeys | Where-Object { -not $queryCache.ContainsKey($_) }).Count
    Write-Output ('CacheForecastOnly catalog_cache_valid={0} expected_catalog_chunks={1} missing_chunks={2} query_cache_valid={3} expected_unique_queries={4} missing_queries={5} estimated_api_calls=0' -f $validCatalogCount,$ExpectedDocumentChunks,$missingChunks,$queryCache.Count,$uniqueQueryKeys.Count,$missingQueries)
    exit 0
}

if ($DryRunFromCache) {
    if ([string]::IsNullOrWhiteSpace($DryRunDataPath)) { throw '-DryRunDataPath is required with -DryRunFromCache.' }
    $dry = Get-Content -LiteralPath $DryRunDataPath -Raw | ConvertFrom-Json -ErrorAction Stop
    $docCache = @{}
    $loadedDocEntries = Get-Content -LiteralPath $dry.catalogCachePath -Raw | ConvertFrom-Json -ErrorAction Stop
    foreach ($entry in $loadedDocEntries) {
        $expected = "$($entry.contentHash):$($entry.modelName):$($entry.dimensions)"
        if ($entry.cacheKey -eq $expected -and @($entry.vector).Count -eq [int]$entry.dimensions) { $docCache[$expected] = $entry }
    }
    $chunks = New-Object 'System.Collections.Generic.List[RagEvalChunk]'
    foreach ($item in $dry.chunks) {
        $key = "$($item.hash):$($dry.modelName):$($dry.dimensions)"
        if (-not $docCache.ContainsKey($key)) { throw "Dry-run catalog cache miss for fixture chunk $($item.hash)." }
        $chunks.Add([RagEvalChunk]@{ ProductId=[int]$item.productId; Type=[string]$item.type; Content=[string]$item.content; Hash=[string]$item.hash; Vector=[single[]]$docCache[$key].vector })
    }
    $development = @($dry.development)
    $authorFixtures = @($dry.authorFixtures)
    $allQueries = @($development.query) + @($authorFixtures.query)
    $queryCache = Read-QueryVectorCache -Path $dry.queryCachePath -ModelName $dry.modelName -Dimensions ([int]$dry.dimensions)
    $script:TrackRankTiming = [bool]$DryRunTrackRankTiming
    $script:RankTimes = [System.Collections.Generic.List[double]]::new()
    $queryVectors = Resolve-CachedQueryVectors -Queries $allQueries -Path $dry.queryCachePath -ModelName $dry.modelName -Dimensions ([int]$dry.dimensions) -Cache $queryCache -BatchDelaySeconds 0 -EmbedMissing { throw 'Dry-run is cache-only and cannot embed.' }
    $vectorMap = @{}
    for ($i=0; $i -lt $development.Count; $i++) { $vectorMap[[string]$development[$i].id] = $queryVectors[$i] }
    for ($i=0; $i -lt $authorFixtures.Count; $i++) { $vectorMap[[string]$authorFixtures[$i].id] = $queryVectors[$development.Count+$i] }
    Write-Output ('DryRunCacheForecast missing_chunks=0 missing_queries=0 estimated_api_calls=0 chunks={0} queries={1}' -f $chunks.Count,$allQueries.Count)
    if ($LexicalOnly) {
        $lexicalDev = Get-Metrics $development @{} $chunks.ToArray() $false 0.38 0.5 $script:TrackRankTiming $script:RankTimes
        $lexicalAuthor = Get-Metrics $authorFixtures @{} $chunks.ToArray() $false 0.38 0.5 $script:TrackRankTiming $script:RankTimes
        Write-Output ('DRYRUN DEV lexical Recall@5={0:P2} MRR@10={1:P2} no_result_empty={2:P2}' -f $lexicalDev.RecallAt5,$lexicalDev.MRRAt10,$lexicalDev.NoResultEmptyAccuracy)
        Write-Output ('DRYRUN AUTHOR lexical Recall@5={0:P2} MRR@10={1:P2} no_result_empty={2:P2}' -f $lexicalAuthor.RecallAt5,$lexicalAuthor.MRRAt10,$lexicalAuthor.NoResultEmptyAccuracy)
        Write-Output ('RankTimingEnabled={0};RankSamples={1};DryRun=TRUE;HoldoutEvaluated=FALSE' -f $script:TrackRankTiming,$script:RankTimes.Count)
        exit 0
    }
    $selection = Invoke-DevelopmentSelection -Development $development -AuthorFixtures $authorFixtures -Vectors $vectorMap -Chunks $chunks.ToArray() -SelectionPath $dry.selectionPath -FixtureVersion 'DRY_RUN_FIXTURE' -TrackTiming $script:TrackRankTiming -RankTimes $script:RankTimes
    Write-Output ('SelectedDevThreshold={0};SelectedSemanticWeight={1};RankTimingEnabled={2};RankSamples={3};DryRun=TRUE;HoldoutEvaluated=FALSE' -f $selection.Threshold,$selection.Weight,$script:TrackRankTiming,$script:RankTimes.Count)
    Write-Output "DevelopmentSelectionSaved=$($dry.selectionPath)"
    exit 0
}

$keyLine = Get-Content -LiteralPath (Join-Path $RepoRoot '.env') | Where-Object { $_ -match '^\s*GEMINI_API_KEY\s*=' } | Select-Object -First 1
if (-not $keyLine) { throw 'GEMINI_API_KEY is missing from repo-root .env' }
$apiKey = ($keyLine -replace '^\s*GEMINI_API_KEY\s*=\s*','').Trim().Trim('"',"'")
if ([string]::IsNullOrWhiteSpace($apiKey)) { throw 'GEMINI_API_KEY is empty' }

$connection = [System.Data.SqlClient.SqlConnection]::new('Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=ECommerceDb_Dev;Integrated Security=True;Connect Timeout=8')
$command = $connection.CreateCommand()
$command.CommandTimeout = 30
$command.CommandText = @'
SELECT p.Id,p.Name,p.Sku,c.Name AS CategoryName,b.Name AS BrandName,p.Description,
       ad.DisplayName AS AttributeName,ad.Unit,v.DataType,v.ValueText,v.ValueNumber,v.ValueBoolean
FROM dbo.Products p
JOIN dbo.Categories c ON c.Id=p.CategoryId
LEFT JOIN dbo.Brands b ON b.Id=p.BrandId
LEFT JOIN dbo.ProductAttributeValues v ON v.ProductId=p.Id
LEFT JOIN dbo.AttributeDefinitions ad ON ad.Id=v.AttributeDefinitionId AND ad.IsActive=1
ORDER BY p.Id,ad.DisplayName;
'@
$table = [System.Data.DataTable]::new()
$connection.Open()
try { [void]([System.Data.SqlClient.SqlDataAdapter]::new($command).Fill($table)) }
finally { $connection.Close(); $connection.Dispose() }

$productMap = @{}
foreach ($row in $table.Rows) {
    $id = [int]$row.Id
    if (-not $productMap.ContainsKey($id)) {
        $productMap[$id] = [ordered]@{
            Name=[string]$row.Name; Sku=[string]$row.Sku; Category=[string]$row.CategoryName
            Brand=if($row.IsNull('BrandName')){'Unknown'}else{[string]$row.BrandName}
            Description=if($row.IsNull('Description')){''}else{([string]$row.Description).Trim()}
            Attributes=[System.Collections.Generic.List[string]]::new()
        }
    }
    if (-not $row.IsNull('AttributeName')) {
        $unit=if($row.IsNull('Unit')){''}else{[string]$row.Unit}
        switch ([int]$row.DataType) {
            0 { $value=if($row.IsNull('ValueText')){''}else{[string]$row.ValueText} }
            1 { $value=if($row.IsNull('ValueNumber')){''}else{([decimal]$row.ValueNumber).ToString([Globalization.CultureInfo]::InvariantCulture)} }
            2 { $value=if($row.IsNull('ValueBoolean')){''}else{([bool]$row.ValueBoolean).ToString()} }
            default { $value='' }
        }
        $attribute = "{0}: {1}" -f [string]$row.AttributeName,$value
        if (-not [string]::IsNullOrWhiteSpace($unit)) { $attribute += " $unit" }
        $productMap[$id].Attributes.Add($attribute)
    }
}
if ($productMap.Count -ne 500) { throw "Expected 500 catalog products; read $($productMap.Count)." }

$chunks = [System.Collections.Generic.List[RagEvalChunk]]::new()
$sha256=[System.Security.Cryptography.SHA256]::Create()
function Get-ContentHash([string]$text) {
    return [BitConverter]::ToString($sha256.ComputeHash([Text.Encoding]::UTF8.GetBytes($text))).Replace('-','')
}
foreach ($entry in $productMap.GetEnumerator()) {
    $id=[int]$entry.Key; $product=$entry.Value
    $summary="{0}. SKU: {1}. Brand: {2}. Category: {3}." -f $product.Name,$product.Sku,$product.Brand,$product.Category
    $chunks.Add([RagEvalChunk]@{ProductId=$id;Type='summary';Content=$summary;Hash=(Get-ContentHash $summary)})
    if ($product.Attributes.Count -gt 0) {
        $spec=[string]::Join("`n",$product.Attributes)
        $chunks.Add([RagEvalChunk]@{ProductId=$id;Type='specifications';Content=$spec;Hash=(Get-ContentHash $spec)})
    }
    if (-not [string]::IsNullOrWhiteSpace($product.Description)) {
        $description=$product.Description.Substring(0,[Math]::Min(1800,$product.Description.Length))
        $chunks.Add([RagEvalChunk]@{ProductId=$id;Type='description';Content=$description;Hash=(Get-ContentHash $description)})
    }
}

if ($LexicalOnly) {
    $devRows=@(Get-Content -LiteralPath (Join-Path $RagInputs 'RAG_EVALUATION_QUERIES_V2.jsonl') | ForEach-Object { $_ | ConvertFrom-Json } | Where-Object split -eq 'development')
    $authorRows=@(Get-Content -LiteralPath (Join-Path $RepoRoot 'docs/rag-evaluation/RAG_AUTHOR_WRITTEN_K03_K05.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
    foreach($set in @(@{Name='DEV';Rows=$devRows},@{Name='AUTHOR_WRITTEN_K03_K05';Rows=$authorRows})) {
        $relevant=@($set.Rows | Where-Object { @($_.expected_product_ids).Count -gt 0 });$empty=@($set.Rows | Where-Object { @($_.expected_product_ids).Count -eq 0 });$r=0.0;$m=0.0;$emptyCorrect=0
        foreach($row in $set.Rows) {
            $ranked=[RagEvalKernel]::Rank([string]$row.query,[single[]]@(),$chunks.ToArray(),$false,0.38,0.5)
            $ids=@($ranked | Select-Object -First 10 | ForEach-Object ProductId)
            if(@($row.expected_product_ids).Count -gt 0) {
                $expected=@($row.expected_product_ids);$top5=@($ids | Select-Object -First 5);$r += @($top5 | Where-Object { $expected -contains $_ }).Count/[double]$expected.Count
                for($i=0;$i -lt $ids.Count;$i++){if($expected -contains $ids[$i]){$m += 1.0/($i+1);break}}
            } elseif($ranked.Count -eq 0){$emptyCorrect++}
        }
        $r5=if($relevant.Count){$r/$relevant.Count}else{0};$mrr=if($relevant.Count){$m/$relevant.Count}else{0};$noResult=if($empty.Count){$emptyCorrect/[double]$empty.Count}else{0}
        Write-Output ('{0} lexical Recall@5={1:P2} MRR@10={2:P2} no_result_empty={3:P2} relevant={4} no_result={5}' -f $set.Name,$r5,$mrr,$noResult,$relevant.Count,$empty.Count)
    }
    Write-Output ('LexicalOnly=TRUE;HoldoutEvaluated=FALSE;Chunks={0}' -f $chunks.Count)
    exit 0
}

$headers=@{ 'x-goog-api-key'=$apiKey }
$script:EmbeddingApiCalls=0;$script:DocumentApiCalls=0;$script:QueryApiCalls=0
$script:EmbeddingCallTimes=[System.Collections.Generic.List[double]]::new()
$cachePath=Join-Path $RepoRoot 'obj/rag-embedding-cache.json'
$cacheEntries=@{}
if(Test-Path -LiteralPath $cachePath){
    try{
        $existing=Get-Content -LiteralPath $cachePath -Raw|ConvertFrom-Json
        foreach($entry in $existing){
            $expectedKey="{0}:{1}:{2}" -f $entry.contentHash,$entry.modelName,$entry.dimensions
            if($entry.cacheKey -eq $expectedKey -and $entry.modelName -eq 'gemini-embedding-001' -and [int]$entry.dimensions -eq 768 -and @($entry.vector).Count -eq 768){$cacheEntries[$expectedKey]=$entry}
        }
        Write-Host "CatalogCacheRead=valid|Entries=$($cacheEntries.Count)"
    }catch{$cacheEntries=@{};Write-Host "CatalogCacheRead=ignored|FailureType=$($_.Exception.GetType().Name)"}
}

function Save-EmbeddingCache {
    $jsonCache=ConvertTo-Json -InputObject @($cacheEntries.Values) -Depth 6
    Write-AtomicCacheFile -Path $cachePath -Content $jsonCache
}

function Get-Embeddings([string[]]$texts,[bool]$isQuery,[object[]]$cacheMetadata=@()) {
    $all=[System.Collections.Generic.List[object]]::new()
    $task=if($isQuery){'RETRIEVAL_QUERY'}else{'RETRIEVAL_DOCUMENT'}
    $batchCount=[int][Math]::Ceiling($texts.Count/16.0)
    for($offset=0;$offset -lt $texts.Count;$offset+=16) {
        $batchNumber=[int][Math]::Floor($offset/16)+1
        if($offset -gt 0 -and $BatchDelaySeconds -gt 0){Write-Host "EmbeddingBatchWait=batch $batchNumber/$batchCount|DelaySeconds=$BatchDelaySeconds|Reason=inter-batch pacing";Start-Sleep -Seconds $BatchDelaySeconds}
        $batch=@($texts | Select-Object -Skip $offset -First 16)
        $requests=@(foreach($text in $batch){@{model='models/gemini-embedding-001';content=@{parts=@(@{text=$text})};taskType=$task;outputDimensionality=768}})
        $payload=@{requests=$requests}|ConvertTo-Json -Depth 8 -Compress
        $batchTimer=[Diagnostics.Stopwatch]::StartNew();$success=$false;$totalRetryWaitMs=0;$completedLong429Waits=0
        for($attempt=1;$attempt -le $RetryMaxAttempts;$attempt++){
            $script:EmbeddingApiCalls++
            if($isQuery){$script:QueryApiCalls++}else{$script:DocumentApiCalls++}
            try{
                $response=Invoke-RestMethod -Method Post -Uri 'https://generativelanguage.googleapis.com/v1beta/models/gemini-embedding-001:batchEmbedContents' -Headers $headers -ContentType 'application/json' -Body $payload -TimeoutSec 20
                $success=$true;$status='200';break
            }catch{
                $status='none';$failureType=$_.Exception.GetType().Name;$retryAfterHeader=$null;$quotaDiagnostic=$null
                if($_.Exception.Response){
                    $status=[string][int]$_.Exception.Response.StatusCode
                    $retryAfterHeader=$_.Exception.Response.Headers['Retry-After']
                }
                if($status -eq '429'){
                    $errorBody=Get-HttpErrorBody $_.Exception
                    $quotaDiagnostic=Get-GeminiQuotaDiagnostic ([int]$status) $errorBody
                    $errorBody=$null
                    Write-Host "GeminiQuota429|HTTP=$($quotaDiagnostic.HttpStatus)|Status=$($quotaDiagnostic.Status)|QuotaMetric=$($quotaDiagnostic.QuotaMetric)|QuotaId=$($quotaDiagnostic.QuotaId)|QuotaDimensions=$($quotaDiagnostic.QuotaDimensions)|RetryDelaySeconds=$($quotaDiagnostic.RetryDelaySeconds)|Classification=$($quotaDiagnostic.Classification)"
                    $stopReason=Get-Quota429StopReason $quotaDiagnostic $completedLong429Waits
                    if($stopReason){
                        $batchTimer.Stop();$script:EmbeddingCallTimes.Add($batchTimer.Elapsed.TotalMilliseconds)
                        if(-not $isQuery){throw "Embedding stopped early at batch $batchNumber/$batchCount ($stopReason). Document chunks saved this run=$script:DocumentChunksSavedThisRun; remaining=$($texts.Count-$offset); total valid cache entries=$($cacheEntries.Count). Retry after quota reset or later. Response body suppressed."}
                        throw "Query embedding stopped early ($stopReason); response body suppressed."
                    }
                } elseif($status -ne '429') { $completedLong429Waits=0 }
                if($status -notin @('429','503') -or $attempt -eq $RetryMaxAttempts){
                    $batchTimer.Stop();$script:EmbeddingCallTimes.Add($batchTimer.Elapsed.TotalMilliseconds)
                    if(-not $isQuery){
                        $remaining=[Math]::Max(0,$script:DocumentChunksTotalMissing-$script:DocumentChunksSavedThisRun)
                        throw "Embedding stopped at batch $batchNumber/$batchCount (HTTP $status, attempts=$attempt). Document chunks saved this run=$script:DocumentChunksSavedThisRun; remaining=$remaining; total valid cache entries=$($cacheEntries.Count). Response body suppressed."
                    }
                    throw "Embedding failure HTTP=$status attempts=$attempt elapsedMs=$([int]$batchTimer.Elapsed.TotalMilliseconds) failureType=$failureType; response body suppressed."
                }
                $delayMs=$null
                if($retryAfterHeader){
                    $seconds=0.0
                    if([double]::TryParse([string]$retryAfterHeader,[ref]$seconds)){$delayMs=[Math]::Max(0,[int]($seconds*1000))}
                    else{$date=[DateTimeOffset]::MinValue;if([DateTimeOffset]::TryParse([string]$retryAfterHeader,[ref]$date)){$delayMs=[Math]::Max(0,[int]($date.Subtract([DateTimeOffset]::UtcNow).TotalMilliseconds))}}
                }
                if($null -eq $delayMs){
                    if($quotaDiagnostic -and $null -ne $quotaDiagnostic.RetryDelaySeconds){$delayMs=[int][Math]::Ceiling($quotaDiagnostic.RetryDelaySeconds*1000)}
                }
                if($null -eq $delayMs){
                    $baseDelay=[Math]::Min(15*[Math]::Pow(2,$attempt-1),$RetryMaxDelaySeconds)
                    $baseDelayMs=[int]($baseDelay*1000)
                    $jitterMaxMs=[int]([Math]::Min(1000,($RetryMaxDelaySeconds-$baseDelay)*1000))
                    $jitterMs=if($jitterMaxMs -gt 0){Get-Random -Minimum 0 -Maximum ($jitterMaxMs+1)}else{0}
                    $delayMs=$baseDelayMs+$jitterMs
                }
                if($totalRetryWaitMs+$delayMs -gt ($RetryMaxTotalWaitSeconds*1000)){
                    $batchTimer.Stop();$script:EmbeddingCallTimes.Add($batchTimer.Elapsed.TotalMilliseconds)
                    if(-not $isQuery){$remaining=[Math]::Max(0,$script:DocumentChunksTotalMissing-$script:DocumentChunksSavedThisRun);throw "Embedding retry wait limit reached at batch $batchNumber/$batchCount (HTTP $status, attempts=$attempt). Document chunks saved this run=$script:DocumentChunksSavedThisRun; remaining=$remaining; total valid cache entries=$($cacheEntries.Count). Response body suppressed."}
                    throw "Embedding retry wait limit reached HTTP=$status attempts=$attempt; response body suppressed."
                }
                $totalRetryWaitMs+=$delayMs
                if($status -eq '429'){
                    if($delayMs -ge 60000){$completedLong429Waits++}else{$completedLong429Waits=0}
                }
                Write-Host "EmbeddingRetry=batch $batchNumber/$batchCount|HTTP$status|Attempt=$attempt/$RetryMaxAttempts|WaitingSeconds=$([Math]::Round($delayMs/1000.0,1))|TotalWaitSeconds=$([Math]::Round($totalRetryWaitMs/1000.0,1))/$RetryMaxTotalWaitSeconds"
                Start-Sleep -Milliseconds $delayMs
            }
        }
        $batchTimer.Stop();$script:EmbeddingCallTimes.Add($batchTimer.Elapsed.TotalMilliseconds)
        if(-not $success -or @($response.embeddings).Count -ne $batch.Count){throw 'Embedding response count did not match request count.'}
        $batchVectors=@($response.embeddings|ForEach-Object{,([single[]]@($_.values))})
        if($cacheMetadata.Count -gt 0){
            for($index=0;$index -lt $batchVectors.Count;$index++){
                $chunk=$cacheMetadata[$offset+$index];$key="$($chunk.Hash):gemini-embedding-001:768"
                $cacheEntries[$key]=[pscustomobject]@{cacheKey=$key;contentHash=$chunk.Hash;modelName='gemini-embedding-001';dimensions=768;productId=$chunk.ProductId;chunkType=$chunk.Type;vector=$batchVectors[$index]}
            }
            Save-EmbeddingCache
            $script:DocumentChunksSavedThisRun += $batch.Count
            Write-Host "CatalogCacheBatchSaved=batch $batchNumber/$batchCount|ChunkCount=$($batch.Count)|SavedThisRun=$script:DocumentChunksSavedThisRun|Entries=$($cacheEntries.Count)|Atomic=TRUE"
        }
        foreach($vector in $batchVectors){$all.Add($vector)}
        Write-Host "EmbeddingBatchDone=$batchNumber/$batchCount|BatchItems=$($batch.Count)|Task=$task"
    }
    return ,$all.ToArray()
}

$uniqueMissing=@($chunks|Group-Object Hash|ForEach-Object{$_.Group[0]}|Where-Object{$cacheEntries["$($_.Hash):gemini-embedding-001:768"] -eq $null})
$fixtures=@(Get-Content -LiteralPath (Join-Path $RagInputs 'RAG_EVALUATION_QUERIES_V2.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
$authorFixtures=@(Get-Content -LiteralPath (Join-Path $RepoRoot 'docs/rag-evaluation/RAG_AUTHOR_WRITTEN_K03_K05.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
$development=@($fixtures | Where-Object split -eq 'development')
$holdout=@($fixtures | Where-Object split -eq 'holdout')
if($development.Count -ne 96 -or $holdout.Count -ne 24){throw 'Fixture split counts differ from the handoff.'}
$plannedQueryRows=if($HoldoutFinal){$holdout}else{@($development)+@($authorFixtures)}
$queryCachePath=Join-Path $RepoRoot 'obj/rag-query-embedding-cache.json'
$queryCache=Read-QueryVectorCache -Path $queryCachePath -ModelName 'gemini-embedding-001' -Dimensions 768
$queryPlanKeys=@($plannedQueryRows.query|ForEach-Object{Get-QueryVectorCacheKey ([string]$_) 'gemini-embedding-001' 768}|Sort-Object -Unique)
$queryMissingCount=@($queryPlanKeys|Where-Object{-not $queryCache.ContainsKey($_)}).Count
$fullDocumentBatchCount=[int][Math]::Ceiling($uniqueMissing.Count/16.0)
$documentBatchCount=$fullDocumentBatchCount
if($MaxBatchesPerRun -gt 0){$documentBatchCount=[Math]::Min($documentBatchCount,$MaxBatchesPerRun)}
$plannedDocumentChunkCount=[Math]::Min($uniqueMissing.Count,$documentBatchCount*16)
$queryBatchCount=[int][Math]::Ceiling($queryMissingCount/16.0)
$queryBatchesThisRun=if($plannedDocumentChunkCount -eq $uniqueMissing.Count){$queryBatchCount}else{0}
$maxBatchesLabel=if($MaxBatchesPerRun -eq 0){'unlimited'}else{[string]$MaxBatchesPerRun}
Write-Output ('EmbeddingForecast missing_chunks={0} document_batches={1}/{2} max_batches_per_run={3} unique_queries_to_embed={4} query_batches={5} estimated_calls_this_run={6} estimated_calls_for_complete_run={7}' -f $uniqueMissing.Count,$documentBatchCount,$fullDocumentBatchCount,$maxBatchesLabel,$queryMissingCount,$queryBatchCount,($documentBatchCount+$queryBatchesThisRun),($fullDocumentBatchCount+$queryBatchCount))
$script:DocumentChunksSavedThisRun=0
$script:DocumentChunksTotalMissing=$uniqueMissing.Count
$plannedMissing=@($uniqueMissing|Select-Object -First $plannedDocumentChunkCount)
$docVectors=Get-Embeddings -texts @($plannedMissing|ForEach-Object Content) -isQuery $false -cacheMetadata $plannedMissing
if($docVectors.Count -ne $plannedMissing.Count){throw 'Document embedding count did not match this run plan.'}
if($plannedMissing.Count -lt $uniqueMissing.Count){throw "MaxBatchesPerRun reached after saving $script:DocumentChunksSavedThisRun chunks. Remaining chunks=$($uniqueMissing.Count-$plannedMissing.Count); rerun to resume from cache."}
foreach($chunk in $chunks){$chunk.Vector=[single[]]$cacheEntries["$($chunk.Hash):gemini-embedding-001:768"].vector}
if($uniqueMissing.Count -gt 0){Write-Host "CatalogCacheWrite=complete|Entries=$($cacheEntries.Count)|Path=$cachePath"}

if($HoldoutFinal){
    $selectionPath=Join-Path $RepoRoot 'obj/rag-eval-selection.json'
    if(-not (Test-Path -LiteralPath $selectionPath)){throw 'Development selection file is missing; do not evaluate holdout before development tuning.'}
    $selection=Get-Content -LiteralPath $selectionPath -Raw|ConvertFrom-Json
    if($selection.fixtureVersion -ne 'RAG_EVALUATION_QUERIES_V2' -or $selection.developmentCount -ne 96){throw 'Development selection metadata is invalid.'}
    $threshold=[double]$selection.threshold;$weight=[double]$selection.semanticWeight
    $script:TrackRankTiming=$true
    $holdoutVectors=Resolve-CachedQueryVectors -Queries @($holdout.query) -Path $queryCachePath -ModelName 'gemini-embedding-001' -Dimensions 768 -Cache $queryCache -BatchDelaySeconds $BatchDelaySeconds -EmbedMissing { param($batch) Get-Embeddings -texts $batch -isQuery $true }
    if($holdoutVectors.Count -ne $holdout.Count){throw 'Holdout query embedding count mismatch.'}
    $holdoutMap=@{}
    for($i=0;$i -lt $holdout.Count;$i++){$holdoutMap[[string]$holdout[$i].id]=$holdoutVectors[$i]}
    $lexicalHoldout=Get-Metrics $holdout @{} $chunks.ToArray() $false $threshold $weight $script:TrackRankTiming $script:RankTimes
    $hybridHoldout=Get-Metrics $holdout $holdoutMap $chunks.ToArray() $true $threshold $weight $script:TrackRankTiming $script:RankTimes
    Write-Output ('HOLDOUT lexical R5(all)={0:P2} R5(capped)={1:P2} ceiling(all)={2:P2} MRR10={3:P2} no_result_empty={4:P2}' -f $lexicalHoldout.RecallAt5,$lexicalHoldout.RecallAt5Capped,$lexicalHoldout.RecallAt5TheoreticalCeiling,$lexicalHoldout.MRRAt10,$lexicalHoldout.NoResultEmptyAccuracy)
    Write-Output ('HOLDOUT tuned hybrid threshold={0} semanticWeight={1} R5(all)={2:P2} R5(capped)={3:P2} ceiling(all)={4:P2} MRR10={5:P2} no_result_empty={6:P2}' -f $threshold,$weight,$hybridHoldout.RecallAt5,$hybridHoldout.RecallAt5Capped,$hybridHoldout.RecallAt5TheoreticalCeiling,$hybridHoldout.MRRAt10,$hybridHoldout.NoResultEmptyAccuracy)
    Write-TypeMetrics $holdout $holdoutMap $chunks.ToArray() $true $threshold $weight 'HOLDOUT_HYBRID' $script:TrackRankTiming $script:RankTimes
    Write-Output ('EmbeddingCalls={0};QueryCalls={1};QueryLatencyAvgP95Ms={2}/{3};RankLatencyAvgP95Ms={4}/{5};CPUms={6};WorkingSetMB={7:N1};HoldoutEvaluated=TRUE' -f $script:EmbeddingApiCalls,$script:QueryApiCalls,(Get-LatencySummary $script:EmbeddingCallTimes).AverageMs,(Get-LatencySummary $script:EmbeddingCallTimes).P95Ms,(Get-LatencySummary $script:RankTimes).AverageMs,(Get-LatencySummary $script:RankTimes).P95Ms,[Diagnostics.Process]::GetCurrentProcess().TotalProcessorTime.TotalMilliseconds,([Diagnostics.Process]::GetCurrentProcess().WorkingSet64/1MB))
    exit 0
}

$devQueries=@($development.query)+@($authorFixtures.query)
$script:TrackRankTiming=$true
$devQueryVectors=Resolve-CachedQueryVectors -Queries $devQueries -Path $queryCachePath -ModelName 'gemini-embedding-001' -Dimensions 768 -Cache $queryCache -BatchDelaySeconds $BatchDelaySeconds -EmbedMissing { param($batch) Get-Embeddings -texts $batch -isQuery $true }
$devVectorMap=@{}
for($i=0;$i -lt $development.Count;$i++){$devVectorMap[[string]$development[$i].id]=$devQueryVectors[$i]}
for($i=0;$i -lt $authorFixtures.Count;$i++){$devVectorMap[[string]$authorFixtures[$i].id]=$devQueryVectors[$development.Count+$i]}

$selected=Invoke-DevelopmentSelection -Development $development -AuthorFixtures $authorFixtures -Vectors $devVectorMap -Chunks $chunks.ToArray() -SelectionPath (Join-Path $RepoRoot 'obj/rag-eval-selection.json') -FixtureVersion 'RAG_EVALUATION_QUERIES_V2' -TrackTiming $script:TrackRankTiming -RankTimes $script:RankTimes
Write-Output ('SelectedDevThreshold={0};SelectedSemanticWeight={1};DocumentChunks={2};DocumentBatches={3};EmbeddingCalls={4};QueryCalls={5};EmbeddingLatencyAvgP95Ms={6}/{7};RankLatencyAvgP95Ms={8}/{9};CPUms={10};WorkingSetMB={11:N1};HoldoutEvaluated=FALSE' -f $selected.Threshold,$selected.Weight,$chunks.Count,[Math]::Ceiling($chunks.Count/16.0),$script:EmbeddingApiCalls,$script:QueryApiCalls,(Get-LatencySummary $script:EmbeddingCallTimes).AverageMs,(Get-LatencySummary $script:EmbeddingCallTimes).P95Ms,(Get-LatencySummary $script:RankTimes).AverageMs,(Get-LatencySummary $script:RankTimes).P95Ms,[Diagnostics.Process]::GetCurrentProcess().TotalProcessorTime.TotalMilliseconds,([Diagnostics.Process]::GetCurrentProcess().WorkingSet64/1MB))
Write-Output "DevelopmentSelectionSaved=$(Join-Path $RepoRoot 'obj/rag-eval-selection.json')"
exit 0

# Final holdout run: only the already-selected development parameters are evaluated here.
$holdoutVectors=Get-Embeddings -texts @($holdout.query) -isQuery $true
$holdoutMap=@{}
for($i=0;$i -lt $holdout.Count;$i++){$holdoutMap[[string]$holdout[$i].id]=$holdoutVectors[$i]}
$lexicalHoldout=Get-Metrics $holdout @{} $chunks.ToArray() $false $selected.Threshold $selected.Weight $script:TrackRankTiming $script:RankTimes
$hybridHoldout=Get-Metrics $holdout $holdoutMap $chunks.ToArray() $true $selected.Threshold $selected.Weight $script:TrackRankTiming $script:RankTimes
Write-Output ('HOLDOUT lexical Recall@5={0:P2} MRR@10={1:P2} no_result_empty={2:P2}' -f $lexicalHoldout.RecallAt5,$lexicalHoldout.MRRAt10,$lexicalHoldout.NoResultEmptyAccuracy)
Write-Output ('HOLDOUT tuned hybrid Recall@5={0:P2} MRR@10={1:P2} no_result_empty={2:P2}' -f $hybridHoldout.RecallAt5,$hybridHoldout.MRRAt10,$hybridHoldout.NoResultEmptyAccuracy)
Write-Output ('SelectedDevThreshold={0};SelectedSemanticWeight={1};DocumentChunks={2};DocumentBatches={3};QueryBatchSize=32' -f $selected.Threshold,$selected.Weight,$chunks.Count,[Math]::Ceiling($chunks.Count/32.0))
$apiKey=$null; $headers=$null
