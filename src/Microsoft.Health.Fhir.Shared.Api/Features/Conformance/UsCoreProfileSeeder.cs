// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using EnsureThat;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Medino;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Health.Core;
using Microsoft.Health.Core.Extensions;
using Microsoft.Health.Core.Features.Context;
using Microsoft.Health.Extensions.DependencyInjection;
using Microsoft.Health.Fhir.Core;
using Microsoft.Health.Fhir.Core.Configs;
using Microsoft.Health.Fhir.Core.Extensions;
using Microsoft.Health.Fhir.Core.Features.Context;
using Microsoft.Health.Fhir.Core.Features.Operations;
using Microsoft.Health.Fhir.Core.Features.Persistence;
using Microsoft.Health.Fhir.Core.Features.Validation;
using Microsoft.Health.Fhir.Core.Messages.CapabilityStatement;
using Microsoft.Health.Fhir.Core.Models;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.Health.Fhir.Api.Features.Conformance
{
    public sealed class UsCoreProfileSeeder : IUsCoreProfileSeeder
    {
        private readonly ImplementationGuidesConfiguration _configuration;
        private readonly IScopeProvider<IFhirDataStore> _fhirDataStoreFactory;
        private readonly IResourceWrapperFactory _resourceWrapperFactory;
        private readonly FhirJsonParser _parser;
        private readonly ISupportedProfilesStore _supportedProfilesStore;
        private readonly IUsCoreProfilePackageDownloader _packageDownloader;
        private readonly RequestContextAccessor<IFhirRequestContext> _fhirRequestContextAccessor;
        private readonly IMediator _mediator;
        private readonly ILogger<UsCoreProfileSeeder> _logger;
        private readonly Func<Assembly> _embeddedAssemblyProvider;

        public UsCoreProfileSeeder(
            IOptions<ImplementationGuidesConfiguration> configuration,
            IScopeProvider<IFhirDataStore> fhirDataStoreFactory,
            IResourceWrapperFactory resourceWrapperFactory,
            FhirJsonParser parser,
            ISupportedProfilesStore supportedProfilesStore,
            IUsCoreProfilePackageDownloader packageDownloader,
            RequestContextAccessor<IFhirRequestContext> fhirRequestContextAccessor,
            IMediator mediator,
            ILogger<UsCoreProfileSeeder> logger)
            : this(
                configuration,
                fhirDataStoreFactory,
                resourceWrapperFactory,
                parser,
                supportedProfilesStore,
                packageDownloader,
                fhirRequestContextAccessor,
                mediator,
                logger,
                () => typeof(VersionSpecificModelInfoProvider).Assembly)
        {
        }

        internal UsCoreProfileSeeder(
            IOptions<ImplementationGuidesConfiguration> configuration,
            IScopeProvider<IFhirDataStore> fhirDataStoreFactory,
            IResourceWrapperFactory resourceWrapperFactory,
            FhirJsonParser parser,
            ISupportedProfilesStore supportedProfilesStore,
            IUsCoreProfilePackageDownloader packageDownloader,
            RequestContextAccessor<IFhirRequestContext> fhirRequestContextAccessor,
            IMediator mediator,
            ILogger<UsCoreProfileSeeder> logger,
            Func<Assembly> embeddedAssemblyProvider)
        {
            EnsureArg.IsNotNull(configuration?.Value, nameof(configuration));
            _configuration = configuration.Value;
            _fhirDataStoreFactory = EnsureArg.IsNotNull(fhirDataStoreFactory, nameof(fhirDataStoreFactory));
            _resourceWrapperFactory = EnsureArg.IsNotNull(resourceWrapperFactory, nameof(resourceWrapperFactory));
            _parser = EnsureArg.IsNotNull(parser, nameof(parser));
            _supportedProfilesStore = EnsureArg.IsNotNull(supportedProfilesStore, nameof(supportedProfilesStore));
            _packageDownloader = EnsureArg.IsNotNull(packageDownloader, nameof(packageDownloader));
            _fhirRequestContextAccessor = EnsureArg.IsNotNull(fhirRequestContextAccessor, nameof(fhirRequestContextAccessor));
            _mediator = EnsureArg.IsNotNull(mediator, nameof(mediator));
            _logger = EnsureArg.IsNotNull(logger, nameof(logger));
            _embeddedAssemblyProvider = EnsureArg.IsNotNull(embeddedAssemblyProvider, nameof(embeddedAssemblyProvider));
        }

        public async Task SeedAsync(CancellationToken cancellationToken)
        {
            if (!(_configuration.USCore?.AutoSeedProfiles ?? false))
            {
                _logger.LogInformation("US Core profile auto-seed is disabled.");
                return;
            }

            _logger.LogInformation("US Core profile auto-seed starting.");

            using IScoped<IFhirDataStore> scopedDataStore = _fhirDataStoreFactory.Invoke();
            IFhirDataStore dataStore = scopedDataStore.Value;

            var requiredProfileIds = GetRequiredProfileIds();
            if (await AllRequiredProfilesPresentAsync(dataStore, requiredProfileIds, cancellationToken).ConfigureAwait(false))
            {
                _logger.LogInformation("All required US Core profiles are already present; skipping seed.");
                return;
            }

            // ResourceWrapperFactory requires an IFhirRequestContext (Method/Uri). Background seeding has no HTTP request.
            IFhirRequestContext existingContext = _fhirRequestContextAccessor.RequestContext;
            _fhirRequestContextAccessor.RequestContext = CreateBackgroundRequestContext();

            try
            {
                var embeddedProfiles = LoadEmbeddedStructureDefinitions(requiredProfileIds).ToList();
                _logger.LogInformation(
                    "Loaded {EmbeddedCount} embedded US Core StructureDefinition resources (required={RequiredCount}).",
                    embeddedProfiles.Count,
                    requiredProfileIds.Count);

                if (embeddedProfiles.Count == 0)
                {
                    _logger.LogError(
                        "No embedded US Core StructureDefinitions found in assembly {AssemblyName}. Seed cannot proceed.",
                        _embeddedAssemblyProvider().GetName().Name);
                }

                var upsertedCount = 0;
                upsertedCount += await UpsertProfilesAsync(dataStore, embeddedProfiles, cancellationToken).ConfigureAwait(false);

                if (_configuration.USCore.DownloadFullPackage)
                {
                    upsertedCount += await UpsertDownloadedProfilesAsync(dataStore, cancellationToken).ConfigureAwait(false);
                }

                if (upsertedCount > 0)
                {
                    _supportedProfilesStore.Refresh();
                    await _mediator.PublishAsync(new RebuildCapabilityStatement(RebuildPart.Profiles), cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation("Seeded {UpsertedCount} US Core StructureDefinition resources and requested CapabilityStatement rebuild.", upsertedCount);
                }
                else
                {
                    _logger.LogWarning("US Core profile auto-seed completed with 0 upserts.");
                }
            }
            finally
            {
                _fhirRequestContextAccessor.RequestContext = existingContext;
            }
        }

        private static FhirRequestContext CreateBackgroundRequestContext()
        {
            return new FhirRequestContext(
                method: "PUT",
                uriString: "https://localhost/StructureDefinition",
                baseUriString: "https://localhost/",
                correlationId: Guid.NewGuid().ToString("N"),
                requestHeaders: new Dictionary<string, StringValues>(),
                responseHeaders: new Dictionary<string, StringValues>())
            {
                IsBackgroundTask = true,
                ResourceType = KnownResourceTypes.StructureDefinition,
            };
        }

        private static List<string> GetRequiredProfileIds()
        {
            return UsCoreRequiredProfiles.CanonicalUrls
                .Select(GetProfileIdFromCanonical)
                .ToList();
        }

        private static string GetProfileIdFromCanonical(string canonicalUrl)
        {
            return canonicalUrl.Substring(canonicalUrl.LastIndexOf('/') + 1);
        }

        private static async Task<bool> AllRequiredProfilesPresentAsync(
            IFhirDataStore dataStore,
            IReadOnlyList<string> requiredProfileIds,
            CancellationToken cancellationToken)
        {
            foreach (var profileId in requiredProfileIds)
            {
                var existing = await dataStore.GetAsync(new ResourceKey(KnownResourceTypes.StructureDefinition, profileId), cancellationToken).ConfigureAwait(false);
                if (existing == null)
                {
                    return false;
                }
            }

            return true;
        }

        private async Task<int> UpsertProfilesAsync(
            IFhirDataStore dataStore,
            IReadOnlyList<(string Id, string Json)> profiles,
            CancellationToken cancellationToken)
        {
            var upsertedCount = 0;
            foreach (var (id, json) in profiles)
            {
                try
                {
                    if (await UpsertStructureDefinitionIfMissingAsync(dataStore, id, json, cancellationToken).ConfigureAwait(false))
                    {
                        upsertedCount++;
                        _logger.LogInformation("Upserted US Core StructureDefinition/{ProfileId}.", id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to upsert US Core StructureDefinition/{ProfileId}.", id);
                }
            }

            return upsertedCount;
        }

        private IEnumerable<(string Id, string Json)> LoadEmbeddedStructureDefinitions(IReadOnlyCollection<string> requiredProfileIds)
        {
            var requiredSet = new HashSet<string>(requiredProfileIds, StringComparer.Ordinal);
            var assembly = _embeddedAssemblyProvider();

            foreach (var resourceName in assembly.GetManifestResourceNames())
            {
                if (!resourceName.Contains("Data.UsCore", StringComparison.Ordinal) ||
                    !resourceName.Contains("StructureDefinition-", StringComparison.Ordinal))
                {
                    continue;
                }

                // Extract id from resource name suffix: StructureDefinition-{id}.json
                const string suffixPrefix = "StructureDefinition-";
                var suffixIndex = resourceName.LastIndexOf(suffixPrefix, StringComparison.Ordinal);
                if (suffixIndex < 0)
                {
                    continue;
                }

                var idWithExtension = resourceName.Substring(suffixIndex + suffixPrefix.Length);
                if (!idWithExtension.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var id = idWithExtension.Substring(0, idWithExtension.Length - ".json".Length);
                if (!requiredSet.Contains(id))
                {
                    continue;
                }

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    continue;
                }

                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                if (!string.IsNullOrWhiteSpace(json))
                {
                    yield return (id, json);
                }
            }
        }

        private async Task<int> UpsertDownloadedProfilesAsync(IFhirDataStore dataStore, CancellationToken cancellationToken)
        {
            try
            {
                var downloadedProfiles = await _packageDownloader.DownloadStructureDefinitionsAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Downloaded {DownloadedCount} StructureDefinitions from US Core package.", downloadedProfiles.Count);
                return await UpsertProfilesAsync(dataStore, downloadedProfiles, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Full US Core package download failed; embedded profiles were retained. Primary URL: {PrimaryUrl}. Fallback URL: {FallbackUrl}.",
                    UsCoreRequiredProfiles.PackageDownloadUrl,
                    UsCoreProfilePackageDownloader.FallbackPackageDownloadUrl);
                return 0;
            }
        }

        private async Task<bool> UpsertStructureDefinitionIfMissingAsync(
            IFhirDataStore dataStore,
            string profileId,
            string json,
            CancellationToken cancellationToken)
        {
            var existing = await dataStore.GetAsync(new ResourceKey(KnownResourceTypes.StructureDefinition, profileId), cancellationToken).ConfigureAwait(false);
            if (existing != null)
            {
                return false;
            }

            var resource = (Resource)await _parser.ParseAsync(json, typeof(Resource)).ConfigureAwait(false);
            resource.Id = profileId;

            // SqlServerFhirDataStore.SyncVersionIdAndLastUpdatedInMeta requires lastUpdated in raw JSON.
            // Embedded IG profiles ship without meta; mirror ImportResourceParser for creates.
            resource.Meta ??= new Meta();
            resource.Meta.LastUpdated = Clock.UtcNow.UtcDateTime.TruncateToMillisecond();
            resource.Meta.VersionId = "1";

            var element = resource.ToResourceElement();
            var wrapper = _resourceWrapperFactory.Create(element, deleted: false, keepMeta: true);

            await dataStore.UpsertAsync(
                new ResourceWrapperOperation(
                    wrapper,
                    allowCreate: true,
                    keepHistory: true,
                    weakETag: null,
                    requireETagOnUpdate: false,
                    keepVersion: false,
                    bundleResourceContext: null),
                cancellationToken).ConfigureAwait(false);

            return true;
        }
    }
}
