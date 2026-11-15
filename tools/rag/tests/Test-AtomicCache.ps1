$ErrorActionPreference = 'Stop'
. (Join-Path (Split-Path $PSScriptRoot -Parent) 'AtomicJsonCache.ps1')

$testRoot = Join-Path $PSScriptRoot ('temp-' + [Guid]::NewGuid().ToString('N'))
$cachePath = Join-Path $testRoot 'cache.json'

try {
    Write-AtomicCacheFile -Path $cachePath -Content (@{ items = @(1) } | ConvertTo-Json -Compress)
    Write-AtomicCacheFile -Path $cachePath -Content (@{ items = @(1, 2) } | ConvertTo-Json -Compress)
    Write-AtomicCacheFile -Path $cachePath -Content (@{ items = @(1, 2, 3) } | ConvertTo-Json -Compress)

    $loaded = [IO.File]::ReadAllText($cachePath) | ConvertFrom-Json
    if (@($loaded.items).Count -ne 3) { throw 'Cache does not contain all three writes.' }
    if (@($loaded.items) -join ',' -ne '1,2,3') { throw 'Cache content differs from the last complete write.' }

    $original = [IO.File]::ReadAllText($cachePath)
    $missingTemp = Join-Path $testRoot 'missing.tmp'
    $failedAsExpected = $false
    try {
        Replace-AtomicCacheFile -SourcePath $missingTemp -DestinationPath $cachePath
    }
    catch {
        $failedAsExpected = $true
    }

    if (-not $failedAsExpected) { throw 'Expected atomic replace with a missing temporary file to fail.' }
    if ([IO.File]::ReadAllText($cachePath) -cne $original) { throw 'Cache changed after the simulated replace failure.' }
    if (Get-ChildItem -LiteralPath $testRoot -Filter '*.tmp' -ErrorAction SilentlyContinue) { throw 'Unexpected temporary file remains.' }

    Write-Output 'AtomicCacheTest=PASS;PowerShell=5.1;Writes=3;ReplaceFailurePreservedOldCache=TRUE'
}
finally {
    if (Test-Path -LiteralPath $testRoot) { Remove-Item -LiteralPath $testRoot -Recurse -Force }
}
