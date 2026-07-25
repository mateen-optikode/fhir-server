# Task 3 Report: UsCoreProfileSeeder + optional package downloader

## Status

**DONE**

## Summary

Implemented `UsCoreProfileSeeder` with idempotent embedded StructureDefinition upsert via `IFhirDataStore` + `IResourceWrapperFactory` (import-style path, no MediatR), optional full-package download via `UsCoreProfilePackageDownloader`, and `ISupportedProfilesStore.Refresh()` after any upsert.

## Files Created

| File | Purpose |
|------|---------|
| `src/Microsoft.Health.Fhir.Shared.Api/Features/Conformance/IUsCoreProfileSeeder.cs` | Seeder contract |
| `src/Microsoft.Health.Fhir.Shared.Api/Features/Conformance/IUsCoreProfilePackageDownloader.cs` | Downloader contract |
| `src/Microsoft.Health.Fhir.Shared.Api/Features/Conformance/UsCoreProfileSeeder.cs` | Embedded seed + optional download upsert |
| `src/Microsoft.Health.Fhir.Shared.Api/Features/Conformance/UsCoreProfilePackageDownloader.cs` | HTTP GET + tar.gz extract |
| `src/Microsoft.Health.Fhir.Shared.Api.UnitTests/Features/Conformance/UsCoreProfileSeederTests.cs` | 4 TDD unit tests |

## Files Modified

| File | Change |
|------|--------|
| `src/Microsoft.Health.Fhir.Shared.Api/Microsoft.Health.Fhir.Shared.Api.projitems` | Compile includes for seeder/downloader |
| `src/Microsoft.Health.Fhir.Shared.Api.UnitTests/Microsoft.Health.Fhir.Shared.Api.UnitTests.projitems` | Compile include for seeder tests |

## Design Notes

### Upsert path

Uses direct datastore upsert (same pattern as import):

```csharp
var resource = parser.Parse<Resource>(json);
var element = resource.ToResourceElement();
var wrapper = resourceWrapperFactory.Create(element, deleted: false, keepMeta: true);
await fhirDataStore.UpsertAsync(
    new ResourceWrapperOperation(wrapper, allowCreate: true, keepHistory: true, weakETag: null, requireETagOnUpdate: false, keepVersion: false, bundleResourceContext: null),
    cancellationToken);
```

### Idempotency

- `AutoSeedProfiles=false` → immediate return, no datastore access.
- For each required profile id, `GetAsync(ResourceKey("StructureDefinition", id))`; if all present → skip seed and package download.
- Per-profile upsert only when `GetAsync` returns null.

### Embedded resources

Loaded from R4.Core assembly (`VersionSpecificModelInfoProvider.Assembly`) where manifest name contains `Data.UsCore` and `StructureDefinition-`, filtered to the 21 required ids from `UsCoreRequiredProfiles.CanonicalUrls`.

### Package download URLs

| Role | URL |
|------|-----|
| Primary | `https://packages.simplifier.net/hl7.fhir.us.core/6.1.0` (`UsCoreRequiredProfiles.PackageDownloadUrl`) |
| Fallback | `https://packages.fhir.org/hl7.fhir.us.core/6.1.0` (`UsCoreProfilePackageDownloader.FallbackPackageDownloadUrl`) |

Downloader tries primary first; on HTTP/DNS failure logs warning and tries fallback. Full-package failures are caught in seeder (warning logged, embedded seed retained).

### Scoped datastore

Uses `IScopeProvider<IFhirDataStore>` per existing bulk/delete patterns.

## Unit Tests

```powershell
dotnet test src\Microsoft.Health.Fhir.Api.UnitTests\Microsoft.Health.Fhir.R4.Api.UnitTests.csproj `
  --filter "FullyQualifiedName~UsCoreProfileSeederTests" `
  -f net9.0 /p:TreatWarningsAsErrors=false
```

**Result:** PASS — 4 passed, 0 failed.

| Test | Behavior verified |
|------|-------------------|
| AutoSeedProfiles disabled | No scoped datastore, no upsert, no refresh |
| All required present | No upsert, no refresh, no download |
| Missing required profile | Upsert ≥1 embedded, Refresh called |
| Download throws | No throw, Refresh after embedded upsert, warning logged |

## Commit

```
feat: add US Core profile seeder with optional full package download
```

## Verification Checklist

| Item | Status |
|------|--------|
| TDD tests written first | Yes |
| Import-style upsert (not MediatR) | Yes |
| Embedded 21 profiles from R4.Core | Yes |
| Optional full package with fallback URL | Yes |
| Refresh after upserts | Yes |
| `101.sql` untouched | Yes |

## Concerns / Follow-ups

1. **Task 4 wiring:** Register `IUsCoreProfileSeeder`, `IUsCoreProfilePackageDownloader`, and `UsCoreProfileSeedHostedService` in `Startup.cs`.
2. **Simplifier DNS:** Same as Task 2 — primary URL may fail; fallback to `packages.fhir.org` is implemented.
3. **Downloader scope:** Upserts all StructureDefinitions from the package tarball, not only the 21 Inferno-required set (intentional for `DownloadFullPackage`).
