param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$BaseUrl = 'https://generativelanguage.googleapis.com',
    [string]$Model,
    [string[]]$Models,
    [ValidateSet('none','low','medium')][string]$Thinking,
    [ValidateRange(0,120)][int]$InterRequestDelaySeconds = 15,
    [switch]$DryRun
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Net.Http
$baseUri=[Uri]$BaseUrl
if($baseUri.Scheme -ne 'https' -and -not ($baseUri.Scheme -eq 'http' -and $baseUri.Host -in @('localhost','127.0.0.1','::1'))){throw 'Safety stop: probe only permits Gemini HTTPS or a loopback fake server.'}
$settings=Get-Content -LiteralPath (Join-Path $RepoRoot 'src/ECommerce.API/appsettings.json') -Raw|ConvertFrom-Json
$configuredModel=([string]$settings.Gemini.GenerationModel -replace '^models/','')
if($Model -and $Models){throw 'Use either -Model or -Models, not both.'}
$selectedModels=if($Model){@($Model)}elseif($Models){@($Models)}else{@($configuredModel)}
$selectedModels=@($selectedModels|ForEach-Object{([string]$_).Trim() -replace '^models/',''}|Where-Object{$_})
if(-not $selectedModels.Count){throw 'At least one model must be selected.'}
$effectiveThinking=if($PSBoundParameters.ContainsKey('Thinking')){$Thinking}elseif([string]::IsNullOrWhiteSpace([string]$settings.Gemini.GenerationThinkingLevel)){'none'}else{[string]$settings.Gemini.GenerationThinkingLevel}
if($effectiveThinking -notin @('none','low','medium')){throw 'Configured GenerationThinkingLevel must be none, low, or medium.'}
if($DryRun){Write-Output ('DryRun=TRUE;ModelsList=planned;Models={0};ConfiguredModel={1};Thinking={2};BetweenGenerationTestsSeconds={3};TimeoutSeconds=25;KeyRead=FALSE;Network=0' -f ($selectedModels -join ','),$configuredModel,$effectiveThinking,$InterRequestDelaySeconds);exit 0}

$apiKey=''
if($baseUri.Host -notin @('localhost','127.0.0.1','::1')){
    $apiKey=[Environment]::GetEnvironmentVariable('GEMINI_API_KEY')
    if([string]::IsNullOrWhiteSpace($apiKey)){$envPath=Join-Path $RepoRoot '.env';if(Test-Path -LiteralPath $envPath){$line=Get-Content -LiteralPath $envPath|Where-Object{$_ -match '^\s*GEMINI_API_KEY\s*='}|Select-Object -First 1;if($line){$apiKey=($line -replace '^\s*GEMINI_API_KEY\s*=\s*','').Trim().Trim('"',"'")}}}
    if([string]::IsNullOrWhiteSpace($apiKey)){try{$secretLines=& dotnet user-secrets list --project (Join-Path $RepoRoot 'src/ECommerce.API/ECommerce.API.csproj') 2>$null;foreach($line in $secretLines){if($line -match '^\s*(?:Gemini:ApiKey|Gemini__ApiKey|GEMINI_API_KEY)\s*=\s*(.*)$'){$apiKey=$Matches[1].Trim();break}}}catch{}}
    if([string]::IsNullOrWhiteSpace($apiKey)){throw 'No Gemini API key source was found (environment, repo .env, or API user-secrets). The key value is never printed.'}
}

function ConvertTo-SafeLabel([object]$Value){if($null -eq $Value){return ''};$text=[string]$Value;if($text.Length -gt 160 -or $text -notmatch '^[A-Za-z0-9][A-Za-z0-9._:/=-]*$'){return ''};return $text}
function Get-Quota429Fields([string]$Body){
    $statusText='';$metric='';$quotaId='';$retry=''
    try{$root=$Body|ConvertFrom-Json -ErrorAction Stop;$err=$root.error;if($err){$statusText=ConvertTo-SafeLabel $err.status;foreach($detail in @($err.details)){if(-not $detail){continue};if($detail.retryDelay -match '^([0-9]+(?:\.[0-9]+)?s)$'){$retry=$Matches[1]};foreach($violation in @($detail.violations)){if(-not $violation){continue};if(-not $metric){$metric=ConvertTo-SafeLabel $violation.quotaMetric};if(-not $quotaId){$quotaId=ConvertTo-SafeLabel $violation.quotaId}}}}}catch{}
    $labels="$metric $quotaId";$class='unknown';if($labels -match '(?i)(per[_ -]?day|daily|quota[_ -]?day|day[_ -]?limit)'){$class='per-day'}elseif($labels -match '(?i)(per[_ -]?minute|minutely|minute[_ -]?limit)'){$class='per-minute'}
    return [pscustomobject]@{Status=$statusText;QuotaMetric=$metric;QuotaId=$quotaId;RetryDelay=$retry;Classification=$class}
}

