param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$BaseUrl = 'https://generativelanguage.googleapis.com',
    [switch]$DryRun
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Net.Http
$baseUri=[Uri]$BaseUrl
if($baseUri.Scheme -ne 'https' -and -not ($baseUri.Scheme -eq 'http' -and $baseUri.Host -in @('localhost','127.0.0.1','::1'))){throw 'Safety stop: probe only permits Gemini HTTPS or a loopback fake server.'}
$settings=Get-Content -LiteralPath (Join-Path $RepoRoot 'src/ECommerce.API/appsettings.json') -Raw|ConvertFrom-Json
$configuredModel=[string]$settings.Gemini.GenerationModel
if($DryRun){Write-Output "DryRun=TRUE;ModelsList=planned;ConfiguredModel=$configuredModel;GenerationThinking=low,medium;Streaming=planned;KeyRead=FALSE;Network=0";exit 0}

$apiKey=''
if($baseUri.Host -notin @('localhost','127.0.0.1','::1')){
    $apiKey=[Environment]::GetEnvironmentVariable('GEMINI_API_KEY')
    if([string]::IsNullOrWhiteSpace($apiKey)){
        $envPath=Join-Path $RepoRoot '.env'
        if(Test-Path -LiteralPath $envPath){$line=Get-Content -LiteralPath $envPath|Where-Object{$_ -match '^\s*GEMINI_API_KEY\s*='}|Select-Object -First 1;if($line){$apiKey=($line -replace '^\s*GEMINI_API_KEY\s*=\s*','').Trim().Trim('"',"'")}}
    }
    if([string]::IsNullOrWhiteSpace($apiKey)){
        try{$secretLines=& dotnet user-secrets list --project (Join-Path $RepoRoot 'src/ECommerce.API/ECommerce.API.csproj') 2>$null;foreach($line in $secretLines){if($line -match '^\s*(?:Gemini:ApiKey|Gemini__ApiKey|GEMINI_API_KEY)\s*=\s*(.*)$'){$apiKey=$Matches[1].Trim();break}}}catch{}
    }
    if([string]::IsNullOrWhiteSpace($apiKey)){throw 'No Gemini API key source was found (environment, repo .env, or API user-secrets). The key value is never printed.'}
}
$handler=[System.Net.Http.HttpClientHandler]::new();$handler.UseProxy=$false
$client=[System.Net.Http.HttpClient]::new($handler);$client.Timeout=[TimeSpan]::FromSeconds(25)
if($apiKey){[void]$client.DefaultRequestHeaders.TryAddWithoutValidation('x-goog-api-key',$apiKey)}
$results=New-Object 'System.Collections.Generic.List[object]'
function Invoke-ProbeRequest([string]$Name,[string]$Path,[string]$Thinking,[bool]$Streaming){
    $timer=[Diagnostics.Stopwatch]::StartNew();$status=0;$errorType='none';$firstByteMs=$null
    try{
        $request=[System.Net.Http.HttpRequestMessage]::new($(if($Name -eq 'models.list'){[Net.Http.HttpMethod]::Get}else{[Net.Http.HttpMethod]::Post}),[Uri]::new($baseUri,$Path))
        if($Name -ne 'models.list'){$body=@{contents=@(@{parts=@(@{text='In one short sentence, say hello as a shopping assistant.'})});generationConfig=@{maxOutputTokens=32;thinkingConfig=@{thinkingLevel=$Thinking}}}|ConvertTo-Json -Depth 8 -Compress;$request.Content=[System.Net.Http.StringContent]::new($body,[Text.Encoding]::UTF8,'application/json')}
        $response=$client.SendAsync($request,[Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult();$status=[int]$response.StatusCode
        $stream=$response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();$buffer=New-Object byte[] 1;$read=$stream.Read($buffer,0,1);$timer.Stop();if($read -gt 0){$firstByteMs=[int]$timer.Elapsed.TotalMilliseconds}
        if(-not $Streaming -and $read -gt 0){while($stream.Read($buffer,0,1) -gt 0){}}
        if(-not $response.IsSuccessStatusCode){$errorType='http-'+$status}
        $stream.Dispose();$response.Dispose();$request.Dispose()
    }catch{$timer.Stop();$errorType=$_.Exception.GetType().Name}
    $results.Add([pscustomobject]@{Name=$Name;Status=$status;LatencyMs=[int]$timer.Elapsed.TotalMilliseconds;FirstByteMs=$firstByteMs;ErrorType=$errorType})
}
try{
    $modelsTimer=[Diagnostics.Stopwatch]::StartNew();$modelsStatus=0;$modelsError='none';$modelsPresent=$false
    try{$modelsResponse=$client.GetAsync([Uri]::new($baseUri,'v1beta/models')).GetAwaiter().GetResult();$modelsStatus=[int]$modelsResponse.StatusCode;if($modelsResponse.IsSuccessStatusCode){$modelsText=$modelsResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();$modelsJson=$modelsText|ConvertFrom-Json;$modelNames=@($modelsJson.models|ForEach-Object{[string]$_.name});$modelsPresent=@($modelNames|Where-Object{($_ -replace '^models/','') -eq $configuredModel}).Count -gt 0;foreach($modelName in $modelNames){Write-Output "Model=$modelName"}}else{$modelsError='http-'+$modelsStatus};$modelsResponse.Dispose()}catch{$modelsError=$_.Exception.GetType().Name}
    $modelsTimer.Stop();$results.Add([pscustomobject]@{Name='models.list';Status=$modelsStatus;LatencyMs=[int]$modelsTimer.Elapsed.TotalMilliseconds;FirstByteMs=$null;ErrorType=$modelsError})
    Invoke-ProbeRequest 'generate-low' ("v1beta/models/{0}:generateContent" -f $configuredModel) 'low' $false
    Invoke-ProbeRequest 'generate-medium' ("v1beta/models/{0}:generateContent" -f $configuredModel) 'medium' $false
    Invoke-ProbeRequest 'stream-medium' ("v1beta/models/{0}:streamGenerateContent?alt=sse" -f $configuredModel) 'medium' $true
    foreach($item in $results){Write-Output ('Probe={0};HTTP={1};LatencyMs={2};TimeToFirstByteMs={3};ErrorType={4}' -f $item.Name,$item.Status,$item.LatencyMs,$(if($null -eq $item.FirstByteMs){'n/a'}else{$item.FirstByteMs}),$item.ErrorType)}
    Write-Output ('Summary=probes:{0};configured_model_present={1};thinking=low,medium;streaming_ttft={2};KeyPrinted=FALSE' -f $results.Count,$modelsPresent,$(if($results[3].FirstByteMs -ne $null){'measured'}else{'unavailable'}))
}finally{$client.Dispose()}
