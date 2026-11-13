$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$dbScript = Join-Path $PSScriptRoot 'db-snapshot.ps1'
$smokeScript = Join-Path $PSScriptRoot 'guest-smoke.ps1'
$powershell = (Get-Command powershell.exe).Source

foreach ($path in @($dbScript,$smokeScript)) {
    $tokens=$null; $errors=$null
    [System.Management.Automation.Language.Parser]::ParseFile($path,[ref]$tokens,[ref]$errors)|Out-Null
    if ($errors.Count) { throw "PowerShell parser errors in ${path}: $($errors -join '; ')" }
}

$dbOutput = (& $powershell -NoProfile -ExecutionPolicy Bypass -File $dbScript -DryRun 2>&1 | Out-String)
if ($LASTEXITCODE -ne 0 -or -not $dbOutput.Contains('Mode=DRY_RUN;Server=FAKE-SQL-SERVER;Database=ECommerceDb_Dev') -or -not $dbOutput.Contains('Products=500')) {
    throw "DB snapshot fake dry-run failed: $dbOutput"
}

$smokeOutput = (& $powershell -NoProfile -ExecutionPolicy Bypass -File $smokeScript -DryRun 2>&1 | Out-String)
if ($LASTEXITCODE -ne 0 -or @([regex]::Matches($smokeOutput,'Case=')).Count -ne 10 -or -not $smokeOutput.Contains('Case=prompt-extraction-request;HTTP=200;ProductCount=0')) {
    throw "Guest smoke fake dry-run failed: $smokeOutput"
}

$previousErrorActionPreference = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
$unsafeDb = (& $powershell -NoProfile -ExecutionPolicy Bypass -File $dbScript -Database 'ECommerceDb_Prod' -DryRun 2>&1 | Out-String)
$unsafeDbExitCode = $LASTEXITCODE
$unsafeHost = (& $powershell -NoProfile -ExecutionPolicy Bypass -File $smokeScript -BaseUrl 'http://example.com' -DryRun 2>&1 | Out-String)
$unsafeHostExitCode = $LASTEXITCODE
$ErrorActionPreference = $previousErrorActionPreference
if ($unsafeDbExitCode -eq 0 -or -not $unsafeDb.Contains('Safety stop')) { throw 'DB snapshot did not stop on a non-development database name.' }
if ($unsafeHostExitCode -eq 0 -or -not $unsafeHost.Contains('Safety stop')) { throw 'Guest smoke did not stop for a non-localhost target.' }

Write-Output 'LocalAcceptanceScripts=PASS;PowerShell=5.1;Parser=PASS;DbSnapshotFake=PASS;GuestSmokeFakeCases=10;UnsafeTargetGuards=PASS;Network=0;Database=0'
