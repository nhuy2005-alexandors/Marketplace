param([ValidateRange(1,16)][int]$BatchSize = 16)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$keyLine = Get-Content -LiteralPath (Join-Path $repoRoot '.env') | Where-Object { $_ -match '^\s*GEMINI_API_KEY\s*=' } | Select-Object -First 1
if (-not $keyLine) { throw 'GEMINI_API_KEY is missing from repo-root .env' }
$apiKey = ($keyLine -replace '^\s*GEMINI_API_KEY\s*=\s*','').Trim().Trim('"',"'")
if ([string]::IsNullOrWhiteSpace($apiKey)) { throw 'GEMINI_API_KEY is empty' }

$items = @()
for ($i=1; $i -le $BatchSize; $i++) { $items += "Synthetic V2 demo item ${i}: fictional category, simple catalog description." }
$requests = @($items | ForEach-Object {
    @{ model='models/gemini-embedding-001'; content=@{ parts=@(@{text=$_}) }; taskType='RETRIEVAL_DOCUMENT'; outputDimensionality=768 }
})
$payload = @{ requests=$requests } | ConvertTo-Json -Depth 8 -Compress
$client = [System.Net.Http.HttpClient]::new()
$client.Timeout = [TimeSpan]::FromSeconds(30)
$request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Post,
    'https://generativelanguage.googleapis.com/v1beta/models/gemini-embedding-001:batchEmbedContents')
[void]$request.Headers.TryAddWithoutValidation('x-goog-api-key',$apiKey)
$request.Content = [System.Net.Http.StringContent]::new($payload,[Text.Encoding]::UTF8,'application/json')
$timer = [Diagnostics.Stopwatch]::StartNew()
$status = 'none'; $failure = 'none'; $dimensions = 0; $vectors = 0
try {
    $response = $client.SendAsync($request,[System.Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
    $status = [string][int]$response.StatusCode
    if ($response.IsSuccessStatusCode) {
        $json = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
        $vectors = @($json.embeddings).Count
        if ($vectors -gt 0) { $dimensions = @($json.embeddings[0].values).Count }
    }
    $response.Dispose()
} catch {
    $exception = $_.Exception
    while ($exception.InnerException) { $exception=$exception.InnerException }
    $failure = $exception.GetType().Name
    if ($exception.PSObject.Properties.Name -contains 'Response' -and $exception.Response -and $exception.Response.StatusCode) {
        $status = [string][int]$exception.Response.StatusCode
    }
} finally {
    $timer.Stop(); $client.Dispose(); $request.Dispose(); $apiKey=$null
}
Write-Output ('BatchSize={0};HttpStatus={1};ElapsedMs={2};Vectors={3};Dimensions={4};FailureType={5}' -f $BatchSize,$status,$timer.ElapsedMilliseconds,$vectors,$dimensions,$failure)
