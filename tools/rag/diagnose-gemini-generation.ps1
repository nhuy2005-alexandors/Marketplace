$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$keyLine = Get-Content -LiteralPath (Join-Path $repoRoot '.env') | Where-Object { $_ -match '^\s*GEMINI_API_KEY\s*=' } | Select-Object -First 1
if (-not $keyLine) { throw 'GEMINI_API_KEY is missing from repo-root .env' }
$apiKey = ($keyLine -replace '^\s*GEMINI_API_KEY\s*=\s*','').Trim().Trim('"',"'")
if ([string]::IsNullOrWhiteSpace($apiKey)) { throw 'GEMINI_API_KEY is empty' }
$base = 'https://generativelanguage.googleapis.com/v1beta'
$client = [System.Net.Http.HttpClient]::new()
$client.Timeout = [TimeSpan]::FromSeconds(60)

function New-Request([string]$method,[string]$uri,[string]$jsonBody=$null) {
    $req=[System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new($method),$uri)
    [void]$req.Headers.TryAddWithoutValidation('x-goog-api-key',$apiKey)
    if($null -ne $jsonBody){$req.Content=[System.Net.Http.StringContent]::new($jsonBody,[Text.Encoding]::UTF8,'application/json')}
    return $req
}

try {
    $listTimer=[Diagnostics.Stopwatch]::StartNew()
    try {
        $listRequest=New-Request 'GET' "$base/models?pageSize=1000"
        $listResponse=$client.SendAsync($listRequest).GetAwaiter().GetResult()
        $listTimer.Stop()
        Write-Output ('ModelsListStatus={0};ElapsedMs={1}' -f [int]$listResponse.StatusCode,$listTimer.ElapsedMilliseconds)
        if($listResponse.IsSuccessStatusCode){
            $modelJson=$listResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult()|ConvertFrom-Json
            foreach($model in $modelJson.models){Write-Output $model.name}
            $hasModel=@($modelJson.models|Where-Object name -eq 'models/gemini-3.8-flash').Count -gt 0
            Write-Output "Gemini38FlashListed=$hasModel"
        }
        $listResponse.Dispose();$listRequest.Dispose()
    } catch {
        $listTimer.Stop();$failure=$_.Exception.GetType().Name
        Write-Output ('ModelsListStatus=none;ElapsedMs={0};FailureType={1}' -f $listTimer.ElapsedMilliseconds,$failure)
    }

    $payloadForLevel = {
        param([string]$level)
        return (@{
            systemInstruction=@{parts=@(@{text='Answer briefly. Return JSON only.'})}
            contents=@(@{role='user';parts=@(@{text='Synthetic demo evidence: item 1 is a travel mug. Reply with a compact JSON answer.'})})
            generationConfig=@{maxOutputTokens=48;responseMimeType='application/json';thinkingConfig=@{thinkingLevel=$level}}
        }|ConvertTo-Json -Depth 8 -Compress)
    }

    foreach($level in @('low','medium')){
        $timer=[Diagnostics.Stopwatch]::StartNew();$status='none';$failure='none'
        try{
            $body=&$payloadForLevel $level
            $request=New-Request 'POST' "$base/models/gemini-3.8-flash:generateContent" $body
            $response=$client.SendAsync($request,[System.Net.Http.HttpCompletionOption]::ResponseContentRead).GetAwaiter().GetResult()
            $status=[string][int]$response.StatusCode
            $null=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            $response.Dispose();$request.Dispose()
        }catch{$failure=$_.Exception.GetType().Name}
        $timer.Stop()
        Write-Output ('GenerationThinking={0};HttpStatus={1};ElapsedMs={2};FailureType={3}' -f $level,$status,$timer.ElapsedMilliseconds,$failure)
    }

    $timer=[Diagnostics.Stopwatch]::StartNew();$status='none';$firstByte='none';$failure='none'
    try{
        $body=&$payloadForLevel 'low'
        $request=New-Request 'POST' "$base/models/gemini-3.8-flash:streamGenerateContent?alt=sse" $body
        $response=$client.SendAsync($request,[System.Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
        $status=[string][int]$response.StatusCode
        if($response.IsSuccessStatusCode){
            $stream=$response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();$buffer=New-Object byte[] 1
            $read=$stream.ReadAsync($buffer,0,1).GetAwaiter().GetResult()
            if($read -gt 0){$firstByte=[string]$timer.ElapsedMilliseconds}else{$firstByte='eof'}
            $stream.Dispose()
        }
        $response.Dispose();$request.Dispose()
    }catch{$failure=$_.Exception.GetType().Name;if($failure -eq 'TaskCanceledException'){$firstByte='timeout'}}
    $timer.Stop()
    Write-Output ('GenerationStreamingThinking=low;HttpStatus={0};FirstByteMs={1};ElapsedMs={2};FailureType={3}' -f $status,$firstByte,$timer.ElapsedMilliseconds,$failure)
} finally {
    $client.Dispose();$apiKey=$null
}