$handler=[System.Net.Http.HttpClientHandler]::new();$handler.UseProxy=$false
$client=[System.Net.Http.HttpClient]::new($handler);$client.Timeout=[Threading.Timeout]::InfiniteTimeSpan
if($apiKey){[void]$client.DefaultRequestHeaders.TryAddWithoutValidation('x-goog-api-key',$apiKey)}
function Invoke-ProbeRequest([string]$Name,[string]$Method,[string]$Path,[object]$Payload){
    $timer=[Diagnostics.Stopwatch]::StartNew();$status=0;$errorType='none';$firstByteMs=$null;$body='';$response=$null;$request=$null;$cts=[Threading.CancellationTokenSource]::new();$cts.CancelAfter([TimeSpan]::FromSeconds(25))
    try{
        $httpMethod=if($Method -eq 'Get'){[System.Net.Http.HttpMethod]::Get}else{[System.Net.Http.HttpMethod]::Post}
        $request=[System.Net.Http.HttpRequestMessage]::new($httpMethod,[Uri]::new($baseUri,$Path))
        if($null -ne $Payload){$json=ConvertTo-Json -InputObject $Payload -Depth 8 -Compress;$request.Content=[System.Net.Http.StringContent]::new($json,[Text.Encoding]::UTF8,'application/json')}
        $response=$client.SendAsync($request,[Net.Http.HttpCompletionOption]::ResponseHeadersRead,$cts.Token).GetAwaiter().GetResult();$status=[int]$response.StatusCode
        $stream=$response.Content.ReadAsStreamAsync($cts.Token).GetAwaiter().GetResult();$buffer=New-Object byte[] 4096;$memory=[IO.MemoryStream]::new();$read=$stream.ReadAsync($buffer,0,$buffer.Length,$cts.Token).GetAwaiter().GetResult();$timer.Stop();if($read -gt 0){$firstByteMs=[int]$timer.Elapsed.TotalMilliseconds;$memory.Write($buffer,0,$read)}
        while($read -gt 0){$read=$stream.ReadAsync($buffer,0,$buffer.Length,$cts.Token).GetAwaiter().GetResult();if($read -gt 0){$memory.Write($buffer,0,$read)}}
        $body=[Text.Encoding]::UTF8.GetString($memory.ToArray());$memory.Dispose();$stream.Dispose();if(-not $response.IsSuccessStatusCode){$errorType='http-'+$status}
    }catch{$timer.Stop();$errorType=$_.Exception.GetBaseException().GetType().Name}
    finally{if($response){$response.Dispose()};if($request){$request.Dispose()};$cts.Dispose()}
    return [pscustomobject]@{Name=$Name;HttpStatus=$status;LatencyMs=[int]$timer.Elapsed.TotalMilliseconds;TimeToFirstByteMs=$firstByteMs;ErrorType=$errorType;Body=$body}
}

