param([string]$Server='(localdb)\MSSQLLocalDB',[string]$Database='ECommerceDb_Dev',[string]$RagInputs=(Join-Path (Split-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path -Parent) 'Marketplace-khang-ai_rag\RAG_inputs'),[switch]$DryRun)
$ErrorActionPreference='Stop'
if($Database -cne 'ECommerceDb_Dev'){throw 'Safety stop: only ECommerceDb_Dev is allowed.'}
$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if(-not ('ECommerce.Infrastructure.AI.RetrievalScoring' -as [type])){Add-Type -Path (Join-Path $repoRoot 'src/ECommerce.Infrastructure/AI/RetrievalScoring.cs')}
$fixture=Join-Path $RagInputs 'RAG_EVALUATION_QUERIES_V2.jsonl'
$rows=@(Get-Content -LiteralPath $fixture|ForEach-Object{$_|ConvertFrom-Json})
if($DryRun){
    foreach($row in $rows){$class=if(@($row.expected_product_ids).Count){'Answerable'}else{'No-result'};Write-Output ('LABEL_CHECK id={0} class={1} type={2} expected={3} matched=DRY_RUN missing=DRY_RUN extra=DRY_RUN predicate={4}' -f $row.id,$class,$row.type,@($row.expected_product_ids).Count,'NOT_EXECUTED')}
    Write-Output ('Mode=DRY_RUN;rows={0};SQL=0' -f $rows.Count);exit 0
}
$builder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new();$builder.DataSource=$Server;$builder.InitialCatalog=$Database;$builder.IntegratedSecurity=$true;$builder.ConnectTimeout=8;$builder.ApplicationIntent=[System.Data.SqlClient.ApplicationIntent]::ReadOnly
$connection=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
try{
    $connection.Open();$identity=$connection.CreateCommand();$identity.CommandText='SELECT DB_NAME() AS DatabaseName;';$actual=[string]$identity.ExecuteScalar();if($actual -cne 'ECommerceDb_Dev'){throw "Safety stop: connected to '$actual'."}
    $command=$connection.CreateCommand();$command.CommandTimeout=30;$command.CommandText=@'
SELECT p.Id,p.Name,p.Sku,p.Description,c.Name AS CategoryName,b.Name AS BrandName,
       ad.DisplayName AS AttributeName,COALESCE(av.ValueText,CONVERT(nvarchar(100),av.ValueNumber),CONVERT(nvarchar(10),av.ValueBoolean)) AS AttributeValue
FROM dbo.Products AS p
LEFT JOIN dbo.Categories AS c ON c.Id=p.CategoryId
LEFT JOIN dbo.Brands AS b ON b.Id=p.BrandId
LEFT JOIN dbo.ProductAttributeValues AS av ON av.ProductId=p.Id
LEFT JOIN dbo.AttributeDefinitions AS ad ON ad.Id=av.AttributeDefinitionId
ORDER BY p.Id,ad.DisplayName;
'@
    $reader=$command.ExecuteReader();$catalog=@{}
    while($reader.Read()){
        $id=[int]$reader['Id'];if(-not $catalog.ContainsKey($id)){$catalog[$id]=[pscustomobject]@{Id=$id;Name=[string]$reader['Name'];Sku=[string]$reader['Sku'];Description='';Category='';Brand='';Attributes=New-Object 'System.Collections.Generic.List[string]'}}
        if(-not [DBNull]::Value.Equals($reader['Description'])){$catalog[$id].Description=[string]$reader['Description']};if(-not [DBNull]::Value.Equals($reader['CategoryName'])){$catalog[$id].Category=[string]$reader['CategoryName']};if(-not [DBNull]::Value.Equals($reader['BrandName'])){$catalog[$id].Brand=[string]$reader['BrandName']};if(-not [DBNull]::Value.Equals($reader['AttributeValue'])){$catalog[$id].Attributes.Add(([string]$reader['AttributeName'])+' '+([string]$reader['AttributeValue']))}
    }
    $reader.Close();Write-Output ('Mode=READ_ONLY;Database={0};Products={1};FixtureRows={2}' -f $actual,$catalog.Count,$rows.Count)
    foreach($row in $rows){
        $expected=@($row.expected_product_ids|ForEach-Object{[int]$_});$matched=0;$missing=0;$matchedIds=New-Object 'System.Collections.Generic.List[int]';$predicateMatches=New-Object 'System.Collections.Generic.List[int]'
        foreach($p in $catalog.Values){
            $candidateBody=($p.Name+' '+$p.Sku+' '+$p.Description+' '+$p.Category+' '+$p.Brand+' '+($p.Attributes -join ' '));$candidateMatch=$false
            if($row.type -eq 'exact_sku'){$candidateSku='';$candidateMatch=[ECommerce.Infrastructure.AI.RetrievalScoring]::TryFindSkuCandidate([string]$row.query,[ref]$candidateSku) -and $candidateSku -and $p.Sku.Equals($candidateSku,[StringComparison]::OrdinalIgnoreCase)}
            else{$candidateMatch=[ECommerce.Infrastructure.AI.RetrievalScoring]::LexicalScore([string]$row.query,$candidateBody) -gt 0}
            if($candidateMatch){$predicateMatches.Add([int]$p.Id)}
        }
        foreach($id in $expected){if(-not $catalog.ContainsKey($id)){$missing++;continue};$p=$catalog[$id]
            $body=($p.Name+' '+$p.Sku+' '+$p.Description+' '+$p.Category+' '+$p.Brand+' '+($p.Attributes -join ' '))
            $predicatePass=$false
            if($row.type -eq 'exact_sku'){$skuCandidate='';$predicatePass=[ECommerce.Infrastructure.AI.RetrievalScoring]::TryFindSkuCandidate([string]$row.query,[ref]$skuCandidate) -and $skuCandidate -and $p.Sku.Equals($skuCandidate,[StringComparison]::OrdinalIgnoreCase)}
            else{$predicatePass=[ECommerce.Infrastructure.AI.RetrievalScoring]::LexicalScore([string]$row.query,$body) -gt 0}
            if($predicatePass){$matched++;$matchedIds.Add($id)}else{$missing++}
        }
        $class=if($expected.Count){'Answerable'}else{'No-result'}
        $extra=@($predicateMatches|Where-Object{$expected -notcontains $_}).Count
        $predicateStatus=if($row.type -eq 'exact_sku'){'SKU_EXACT_CHECK'}elseif($row.type -eq 'no_result'){'NO_RESULT_LEXICAL_CANDIDATE_CHECK'}else{'LEXICAL_EVIDENCE_ONLY_PREDICATE_NOT_ENCODED'}
        Write-Output ('LABEL_CHECK id={0} class={1} type={2} expected={3} matched={4} missing_or_predicate_mismatch={5} extra={6} predicate_status={7} matched_ids={8}' -f $row.id,$class,$row.type,$expected.Count,$matched,$missing,$extra,$predicateStatus,($matchedIds -join ','))
    }
    Write-Output 'Notice: input fixture has no explicit predicate field; non-SKU checks are lexical evidence diagnostics, not human validation of relevance.'
}finally{if($connection.State -ne [System.Data.ConnectionState]::Closed){$connection.Close()};$connection.Dispose()}
