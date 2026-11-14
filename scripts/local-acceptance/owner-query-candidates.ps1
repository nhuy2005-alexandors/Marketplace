param([string]$Server='(localdb)\MSSQLLocalDB',[string]$Database='ECommerceDb_Dev',[switch]$DryRun)
$ErrorActionPreference='Stop'
if($Database -cne 'ECommerceDb_Dev'){throw 'Safety stop: only ECommerceDb_Dev is allowed.'}
$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$draftPath=Join-Path $repoRoot 'docs/rag-evaluation/RAG_OWNER_REVIEWED_QUERIES.jsonl'
$drafts=@(Get-Content -LiteralPath $draftPath|Where-Object{$_.Trim()}|ForEach-Object{$_|ConvertFrom-Json})
if($DryRun){foreach($draft in $drafts){Write-Output ('DRAFT id={0} scenario={1} candidate_query=SELECT-only-not-executed' -f $draft.id,$draft.scenario)};exit 0}
$builder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new();$builder.DataSource=$Server;$builder.InitialCatalog=$Database;$builder.IntegratedSecurity=$true;$builder.ConnectTimeout=8;$builder.ApplicationIntent=[System.Data.SqlClient.ApplicationIntent]::ReadOnly
$connection=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
try{
    $connection.Open();$identity=$connection.CreateCommand();$identity.CommandText='SELECT DB_NAME() AS DatabaseName;';$actual=[string]$identity.ExecuteScalar();if($actual -cne 'ECommerceDb_Dev'){throw "Safety stop: connected to '$actual'."}
    Write-Output ('Mode=READ_ONLY;Database={0};Drafts={1}' -f $actual,$drafts.Count)
    foreach($draft in $drafts){
        $terms=@(([string]$draft.query -split '[^\p{L}\p{N}]+'|Where-Object{$_.Length -ge 3}|Select-Object -Unique|Select-Object -First 8))
        $command=$connection.CreateCommand();$command.CommandTimeout=20
        $clauses=New-Object 'System.Collections.Generic.List[string]';$index=0
        foreach($term in $terms){$parameter='@term'+$index;$clauses.Add("(p.Name LIKE $parameter OR p.Sku LIKE $parameter OR c.Name LIKE $parameter OR b.Name LIKE $parameter OR av.ValueText LIKE $parameter OR ad.DisplayName LIKE $parameter)");$param=$command.Parameters.Add($parameter,[System.Data.SqlDbType]::NVarChar,200);$param.Value='%'+$term+'%';$index++}
        if($clauses.Count -eq 0){$clauses.Add('1=1')}
        $command.CommandText=@'
SELECT TOP (50) p.Id AS ProductId,p.Name,p.Sku,c.Name AS CategoryName,b.Name AS BrandName,p.Price,p.Stock,
       ad.DisplayName AS AttributeName,COALESCE(av.ValueText,CONVERT(nvarchar(100),av.ValueNumber),CONVERT(nvarchar(10),av.ValueBoolean)) AS AttributeValue
FROM dbo.Products AS p
LEFT JOIN dbo.Categories AS c ON c.Id=p.CategoryId
LEFT JOIN dbo.Brands AS b ON b.Id=p.BrandId
LEFT JOIN dbo.ProductAttributeValues AS av ON av.ProductId=p.Id
LEFT JOIN dbo.AttributeDefinitions AS ad ON ad.Id=av.AttributeDefinitionId
WHERE __CLAUSES__
ORDER BY p.Id,ad.DisplayName;
'@ -replace '__CLAUSES__',($clauses -join ' OR ')
        Write-Output ('QUERY id={0} scenario={1} text={2}' -f $draft.id,$draft.scenario,(ConvertTo-Json -InputObject $draft.query -Compress))
        $reader=$command.ExecuteReader();$seen=@{}
        while($reader.Read()){$pid=[int]$reader['ProductId'];if(-not $seen.ContainsKey($pid)){$seen[$pid]=$true;$attrs=New-Object 'System.Collections.Generic.List[string]'};if(-not [DBNull]::Value.Equals($reader['AttributeValue'])){$attrs.Add(([string]$reader['AttributeName'])+'='+([string]$reader['AttributeValue']))};Write-Output ('CANDIDATE id={0} name={1} sku={2} category={3} brand={4} price={5} stock={6} attributes={7}' -f $pid,$reader['Name'],$reader['Sku'],$reader['CategoryName'],$reader['BrandName'],$reader['Price'],$reader['Stock'],($attrs -join ';'))}
        $reader.Close();$command.Dispose()
    }
}finally{if($connection.State -ne [System.Data.ConnectionState]::Closed){$connection.Close()};$connection.Dispose()}
