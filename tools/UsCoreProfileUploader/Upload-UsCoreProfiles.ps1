<#
.SYNOPSIS
  Downloads hl7.fhir.us.core@6.1.0 and PUTs all StructureDefinition resources to a FHIR R4 server.

.PARAMETER FhirBaseUrl
  FHIR server base URL, e.g. https://fhir.optikode.com

.PARAMETER AccessToken
  Bearer token with permission to create/update StructureDefinition

.PARAMETER WorkDir
  Temp directory for package download (default: %TEMP%\uscore-6.1.0)
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string]$FhirBaseUrl,

  [Parameter(Mandatory = $true)]
  [string]$AccessToken,

  [string]$WorkDir = (Join-Path $env:TEMP "uscore-6.1.0"),

  [string]$PackageName = "hl7.fhir.us.core",

  [string]$PackageVersion = "6.1.0"
)

$ErrorActionPreference = "Stop"
$FhirBaseUrl = $FhirBaseUrl.TrimEnd("/")

New-Item -ItemType Directory -Force -Path $WorkDir | Out-Null
$tgz = Join-Path $WorkDir "$PackageName-$PackageVersion.tgz"
$extract = Join-Path $WorkDir "extract"

# Simplifier FHIR package registry (npm-compatible)
$packageUrl = "https://packages.simplifier.net/$PackageName/$PackageVersion"

Write-Host "Downloading $packageUrl ..."
Invoke-WebRequest -Uri $packageUrl -OutFile $tgz -UseBasicParsing

if (Test-Path $extract) { Remove-Item $extract -Recurse -Force }
New-Item -ItemType Directory -Force -Path $extract | Out-Null

tar -xzf $tgz -C $extract

$packageDir = Join-Path $extract "package"
if (-not (Test-Path $packageDir)) {
  throw "Expected package/ folder inside tarball not found at $packageDir"
}

$files = Get-ChildItem -Path $packageDir -Filter "StructureDefinition-*.json" -File
if ($files.Count -lt 1) {
  throw "No StructureDefinition-*.json files found in $packageDir"
}

Write-Host "Found $($files.Count) StructureDefinition files. Uploading to $FhirBaseUrl ..."

$headers = @{
  Authorization = "Bearer $AccessToken"
  Accept        = "application/fhir+json"
  "Content-Type" = "application/fhir+json"
}

$failed = @()
$ok = 0

foreach ($file in $files) {
  $json = Get-Content $file.FullName -Raw -Encoding UTF8
  $obj = $json | ConvertFrom-Json
  if ($obj.resourceType -ne "StructureDefinition") {
    Write-Warning "Skipping $($file.Name): resourceType=$($obj.resourceType)"
    continue
  }

  $id = $obj.id
  if ([string]::IsNullOrWhiteSpace($id)) {
    $failed += "$($file.Name): missing id"
    continue
  }

  $url = "$FhirBaseUrl/StructureDefinition/$([uri]::EscapeDataString($id))"
  try {
    $resp = Invoke-WebRequest -Uri $url -Method Put -Headers $headers -Body $json -UseBasicParsing
    if ($resp.StatusCode -ge 200 -and $resp.StatusCode -lt 300) {
      $ok++
      Write-Host "OK $($resp.StatusCode) $id"
    }
    else {
      $failed += "$id -> HTTP $($resp.StatusCode)"
    }
  }
  catch {
    $status = $null
    if ($_.Exception.Response) {
      $status = [int]$_.Exception.Response.StatusCode
    }
    $failed += "$id -> ERROR status=$status $($_.Exception.Message)"
  }
}

Write-Host "Uploaded $ok StructureDefinitions."
if ($failed.Count -gt 0) {
  Write-Host "Failures ($($failed.Count)):"
  $failed | ForEach-Object { Write-Host " - $_" }
  exit 1
}

Write-Host "All StructureDefinitions uploaded successfully."
exit 0
