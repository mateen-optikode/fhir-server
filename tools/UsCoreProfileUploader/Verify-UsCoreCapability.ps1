[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string]$FhirBaseUrl,

  [string]$AccessToken,

  [string]$RequirementsPath = (Join-Path $PSScriptRoot "required-profiles-10.1.05.json")
)

$ErrorActionPreference = "Stop"
$FhirBaseUrl = $FhirBaseUrl.TrimEnd("/")
$req = Get-Content $RequirementsPath -Raw -Encoding UTF8 | ConvertFrom-Json

$headers = @{ Accept = "application/fhir+json" }
if ($AccessToken) {
  $headers.Authorization = "Bearer $AccessToken"
}

$metadataUrl = "$FhirBaseUrl/metadata"
Write-Host "GET $metadataUrl"
$raw = Invoke-RestMethod -Uri $metadataUrl -Headers $headers -Method Get

$missingInstantiates = @()
foreach ($i in $req.instantiates) {
  if (-not ($raw.instantiates -contains $i)) {
    $missingInstantiates += $i
  }
}

$supported = New-Object System.Collections.Generic.HashSet[string]
foreach ($res in $raw.rest.resource) {
  if ($null -eq $res.supportedProfile) { continue }
  foreach ($p in @($res.supportedProfile)) {
    [void]$supported.Add([string]$p)
  }
}

$missingProfiles = @()
foreach ($p in $req.requiredSupportedProfiles) {
  if (-not $supported.Contains($p)) {
    $missingProfiles += $p
  }
}

if ($missingInstantiates.Count -eq 0) {
  Write-Host "PASS instantiates"
}
else {
  Write-Host "FAIL instantiates missing:"
  $missingInstantiates | ForEach-Object { Write-Host " - $_" }
}

if ($missingProfiles.Count -eq 0) {
  Write-Host "PASS supportedProfile ($($req.requiredSupportedProfiles.Count) required)"
}
else {
  Write-Host "FAIL supportedProfile missing $($missingProfiles.Count):"
  $missingProfiles | ForEach-Object { Write-Host " - $_" }
}

if ($missingInstantiates.Count -gt 0 -or $missingProfiles.Count -gt 0) {
  exit 1
}

Write-Host "All Inferno 10.1 CapabilityStatement checks passed."
exit 0
