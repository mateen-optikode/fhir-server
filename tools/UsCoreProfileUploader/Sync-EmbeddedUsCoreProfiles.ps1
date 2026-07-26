<#
.SYNOPSIS
  Downloads hl7.fhir.us.core@6.1.0 and copies Inferno-required StructureDefinitions
  into src/Microsoft.Health.Fhir.R4.Core/Data/UsCore/6.1.0/ for embedding.

.PARAMETER WorkDir
  Temp directory for package download (default: %TEMP%\uscore-embed-6.1.0)
#>
[CmdletBinding()]
param(
  [string]$WorkDir = (Join-Path $env:TEMP "uscore-embed-6.1.0"),

  [string]$PackageName = "hl7.fhir.us.core",

  [string]$PackageVersion = "6.1.0"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$outputDir = Join-Path $repoRoot "src\Microsoft.Health.Fhir.R4.Core\Data\UsCore\6.1.0"
$requiredJsonPath = Join-Path $PSScriptRoot "required-profiles-10.1.05.json"

if (-not (Test-Path $requiredJsonPath)) {
  throw "Required profile list not found: $requiredJsonPath"
}

$requiredConfig = Get-Content $requiredJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
$requiredUrls = @($requiredConfig.requiredSupportedProfiles)
if ($requiredUrls.Count -lt 1) {
  throw "No requiredSupportedProfiles entries in $requiredJsonPath"
}

$requiredSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($url in $requiredUrls) {
  [void]$requiredSet.Add($url)
}

Write-Host "Required profiles: $($requiredSet.Count)"

New-Item -ItemType Directory -Force -Path $WorkDir | Out-Null
$tgz = Join-Path $WorkDir "$PackageName-$PackageVersion.tgz"
$extract = Join-Path $WorkDir "extract"

$packageUrl = "https://packages.simplifier.net/$PackageName/$PackageVersion"
$fallbackPackageUrl = "https://packages.fhir.org/$PackageName/$PackageVersion"

Write-Host "Downloading $packageUrl ..."
try {
  Invoke-WebRequest -Uri $packageUrl -OutFile $tgz -UseBasicParsing
}
catch {
  Write-Warning "Primary download failed ($($_.Exception.Message)). Trying $fallbackPackageUrl ..."
  Invoke-WebRequest -Uri $fallbackPackageUrl -OutFile $tgz -UseBasicParsing
}

if (Test-Path $extract) { Remove-Item $extract -Recurse -Force }
New-Item -ItemType Directory -Force -Path $extract | Out-Null

tar -xzf $tgz -C $extract

$packageDir = Join-Path $extract "package"
if (-not (Test-Path $packageDir)) {
  throw "Expected package/ folder inside tarball not found at $packageDir"
}

if (Test-Path $outputDir) {
  Get-ChildItem -Path $outputDir -Filter "StructureDefinition-*.json" -File | Remove-Item -Force
}
else {
  New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
}

$foundUrls = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$copied = 0

$files = Get-ChildItem -Path $packageDir -Filter "StructureDefinition-*.json" -File
foreach ($file in $files) {
  $json = Get-Content $file.FullName -Raw -Encoding UTF8
  $obj = $json | ConvertFrom-Json
  if ($obj.resourceType -ne "StructureDefinition") {
    continue
  }

  $url = [string]$obj.url
  if ([string]::IsNullOrWhiteSpace($url)) {
    continue
  }

  if (-not $requiredSet.Contains($url)) {
    continue
  }

  Copy-Item -Path $file.FullName -Destination (Join-Path $outputDir $file.Name) -Force
  [void]$foundUrls.Add($url)
  $copied++
  Write-Host "Copied $($file.Name) ($url)"
}

$missing = @($requiredSet | Where-Object { -not $foundUrls.Contains($_) })
if ($missing.Count -gt 0) {
  Write-Host "Missing required canonical URLs in package ($($missing.Count)):"
  $missing | ForEach-Object { Write-Host " - $_" }
  exit 1
}

Write-Host "Copied $copied StructureDefinition files to $outputDir"
if ($copied -ne $requiredSet.Count) {
  throw "Expected $($requiredSet.Count) files but copied $copied"
}

exit 0
