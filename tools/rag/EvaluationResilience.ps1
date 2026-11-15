function ConvertTo-SafeQuotaLabel([object]$Value) {
    if ($null -eq $Value) { return '' }
    $text = [string]$Value
    if ($text.Length -gt 160 -or $text -notmatch '^[A-Za-z0-9][A-Za-z0-9._:/=-]*$') { return '' }
    return $text
}

function Get-GeminiQuotaDiagnostic([int]$HttpStatus, [string]$Body) {
    $providerStatus = ''
    $quotaMetric = ''
    $quotaId = ''
    $dimensionParts = New-Object 'System.Collections.Generic.List[string]'
    $retryDelaySeconds = $null

    try {
        $root = $Body | ConvertFrom-Json -ErrorAction Stop
        $errorObject = $root.error
        if ($errorObject) {
            $providerStatus = ConvertTo-SafeQuotaLabel $errorObject.status
            foreach ($detail in @($errorObject.details)) {
                if (-not $detail) { continue }
                if ($detail.retryDelay -match '^([0-9]+(?:\.[0-9]+)?)s$') {
                    $retryDelaySeconds = [double]::Parse($Matches[1], [Globalization.CultureInfo]::InvariantCulture)
                }
                foreach ($violation in @($detail.violations)) {
                    if (-not $violation) { continue }
                    if (-not $quotaMetric) { $quotaMetric = ConvertTo-SafeQuotaLabel $violation.quotaMetric }
                    if (-not $quotaId) { $quotaId = ConvertTo-SafeQuotaLabel $violation.quotaId }
                    if ($violation.quotaDimensions) {
                        foreach ($property in $violation.quotaDimensions.PSObject.Properties) {
                            $key = ConvertTo-SafeQuotaLabel $property.Name
                            $value = ConvertTo-SafeQuotaLabel $property.Value
                            if ($key -and $value -and $dimensionParts.Count -lt 8) { $dimensionParts.Add("$key=$value") }
                        }
                    }
                }
            }
        }
    }
    catch { }

    $classification = 'unknown'
    $labels = "$quotaMetric $quotaId $($dimensionParts -join ' ')"
    if ($labels -match '(?i)(per[_ -]?day|daily|quota[_ -]?day|day[_ -]?limit)') { $classification = 'per-day' }
    elseif ($labels -match '(?i)(per[_ -]?minute|minutely|minute[_ -]?limit)') { $classification = 'per-minute' }

    [pscustomobject]@{
        HttpStatus = $HttpStatus
        Status = $providerStatus
        QuotaMetric = $quotaMetric
        QuotaId = $quotaId
        QuotaDimensions = $dimensionParts -join ';'
        RetryDelaySeconds = $retryDelaySeconds
        Classification = $classification
    }
}

function Get-HttpErrorBody([object]$Exception) {
    try {
        $response = $Exception.Response
        if (-not $response) { return '' }
        if ($response.Content) { return $response.Content.ReadAsStringAsync().GetAwaiter().GetResult() }
        $stream = $response.GetResponseStream()
        if (-not $stream) { return '' }
        $reader = New-Object IO.StreamReader($stream)
        try { return $reader.ReadToEnd() }
        finally { $reader.Dispose(); $stream.Dispose() }
    }
    catch { return '' }
}

function Get-Quota429StopReason([object]$Diagnostic, [int]$CompletedLong429Waits) {
    if ($Diagnostic.HttpStatus -ne 429) { return '' }
    if ($Diagnostic.Classification -eq 'per-day') { return 'possible-daily-quota' }
    if ($CompletedLong429Waits -ge 2) { return 'possible-daily-quota' }
    return ''
}

function Get-QueryVectorCacheKey([string]$Query, [string]$ModelName, [int]$Dimensions) {
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try { $hash = [BitConverter]::ToString($sha256.ComputeHash([Text.Encoding]::UTF8.GetBytes($Query))).Replace('-', '') }
    finally { $sha256.Dispose() }
    return "$hash`:$ModelName`:$Dimensions"
}

function Read-QueryVectorCache([string]$Path, [string]$ModelName, [int]$Dimensions) {
    $entries = @{}
    if (-not [IO.File]::Exists($Path)) { return $entries }
    try {
        $loadedEntries = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -ErrorAction Stop
        foreach ($entry in $loadedEntries) {
            $expected = "$($entry.contentHash):$ModelName`:$Dimensions"
            if ($entry.modelName -eq $ModelName -and [int]$entry.dimensions -eq $Dimensions -and
                $entry.cacheKey -eq $expected -and @($entry.vector).Count -eq $Dimensions) {
                $entries[$expected] = $entry
            }
        }
    }
    catch { return @{} }
    return $entries
}

function Resolve-CachedQueryVectors([string[]]$Queries, [string]$Path, [string]$ModelName, [int]$Dimensions,
    [hashtable]$Cache, [scriptblock]$EmbedMissing, [int]$BatchDelaySeconds = 0) {
    $missing = New-Object 'System.Collections.Generic.List[object]'
    $seen = @{}
    foreach ($query in $Queries) {
        $key = Get-QueryVectorCacheKey $query $ModelName $Dimensions
        if (-not $seen.ContainsKey($key)) {
            $seen[$key] = $true
            if (-not $Cache.ContainsKey($key)) { $missing.Add([pscustomobject]@{ Key = $key; Query = $query }) }
        }
    }

    for ($offset = 0; $offset -lt $missing.Count; $offset += 16) {
        if ($offset -gt 0 -and $BatchDelaySeconds -gt 0) {
            Write-Host "QueryEmbeddingBatchWait=$([Math]::Floor($offset/16)+1)|DelaySeconds=$BatchDelaySeconds"
            Start-Sleep -Seconds $BatchDelaySeconds
        }
        $batch = @($missing | Select-Object -Skip $offset -First 16)
        $vectors = & $EmbedMissing ([string[]]@($batch | ForEach-Object Query))
        if ($vectors.Count -ne $batch.Count) { throw 'Query embedding count did not match request count.' }
        foreach ($index in 0..($batch.Count - 1)) {
            $item = $batch[$index]
            $Cache[$item.Key] = [pscustomobject]@{
                cacheKey = $item.Key
                contentHash = ($item.Key -split ':')[0]
                modelName = $ModelName
                dimensions = $Dimensions
                vector = [single[]]($vectors[$index])
            }
        }
        $json = ConvertTo-Json -InputObject @($Cache.Values) -Depth 5
        Write-AtomicCacheFile -Path $Path -Content $json
        Write-Host "QueryCacheBatchSaved=$([Math]::Floor($offset/16)+1)|Count=$($batch.Count)|TotalEntries=$($Cache.Count)"
    }

    $result = New-Object 'System.Collections.Generic.List[object]'
    foreach ($query in $Queries) {
        $key = Get-QueryVectorCacheKey $query $ModelName $Dimensions
        $result.Add([single[]]($Cache[$key].vector))
    }
    return ,$result.ToArray()
}
