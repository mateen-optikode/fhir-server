# US Core 6.1.0 Capability Statement tooling (Inferno 10.1)

## Prerequisites

1. Deploy a build of microsoft-fhir-server that includes `UsCoreCapabilityProvider`.
2. Obtain an access token allowed to `PUT StructureDefinition` (and preferably read `/metadata`).

## Upload profiles (10.1.05)

```powershell
.\Upload-UsCoreProfiles.ps1 `
  -FhirBaseUrl "https://fhir.optikode.com" `
  -AccessToken "<token>"
```

Wait ~30–60s for profile cache / CapabilityStatement rebuild if needed, then verify.

## Verify (10.1.05 + 10.1.06)

```powershell
.\Verify-UsCoreCapability.ps1 `
  -FhirBaseUrl "https://fhir.optikode.com" `
  -AccessToken "<token>"
```

Exit code 0 means `/metadata` has:

- `instantiates` containing `http://hl7.org/fhir/us/core/CapabilityStatement/us-core-server`
- all canonicals listed in `required-profiles-10.1.05.json` under some `rest.resource.supportedProfile`

## Inferno

Re-run Inferno suite group **10.1 Capability Statement** only against `https://fhir.optikode.com`.
