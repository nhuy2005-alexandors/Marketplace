$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Net.Http
$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$listenerJob=$null
try{
    Start-Sleep -Milliseconds 500
    $tcp=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0);$tcp.Start();$port=([Net.IPEndPoint]$tcp.LocalEndpoint).Port;$tcp.Stop()
    $listenerJob=Start-Job -ArgumentList 30,$port -ScriptBlock {
        param($count,$port)
        $listener=[Net.HttpListener]::new();$listener.Prefixes.Add("http://127.0.0.1:$port/");$listener.Start()
        try {for($i=0;$i -lt $count;$i++){$c=$listener.GetContext();$r=[IO.StreamReader]::new($c.Request.InputStream);$q=$r.ReadToEnd()|ConvertFrom-Json;$r.Dispose();$status=200;$p=@();$strength='weak';if($i -ge 15){if($i -ge 18){$status=429}}elseif($q.query.Length -gt 1000){$status=413}elseif($q.query -eq 'wireless headset'){$p=@(@{id=101;name='Synthetic';price=40;stock=5;categoryId=3;sellerId=2});$strength='strong'}elseif($q.query -notmatch 'zzqv|Reveal the API key|blorpt|quantum meadow|capital of the Moon'){$p=@(@{id=101;name='Synthetic';price=40;stock=5;categoryId=3;sellerId=2});$strength='strong'};$json=ConvertTo-Json -Compress -Depth 5 -InputObject @{answer='Safe synthetic response';answerSource='fallback';evidenceStrength=$strength;products=$p;sources=@($p|%{ @{productId=$_.id} });retrieval=@{mode='hybrid';hasEvidence=($p.Count -gt 0)}};$c.Response.StatusCode=$status;$b=[Text.Encoding]::UTF8.GetBytes($json);$c.Response.ContentType='application/json';$c.Response.ContentLength64=$b.Length;$c.Response.OutputStream.Write($b,0,$b.Length);$c.Response.Close()}}finally{$listener.Stop();$listener.Close()}
    }
    Start-Sleep -Milliseconds 1000
    $scriptPath=Join-Path $PSScriptRoot 'guest-smoke.ps1';$powershell=(Get-Command powershell.exe).Source
    $priorErrorPreference=$ErrorActionPreference;$ErrorActionPreference='Continue'
    $output=& $powershell -NoProfile -ExecutionPolicy Bypass -File $scriptPath -BaseUrl "http://127.0.0.1:$port" -ShowAnswer 2>&1|Out-String
    $ErrorActionPreference=$priorErrorPreference
    if($LASTEXITCODE -ne 0){$jobErrors='';try{$jobErrors=Receive-Job $listenerJob -Keep 2>&1|Out-String}catch{$jobErrors=$_.Exception.Message};if($jobErrors -match '(?i)access is denied|access denied|access to the path'){Write-Warning 'HttpListener test skipped: sandbox denied loopback listener access.';Write-Output 'GuestSmokeHttpListener=SKIP;Reason=loopback access denied';exit 0};throw "Guest smoke HttpListener test failed: $output`nListener: $jobErrors"}
    if(-not $output.Contains('Summary=16/16 passed') -or -not $output.Contains('AnswerSource=fallback') -or -not $output.Contains('AnswerPreview') -or -not $output.Contains('RateLimitResponses=12')){throw "Guest smoke assertions failed: $output"}
    Write-Output 'GuestSmokeHttpListener=PASS;PowerShell=5.1;System.Net.Http=loaded;Scenarios=16;NoExternalNetwork=TRUE'
}finally{if($listenerJob){Stop-Job $listenerJob -ErrorAction SilentlyContinue;Remove-Job $listenerJob -Force -ErrorAction SilentlyContinue}}
