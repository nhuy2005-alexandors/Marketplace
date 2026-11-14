$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$dbScript = Join-Path $PSScriptRoot 'db-snapshot.ps1'
$smokeScript = Join-Path $PSScriptRoot 'guest-smoke.ps1'
$listenerTest = Join-Path $PSScriptRoot 'Test-GuestSmokeHttpListener.ps1'
$probeScript = Join-Path $PSScriptRoot 'gemini-generation-probe.ps1'
$probeTest = Join-Path $PSScriptRoot 'Test-GeminiGenerationProbeHttpListener.ps1'
$powershell = (Get-Command powershell.exe).Source

foreach ($path in @($dbScript,$smokeScript,$listenerTest,$probeScript,$probeTest)) {
    $tokens=$null; $errors=$null
    [System.Management.Automation.Language.Parser]::ParseFile($path,[ref]$tokens,[ref]$errors)|Out-Null
    if ($errors.Count) { throw "PowerShell parser errors in ${path}: $($errors -join '; ')" }
}

$dbOutput = (& $powershell -NoProfile -ExecutionPolicy Bypass -File $dbScript -DryRun 2>&1 | Out-String)
if ($LASTEXITCODE -ne 0 -or -not $dbOutput.Contains('Mode=DRY_RUN;Server=FAKE-SQL-SERVER;Database=ECommerceDb_Dev') -or -not $dbOutput.Contains('Products=500')) {
    throw "DB snapshot fake dry-run failed: $dbOutput"
}

$smokeOutput = (& $powershell -NoProfile -ExecutionPolicy Bypass -File $smokeScript -DryRun 2>&1 | Out-String)
if ($LASTEXITCODE -ne 0 -or @([regex]::Matches($smokeOutput,'Case=')).Count -ne 17 -or -not $smokeOutput.Contains('Case=no-result-weak-evidence;Result=PASS;HTTP=200') -or -not $smokeOutput.Contains('Case=K-11-strict-empty;Result=KNOWN_LIMITATION') -or -not $smokeOutput.Contains('Direction D keeps nearest suggestions') -or -not $smokeOutput.Contains('Summary=PASS=16;FAIL=0;KNOWN_LIMITATION=1') -or -not $smokeOutput.Contains('Case=rate-limit-intentional;Result=PASS;HTTP=429') -or -not $smokeOutput.Contains('Case=prompt-extraction-request;Result=PASS;HTTP=200;ProductCount=0') -or -not $smokeOutput.Contains('Case=payload-too-large-413;Result=PASS;HTTP=413')) {
    throw "Guest smoke fake dry-run failed: $smokeOutput"
}
$onlyOutput=(& $powershell -NoProfile -ExecutionPolicy Bypass -File $smokeScript -DryRun -Only lexical-positive 2>&1|Out-String)
if($LASTEXITCODE -ne 0 -or -not $onlyOutput.Contains('Summary=PASS=1;FAIL=0;KNOWN_LIMITATION=0') -or -not $onlyOutput.Contains('AnswerSource=generated')){throw "Guest smoke -Only selection failed: $onlyOutput"}
$retryOutput=(& $powershell -NoProfile -ExecutionPolicy Bypass -File $smokeScript -DryRun -Only unrelated-english 2>&1|Out-String)
if($LASTEXITCODE -ne 0 -or -not $retryOutput.Contains('Summary=PASS=1;FAIL=0;KNOWN_LIMITATION=0') -or -not $retryOutput.Contains('RETRIED_AFTER_429=TRUE')){throw "Guest smoke retry marker simulation failed: $retryOutput"}
$skipOutput=(& $powershell -NoProfile -ExecutionPolicy Bypass -File $smokeScript -DryRun -Skip unrelated-english 2>&1|Out-String)
if($LASTEXITCODE -ne 0 -or -not $skipOutput.Contains('Summary=PASS=15;FAIL=0;KNOWN_LIMITATION=1') -or $skipOutput.Contains('Case=unrelated-english;')){throw "Guest smoke -Skip selection failed: $skipOutput"}
$showAnswerOutput = (& $powershell -NoProfile -ExecutionPolicy Bypass -File $smokeScript -DryRun -ShowAnswer 2>&1 | Out-String)
$syntheticSecret = ('A' + 'Q' + '.') + 'ABCDE' + 'FGHIJ' + 'KLMNO' + 'PQRST' + 'UVWXYZ' + '0123456789'
if ($LASTEXITCODE -ne 0 -or -not $showAnswerOutput.Contains('[REDACTED]') -or $showAnswerOutput.Contains($syntheticSecret)) {
    throw "Guest smoke answer redaction check failed: $showAnswerOutput"
}
$smokeSource=Get-Content -LiteralPath $smokeScript -Raw
if($smokeSource.Contains($syntheticSecret)){throw 'Guest smoke source contains the complete synthetic key sample; build it only at runtime.'}
$appSettings=Get-Content -LiteralPath (Join-Path $root 'src/ECommerce.API/appsettings.json') -Raw|ConvertFrom-Json
$optionsSource=Get-Content -LiteralPath (Join-Path $root 'src/ECommerce.Infrastructure/AI/GeminiOptions.cs') -Raw
if($appSettings.Gemini.GenerationEnabled -ne $false -or $optionsSource -notmatch 'GenerationEnabled\s*\{\s*get;\s*set;\s*\}\s*=\s*false') { throw 'Gemini answer generation must default to disabled in server config and options.' }
$probeDryRun = (& $powershell -NoProfile -ExecutionPolicy Bypass -File $probeScript -DryRun 2>&1 | Out-String)
if ($LASTEXITCODE -ne 0 -or -not $probeDryRun.Contains('KeyRead=FALSE;Network=0')) { throw "Gemini generation probe dry-run failed: $probeDryRun" }

$previousErrorActionPreference = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
$unsafeDb = (& $powershell -NoProfile -ExecutionPolicy Bypass -File $dbScript -Database 'ECommerceDb_Prod' -DryRun 2>&1 | Out-String)
$unsafeDbExitCode = $LASTEXITCODE
$unsafeHost = (& $powershell -NoProfile -ExecutionPolicy Bypass -File $smokeScript -BaseUrl 'http://example.com' -DryRun 2>&1 | Out-String)
$unsafeHostExitCode = $LASTEXITCODE
$ErrorActionPreference = $previousErrorActionPreference
if ($unsafeDbExitCode -eq 0 -or -not $unsafeDb.Contains('Safety stop')) { throw 'DB snapshot did not stop on a non-development database name.' }
if ($unsafeHostExitCode -eq 0 -or -not $unsafeHost.Contains('Safety stop')) { throw 'Guest smoke did not stop for a non-localhost target.' }

Write-Output 'LocalAcceptanceScripts=PASS;PowerShell=5.1;Parser=PASS;DbSnapshotFake=PASS;GuestSmokeFakeCases=17;K11KnownLimitation=PASS;GenerationDefaultOff=PASS;SecretSampleSource=Absent;OnlySkip=PASS;429RetryMarker=PASS;GeminiProbeDryRun=PASS;UnsafeTargetGuards=PASS;Network=0;Database=0'
