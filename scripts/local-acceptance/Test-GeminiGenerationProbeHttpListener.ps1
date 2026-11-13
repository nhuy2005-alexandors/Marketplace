$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Net.Http
$listenerJob=$null
try {
    $tcp=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0);$tcp.Start();$port=([Net.IPEndPoint]$tcp.LocalEndpoint).Port;$tcp.Stop()
    $listenerJob=Start-Job -ArgumentList $port -ScriptBlock {
        param($port)
        $listener=[Net.HttpListener]::new();$listener.Prefixes.Add("http://127.0.0.1:$port/");$listener.Start()
        try {
            for($i=0;$i -lt 4;$i++){
                $context=$listener.GetContext();$path=$context.Request.Url.AbsolutePath
                if($path -eq '/v1beta/models'){$payload='{"models":[{"name":"models/gemini-3.8-flash"}]}'}
                else{$payload='{"candidates":[{"content":{"parts":[{"text":"ok"}]}}]}'}
                if($path -like '*streamGenerateContent*'){$payload="data: $payload`n`n"}
                $bytes=[Text.Encoding]::UTF8.GetBytes($payload);$context.Response.StatusCode=200;$context.Response.ContentType='application/json';$context.Response.ContentLength64=$bytes.Length;$context.Response.OutputStream.Write($bytes,0,$bytes.Length);$context.Response.Close()
            }
        } finally {$listener.Stop();$listener.Close()}
    }
    Start-Sleep -Milliseconds 500
    $scriptPath=Join-Path $PSScriptRoot 'gemini-generation-probe.ps1';$powershell=(Get-Command powershell.exe).Source
    $previous=$ErrorActionPreference;$ErrorActionPreference='Continue'
    $output=& $powershell -NoProfile -ExecutionPolicy Bypass -File $scriptPath -BaseUrl "http://127.0.0.1:$port" 2>&1 | Out-String
    $ErrorActionPreference=$previous
    $passed=$LASTEXITCODE -eq 0 -and $output.Contains('Probe=models.list;HTTP=200') -and $output.Contains('Probe=generate-low;HTTP=200') -and $output.Contains('Probe=generate-medium;HTTP=200') -and $output.Contains('Probe=stream-medium;HTTP=200') -and $output.Contains('configured_model_present=True') -and $output -notmatch '(?i)AIza|AQ\.[A-Za-z0-9_-]{20,}'
    if(-not $passed){$jobErrors='';try{$jobErrors=Receive-Job $listenerJob -Keep 2>&1|Out-String}catch{$jobErrors=$_.Exception.Message};if($jobErrors -match '(?i)access is denied|access denied|access to the path'){Write-Warning 'Gemini probe HttpListener test skipped: sandbox denied loopback listener access.';Write-Output 'GeminiGenerationProbeHttpListener=SKIP;Reason=loopback access denied';exit 0};throw "Gemini generation probe fake-server test failed: $output`nListener: $jobErrors"}
    Write-Output 'GeminiGenerationProbeHttpListener=PASS;PowerShell=5.1;System.Net.Http=loaded;Requests=4;KeyPrinted=FALSE'
} finally {if($listenerJob){Stop-Job $listenerJob -ErrorAction SilentlyContinue;Remove-Job $listenerJob -Force -ErrorAction SilentlyContinue}}