$results=New-Object 'System.Collections.Generic.List[object]';$modelsListed=@()
try{
    $listResult=Invoke-ProbeRequest 'models.list' 'Get' 'v1beta/models' $null
    if($listResult.HttpStatus -eq 200){try{$listJson=$listResult.Body|ConvertFrom-Json;$modelsListed=@($listJson.models|ForEach-Object{([string]$_.name -replace '^models/','')});foreach($listed in $modelsListed){$safe=ConvertTo-SafeLabel $listed;if($safe){Write-Output "Model=$safe"}}}catch{}}
    Write-Output ('Probe=models.list;HTTP={0};LatencyMs={1};TimeToFirstByteMs={2};ErrorType={3}' -f $listResult.HttpStatus,$listResult.LatencyMs,$(if($null -eq $listResult.TimeToFirstByteMs){'n/a'}else{$listResult.TimeToFirstByteMs}),$listResult.ErrorType)
    $lastGenerationAttempt=$null
    foreach($modelName in $selectedModels){
        $safeModel=ConvertTo-SafeLabel $modelName;if(-not $safeModel){Write-Output 'ModelCapability=invalid-model-label';continue}
        $metadata=Invoke-ProbeRequest "models.get:$safeModel" 'Get' ("v1beta/models/{0}" -f [Uri]::EscapeDataString($safeModel)) $null
        $methods='';$inputLimit='n/a';$outputLimit='n/a'
        if($metadata.HttpStatus -eq 200){try{$modelJson=$metadata.Body|ConvertFrom-Json;$methods=@($modelJson.supportedGenerationMethods|ForEach-Object{ConvertTo-SafeLabel $_}|Where-Object{$_}) -join ',';$inputLimit=if($null -ne $modelJson.inputTokenLimit){[string]$modelJson.inputTokenLimit}else{'n/a'};$outputLimit=if($null -ne $modelJson.outputTokenLimit){[string]$modelJson.outputTokenLimit}else{'n/a'}}catch{}}
        Write-Output ('ModelCapability model={0};listed={1};HTTP={2};supportedGenerationMethods={3};inputTokenLimit={4};outputTokenLimit={5}' -f $safeModel,($modelName -in $modelsListed),$metadata.HttpStatus,$methods,$inputLimit,$outputLimit)
        foreach($kind in @('generate','stream')){
            if($null -ne $lastGenerationAttempt -and $InterRequestDelaySeconds -gt 0){$elapsed=([DateTimeOffset]::UtcNow-$lastGenerationAttempt).TotalSeconds;$remaining=$InterRequestDelaySeconds-$elapsed;if($remaining -gt 0){Write-Output ('ProbePacing waitSeconds={0:N1} before={1}:{2}' -f $remaining,$safeModel,$kind);Start-Sleep -Milliseconds ([int][Math]::Ceiling($remaining*1000))}}
            $generationConfig=@{maxOutputTokens=32};if($effectiveThinking -ne 'none'){$generationConfig.thinkingConfig=@{thinkingLevel=$effectiveThinking}}
            $payload=@{contents=@(@{parts=@(@{text='Say hello in one short sentence.'})});generationConfig=$generationConfig}
            $path=if($kind -eq 'generate'){"v1beta/models/$safeModel`:generateContent"}else{"v1beta/models/$safeModel`:streamGenerateContent?alt=sse"}
            $name=if($kind -eq 'generate'){'generateContent'}else{'streamGenerateContent'}
            $probe=Invoke-ProbeRequest $name 'Post' $path $payload;$lastGenerationAttempt=[DateTimeOffset]::UtcNow
            $quota=if($probe.HttpStatus -eq 429){Get-Quota429Fields $probe.Body}else{[pscustomobject]@{Status='';QuotaMetric='';QuotaId='';RetryDelay='';Classification='n/a'}}
            Write-Output ('Probe={0};Model={1};HTTP={2};LatencyMs={3};TimeToFirstByteMs={4};ErrorType={5};Status={6};QuotaMetric={7};QuotaId={8};RetryDelay={9};QuotaClass={10};Thinking={11}' -f $name,$safeModel,$probe.HttpStatus,$probe.LatencyMs,$(if($null -eq $probe.TimeToFirstByteMs){'n/a'}else{$probe.TimeToFirstByteMs}),$probe.ErrorType,$quota.Status,$quota.QuotaMetric,$quota.QuotaId,$quota.RetryDelay,$quota.Classification,$effectiveThinking)
            $results.Add($probe)
        }
    }
    Write-Output ('Summary=models:{0};generationAttempts:{1};thinking:{2};interRequestDelaySeconds:{3};timeoutSeconds:25;KeyPrinted=FALSE' -f $selectedModels.Count,$results.Count,$effectiveThinking,$InterRequestDelaySeconds)
}finally{$client.Dispose()}
