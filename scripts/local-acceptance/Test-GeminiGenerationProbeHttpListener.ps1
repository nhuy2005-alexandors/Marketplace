$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Net.Http
$googleKeyPrefix='AI'+'za';$alternateKeyPrefix='A'+'Q'+'.';$keyPrefixPattern=[regex]::Escape($googleKeyPrefix)+'|'+[regex]::Escape($alternateKeyPrefix)
$dryRun=& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'gemini-generation-probe.ps1') -DryRun -Models 'gemini-3.8-flash,gemini-2.5-flash' -Thinking low 2>&1|Out-String
if($LASTEXITCODE -ne 0 -or -not $dryRun.Contains('Models=gemini-3.8-flash,gemini-2.5-flash;ModelCount=2') -or -not $dryRun.Contains('Thinking=low') -or -not $dryRun.Contains('KeyRead=FALSE;Network=0')){throw "Gemini probe option dry-run failed: $dryRun"}
$listenerJob=$null
try {
    $tcp=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0);$tcp.Start();$port=([Net.IPEndPoint]$tcp.LocalEndpoint).Port;$tcp.Stop()
    $listenerJob=Start-Job -ArgumentList $port -ScriptBlock {
        param($port)
        $listener=[Net.HttpListener]::new();$listener.Prefixes.Add("http://127.0.0.1:$port/");$listener.Start()
        try {
            for($i=0;$i -lt 4;$i++){
                $context=$listener.GetContext();$path=$context.Request.Url.AbsolutePath;$postedBody=''
                if($context.Request.HttpMethod -eq 'POST'){$reader=[IO.StreamReader]::new($context.Request.InputStream);$postedBody=$reader.ReadToEnd();$reader.Dispose()}
                if($path -eq '/v1beta/models'){$status=200;$payload='{"models":[{"name":"models/gemini-3.8-flash"}]}' }
                elseif($path -eq '/v1beta/models/gemini-3.8-flash'){$status=200;$payload='{"name":"models/gemini-3.8-flash","supportedGenerationMethods":["generateContent","streamGenerateContent"],"inputTokenLimit":8192,"outputTokenLimit":2048}' }
                elseif($path -match ':generateContent$'){$status=429;$payload='{"error":{"status":"RESOURCE_EXHAUSTED","message":"private-provider-body-sentinel","details":[{"@type":"type.googleapis.com/google.rpc.QuotaFailure","violations":[{"quotaMetric":"generativelanguage.googleapis.com/generateRequestsPerMinute","quotaId":"GenerateRequestsPerMinutePerProject"}]},{"@type":"type.googleapis.com/google.rpc.RetryInfo","retryDelay":"1s"}]}}' }
                else{$status=200;$payload='data: {"candidates":[{"content":{"parts":[{"text":"ok"}]}}]}' + "`n`n"}
                if($path -match ':generateContent$'){$posted=$postedBody|ConvertFrom-Json;if($posted.generationConfig.PSObject.Properties.Name -contains 'thinkingConfig'){$status=500;$payload='{"error":"thinkingConfig should be omitted"}'} }
                $bytes=[Text.Encoding]::UTF8.GetBytes($payload);$context.Response.StatusCode=$status;$context.Response.ContentType=$(if($path -like '*streamGenerateContent*'){'text/event-stream'}else{'application/json'});$context.Response.ContentLength64=$bytes.Length
                if($path -like '*streamGenerateContent*'){$context.Response.OutputStream.Write($bytes,0,1);$context.Response.OutputStream.Flush();Start-Sleep -Milliseconds 60;$context.Response.OutputStream.Write($bytes,1,$bytes.Length-1)}else{$context.Response.OutputStream.Write($bytes,0,$bytes.Length)}
                $context.Response.Close()
            }
        }finally{$listener.Stop();$listener.Close()}
    }
    Start-Sleep -Milliseconds 500
    $scriptPath=Join-Path $PSScriptRoot 'gemini-generation-probe.ps1';$output=& powershell -NoProfile -ExecutionPolicy Bypass -File $scriptPath -BaseUrl "http://127.0.0.1:$port" -Model gemini-3.8-flash -Thinking none -InterRequestDelaySeconds 0 2>&1|Out-String
    $passed=$LASTEXITCODE -eq 0 -and $output.Contains('ModelCapability model=gemini-3.8-flash;listed=True;HTTP=200;supportedGenerationMethods=generateContent,streamGenerateContent;inputTokenLimit=8192;outputTokenLimit=2048') -and $output.Contains('Probe=generateContent;Model=gemini-3.8-flash;HTTP=429') -and $output.Contains('QuotaMetric=generativelanguage.googleapis.com/generateRequestsPerMinute') -and $output.Contains('QuotaId=GenerateRequestsPerMinutePerProject') -and $output.Contains('RetryDelay=1s') -and $output.Contains('QuotaClass=per-minute') -and $output.Contains('Probe=streamGenerateContent;Model=gemini-3.8-flash;HTTP=200') -and $output -notmatch ('private-provider-body-sentinel|'+$keyPrefixPattern+'|thinkingConfig should be omitted')
    if(-not $passed){$jobErrors='';try{$jobErrors=Receive-Job $listenerJob -Keep 2>&1|Out-String}catch{$jobErrors=$_.Exception.Message};if($jobErrors -match '(?i)access is denied|access denied|access to the path'){Write-Warning 'Gemini probe HttpListener test skipped: sandbox denied loopback listener access.';Write-Output 'GeminiGenerationProbeHttpListener=SKIP;Reason=loopback access denied';exit 0};throw "Gemini generation probe fake-server assertions failed: $output`nListener: $jobErrors"}
    Write-Output 'GeminiGenerationProbeHttpListener=PASS;PowerShell=5.1;System.Net.Http=loaded;Requests=4;QuotaRedaction=PASS;ThinkingNone=PASS;KeyPrinted=FALSE'
}finally{if($listenerJob){Stop-Job $listenerJob -ErrorAction SilentlyContinue;Remove-Job $listenerJob -Force -ErrorAction SilentlyContinue}}
