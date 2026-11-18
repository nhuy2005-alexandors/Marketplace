param(
    [Parameter(Mandatory = $true)][string]$LocalConfigPath,
    [string]$DotnetPath = "dotnet",
    [string]$SecretDirectory = (Join-Path $env:LOCALAPPDATA "Marketplace\AI"),
    [string]$Urls = "http://localhost:5215"
)
$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$apiDirectory = Join-Path $repoRoot "src\ECommerce.API"
$configuration = Get-Content -LiteralPath $LocalConfigPath -Raw | ConvertFrom-Json
$connection = $configuration.ConnectionStrings.DefaultConnection
if ($connection -notmatch '(?i)Server=\(localdb\)\\MarketplaceRefundDev20261113(?:;|$)' -or
    $connection -notmatch '(?i)Database=ECommerceDb_Dev(?:;|$)') {
    throw "The local configuration must target MarketplaceRefundDev20261113 / ECommerceDb_Dev."
}
function Set-ChildConfiguration($node, [string]$prefix = "") {
    foreach ($property in $node.PSObject.Properties) {
        $name = if ($prefix) { "${prefix}__$($property.Name)" } else { $property.Name }
        if ($property.Value -is [System.Management.Automation.PSCustomObject]) {
            Set-ChildConfiguration $property.Value $name
        } elseif ($property.Value -is [array]) {
            for ($index = 0; $index -lt $property.Value.Count; $index++) {
                [Environment]::SetEnvironmentVariable("${name}__$index", [string]$property.Value[$index], "Process")
            }
        } elseif ($null -ne $property.Value) {
            [Environment]::SetEnvironmentVariable($name, [string]$property.Value, "Process")
        }
    }
}
Set-ChildConfiguration $configuration
$embeddingPath = Join-Path $SecretDirectory "embedding.local.json"
if (Test-Path -LiteralPath $embeddingPath) {
    $embedding = Get-Content -LiteralPath $embeddingPath -Raw | ConvertFrom-Json
    $env:Gemini__ApiKey = $embedding.ApiKey
}
$generationPath = Join-Path $SecretDirectory "gemini.local.json"
if (Test-Path -LiteralPath $generationPath) {
    $generation = Get-Content -LiteralPath $generationPath -Raw | ConvertFrom-Json
    $env:Gemini__GenerationApiKey = $generation.ApiKey
    $env:Gemini__GenerationBaseUrl = $generation.BaseUrl
}
$env:Gemini__GenerationEnabled = "true"
$env:Gemini__GenerationTimeoutSeconds = "45"
$env:Gemini__EmbeddingCachePath = Join-Path $SecretDirectory "hybrid-embedding-cache.json"
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = $Urls
Push-Location $apiDirectory
try {
    & $DotnetPath "bin\Debug\net9.0\ECommerce.API.dll"
    exit $LASTEXITCODE
} finally { Pop-Location }
