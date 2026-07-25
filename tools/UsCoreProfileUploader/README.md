# US Core 6.1.0 Capability Statement tooling (Inferno 10.1)

## Primary path: auto-seed on startup

Deploy a build that includes `UsCoreCapabilityProvider` and `UsCoreProfileSeedHostedService`. On first start (empty StructureDefinition store), the server automatically upserts embedded US Core 6.1 profiles and refreshes `/metadata` — no manual upload required for Inferno 10.1.

Configuration (`FhirServer:ImplementationGuides:USCore`):

```json
"ImplementationGuides": {
  "USCore": {
    "AutoSeedProfiles": true,
    "DownloadFullPackage": true
  }
}
```

| Property | Default | Meaning |
|----------|---------|---------|
| `AutoSeedProfiles` | `true` | Run startup seed after FHIR storage is ready |
| `DownloadFullPackage` | `true` | After embedded seed, try downloading `hl7.fhir.us.core@6.1.0` from Simplifier |

**Expected logs (fresh DB):**

- `UsCoreProfileSeedHostedService begin.`
- Seed upsert / skip messages from `UsCoreProfileSeeder`
- `UsCoreProfileSeedHostedService end.`

On restart with profiles already present, the seeder skips quietly (idempotent).

Seed failures are logged at Error level but do **not** crash the host.

## Fallback: manual PowerShell upload

Use the scripts below only when:

- `AutoSeedProfiles` is set to `false`, or
- Auto-seed failed and you need to repair profiles manually

### Prerequisites

1. Deploy a build of microsoft-fhir-server that includes `UsCoreCapabilityProvider`.
2. Obtain an access token allowed to `PUT StructureDefinition` (and preferably read `/metadata`).

### Upload profiles (10.1.05)

```powershell
.\Upload-UsCoreProfiles.ps1 `
  -FhirBaseUrl "https://fhir.optikode.com" `
  -AccessToken "<token>"
```

Wait ~30–60s for profile cache / CapabilityStatement rebuild if needed, then verify.

## Verify (10.1.05 + 10.1.06)

Works for both auto-seed and manual upload:

```powershell
.\Verify-UsCoreCapability.ps1 `
  -FhirBaseUrl "https://fhir.optikode.com" `
  -AccessToken "<token>"
```

Exit code 0 means `/metadata` has:

- `instantiates` containing `http://hl7.org/fhir/us/core/CapabilityStatement/us-core-server`
- all canonicals listed in `required-profiles-10.1.05.json` under some `rest.resource.supportedProfile`

## Inferno

Re-run Inferno suite group **10.1 Capability Statement** only against your FHIR base URL.
