param(
    [string]$Server = '(localdb)\MSSQLLocalDB',
    [string]$Database = 'ECommerceDb_Dev',
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
if ($Database -cne 'ECommerceDb_Dev') { throw 'Safety stop: only ECommerceDb_Dev is an allowed target.' }

if ($DryRun) {
    $fake = [pscustomobject]@{
        Server = 'FAKE-SQL-SERVER'
        Database = 'ECommerceDb_Dev'
        Counts = [ordered]@{ __EFMigrationsHistory=11; Products=500; ProductAttributeValues=3000; Users=1020; Orders=5000; RefreshTokens=0 }
    }
    Write-Output ('Mode=DRY_RUN;Server={0};Database={1}' -f $fake.Server,$fake.Database)
    foreach ($name in $fake.Counts.Keys) { Write-Output ('{0}={1}' -f $name,$fake.Counts[$name]) }
    exit 0
}

$builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new()
$builder.DataSource = $Server
$builder.InitialCatalog = $Database
$builder.IntegratedSecurity = $true
$builder.ConnectTimeout = 8
$connection = [System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
try {
    $connection.Open()
    $identity = $connection.CreateCommand()
    $identity.CommandText = 'SELECT CAST(SERVERPROPERTY(''ServerName'') AS nvarchar(128)) AS ServerName, DB_NAME() AS DatabaseName;'
    $reader = $identity.ExecuteReader()
    if (-not $reader.Read()) { throw 'Safety stop: SQL Server did not return target identity.' }
    $serverName = [string]$reader['ServerName']
    $actualDatabase = [string]$reader['DatabaseName']
    $reader.Close()
    if ($actualDatabase -cne 'ECommerceDb_Dev') { throw "Safety stop: connected database is '$actualDatabase', expected ECommerceDb_Dev." }
    Write-Output ('Mode=READ_ONLY;Server={0};Database={1}' -f $serverName,$actualDatabase)

    $counts = $connection.CreateCommand()
    $counts.CommandText = @'
SELECT N'__EFMigrationsHistory' AS TableName, COUNT_BIG(*) AS RowCount FROM dbo.__EFMigrationsHistory
UNION ALL SELECT N'Products', COUNT_BIG(*) FROM dbo.Products
UNION ALL SELECT N'ProductAttributeValues', COUNT_BIG(*) FROM dbo.ProductAttributeValues
UNION ALL SELECT N'Users', COUNT_BIG(*) FROM dbo.Users
UNION ALL SELECT N'Orders', COUNT_BIG(*) FROM dbo.Orders
UNION ALL SELECT N'RefreshTokens', COUNT_BIG(*) FROM dbo.RefreshTokens;
'@
    $rows = $counts.ExecuteReader()
    while ($rows.Read()) { Write-Output ('{0}={1}' -f [string]$rows['TableName'],[long]$rows['RowCount']) }
    $rows.Close()
}
finally {
    if ($connection.State -ne [System.Data.ConnectionState]::Closed) { $connection.Close() }
    $connection.Dispose()
}
