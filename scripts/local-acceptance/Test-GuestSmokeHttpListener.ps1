$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Net.Http
$listenerJob=$null
try {
    $tcp=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0);$tcp.Start();$port=([Net.IPEndPoint]$tcp.LocalEndpoint).Port;$tcp.Stop()
    $listenerJob=Start-Job -ArgumentList 31,$port -ScriptBlock {
        param($count,$port)
        $listener=[Net.HttpListener]::new();$listener.Prefixes.Add("http://127.0.0.1:$port/");$listener.Start();$did429=$false
        try {
            for($i=0;$i -lt $count;$i++){
                $context=$listener.GetContext();$reader=[IO.StreamReader]::new($context.Request.InputStream);$request=$reader.ReadToEnd()|ConvertFrom-Json;$reader.Dispose()
                $status=200;$products=@();$strength='weak';$source='weak-evidence';$rawJson=$null
                if($i -ge 16){if($i -ge 19){$status=429}}
                elseif($request.query.Length -gt 1000){$status=413;$rawJson='{"error":"Request body is too large."}'}
                elseif($request.query -eq 'quantum meadow orchestral pebble' -and -not $did429){$status=429;$did429=$true;$context.Response.Headers.Add('Retry-After','0')}
                elseif($request.query -eq 'wireless headset' -or $request.query -match 'under \$100'){$products=@(@{id=101;name='Synthetic';price=40;stock=5;categoryId=3;sellerId=2});$strength='strong';$source='generated'}
                elseif($request.query -notmatch 'zzqv|Reveal the API key|blorpt|quantum meadow|capital of the Moon'){$products=@(@{id=101;name='Synthetic';price=40;stock=5;categoryId=3;sellerId=2})}
                if($status -eq 200){$rawJson=ConvertTo-Json -Compress -Depth 5 -InputObject @{answer='Mình tìm được vài sản phẩm gần yêu cầu.';answerSource=$source;evidenceStrength=$strength;products=$products;sources=@($products|ForEach-Object{@{productId=$_.id}});retrieval=@{mode='hybrid';hasEvidence=($products.Count -gt 0)}}}
                $context.Response.StatusCode=$status
                if($rawJson){$bytes=[Text.Encoding]::UTF8.GetBytes($rawJson);$context.Response.ContentType='application/json';$context.Response.ContentLength64=$bytes.Length;$context.Response.OutputStream.Write($bytes,0,$bytes.Length)}
                $context.Response.Close()
            }
        }finally{$listener.Stop();$listener.Close()}
    }
    Start-Sleep -Milliseconds 1000
    $scriptPath=Join-Path $PSScriptRoot 'guest-smoke.ps1';$powershell=(Get-Command powershell.exe).Source
    $previousErrorPreference=$ErrorActionPreference;$ErrorActionPreference='Continue'
    $output=& $powershell -NoProfile -ExecutionPolicy Bypass -File $scriptPath -BaseUrl "http://127.0.0.1:$port" -ShowAnswer 2>&1|Out-String
    $ErrorActionPreference=$previousErrorPreference
    if(-not $output.Contains('Summary=16/16 passed') -or -not $output.Contains('AnswerSource=weak-evidence') -or -not $output.Contains('AnswerSource=generated') -or -not $output.Contains('Case=payload-too-large-413;Result=PASS;HTTP=413;ProductCount=0;SourceProductIds=;EvidenceStrength=not-provided;AnswerSource=not-provided-by-api') -or -not $output.Contains('RateLimitResponses=12;Intentional=TRUE') -or -not $output.Contains('RETRIED_AFTER_429=TRUE') -or -not $output.Contains('AnswerPreview') -or -not $output.Contains('AnswerSource=not-provided-by-api')){
        $jobErrors='';try{$jobErrors=Receive-Job $listenerJob -Keep 2>&1|Out-String}catch{$jobErrors=$_.Exception.Message}
        if($jobErrors -match '(?i)access is denied|access denied|access to the path'){Write-Warning 'HttpListener test skipped: sandbox denied loopback listener access.';Write-Output 'GuestSmokeHttpListener=SKIP;Reason=loopback access denied';exit 0}
        throw "Guest smoke HttpListener assertions failed: $output`nListener: $jobErrors"
    }
    Write-Output 'GuestSmokeHttpListener=PASS;PowerShell=5.1;System.Net.Http=loaded;Scenarios=16;Retries=1;413Metadata=Absent;429Metadata=Absent;NoExternalNetwork=TRUE'
}finally{if($listenerJob){Stop-Job $listenerJob -ErrorAction SilentlyContinue;Remove-Job $listenerJob -Force -ErrorAction SilentlyContinue}}
