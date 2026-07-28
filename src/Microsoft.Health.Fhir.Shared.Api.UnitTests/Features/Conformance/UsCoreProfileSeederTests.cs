// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Medino;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Health.Core.Features.Context;
using Microsoft.Health.Extensions.DependencyInjection;
using Microsoft.Health.Fhir.Api.Features.Conformance;
using Microsoft.Health.Fhir.Core;
using Microsoft.Health.Fhir.Core.Configs;
using Microsoft.Health.Fhir.Core.Extensions;
using Microsoft.Health.Fhir.Core.Features.Context;
using Microsoft.Health.Fhir.Core.Features.Operations;
using Microsoft.Health.Fhir.Core.Features.Persistence;
using Microsoft.Health.Fhir.Core.Features.Validation;
using Microsoft.Health.Fhir.Core.Messages.CapabilityStatement;
using Microsoft.Health.Fhir.Core.Models;
using Microsoft.Health.Fhir.Tests.Common;
using Microsoft.Health.Test.Utilities;
using NSubstitute;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.Health.Fhir.Api.UnitTests.Features.Conformance
{
    [Trait(Traits.OwningTeam, OwningTeam.Fhir)]
    [Trait(Traits.Category, Categories.Conformance)]
    public class UsCoreProfileSeederTests
    {
        private readonly IScopeProvider<IFhirDataStore> _fhirDataStoreFactory = Substitute.For<IScopeProvider<IFhirDataStore>>();
        private readonly IFhirDataStore _fhirDataStore = Substitute.For<IFhirDataStore>();
        private readonly IScoped<IFhirDataStore> _scopedFhirDataStore = Substitute.For<IScoped<IFhirDataStore>>();
        private readonly IResourceWrapperFactory _resourceWrapperFactory = Substitute.For<IResourceWrapperFactory>();
        private readonly ISupportedProfilesStore _supportedProfilesStore = Substitute.For<ISupportedProfilesStore>();
        private readonly IUsCoreProfilePackageDownloader _packageDownloader = Substitute.For<IUsCoreProfilePackageDownloader>();
        private readonly RequestContextAccessor<IFhirRequestContext> _fhirRequestContextAccessor = Substitute.For<RequestContextAccessor<IFhirRequestContext>>();
        private readonly IMediator _mediator = Substitute.For<IMediator>();
        private readonly FhirJsonParser _parser = new FhirJsonParser();
        private readonly ILogger<UsCoreProfileSeeder> _logger = NullLogger<UsCoreProfileSeeder>.Instance;

        public UsCoreProfileSeederTests()
        {
            _scopedFhirDataStore.Value.Returns(_fhirDataStore);
            _fhirDataStoreFactory.Invoke().Returns(_scopedFhirDataStore);
        }

        [Fact]
        public async Task GivenAutoSeedProfilesDisabled_WhenSeedAsync_ThenNoDatastoreCalls()
        {
            var seeder = CreateSeeder(autoSeedProfiles: false);

            await seeder.SeedAsync(CancellationToken.None);

            _fhirDataStoreFactory.DidNotReceive().Invoke();
            await _fhirDataStore.DidNotReceive().GetAsync(Arg.Any<ResourceKey>(), Arg.Any<CancellationToken>());
            await _fhirDataStore.DidNotReceiveWithAnyArgs().UpsertAsync(default, default);
            _supportedProfilesStore.DidNotReceive().Refresh();
        }

        [Fact]
        public async Task GivenAllRequiredProfilesPresent_WhenSeedAsync_ThenNoUpsertOrRefresh()
        {
            var seeder = CreateSeeder(autoSeedProfiles: true, downloadFullPackage: true);
            SetupAllRequiredProfilesPresent();

            await seeder.SeedAsync(CancellationToken.None);

            await _fhirDataStore.DidNotReceiveWithAnyArgs().UpsertAsync(default, default);
            _supportedProfilesStore.DidNotReceive().Refresh();
            await _packageDownloader.DidNotReceiveWithAnyArgs().DownloadStructureDefinitionsAsync(default);
        }

        [Fact]
        public async Task GivenMissingRequiredProfile_WhenSeedAsync_ThenUpsertsEmbeddedAndRefreshes()
        {
            var missingProfileId = GetProfileIdFromCanonical(UsCoreRequiredProfiles.CanonicalUrls[0]);
            var seeder = CreateSeeder(autoSeedProfiles: true, downloadFullPackage: false);
            SetupMissingProfile(missingProfileId);
            SetupSuccessfulUpsert();

            await seeder.SeedAsync(CancellationToken.None);

            await _fhirDataStore.Received().UpsertAsync(
                Arg.Is<ResourceWrapperOperation>(op => op.Wrapper != null),
                Arg.Any<CancellationToken>());
            _supportedProfilesStore.Received(1).Refresh();
            await _mediator.Received(1).PublishAsync(
                Arg.Is<RebuildCapabilityStatement>(n => n.Part == RebuildPart.Profiles),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task GivenDownloadFullPackageThrows_WhenSeedAsync_ThenSucceedsAndRefreshesAfterEmbeddedUpsert()
        {
            var missingProfileId = GetProfileIdFromCanonical(UsCoreRequiredProfiles.CanonicalUrls[0]);
            var testLogger = Substitute.For<ILogger<UsCoreProfileSeeder>>();
            var seeder = CreateSeeder(
                autoSeedProfiles: true,
                downloadFullPackage: true,
                logger: testLogger);
            SetupMissingProfile(missingProfileId);
            SetupSuccessfulUpsert();
            _packageDownloader
                .DownloadStructureDefinitionsAsync(Arg.Any<CancellationToken>())
                .Returns<Task<IReadOnlyList<(string Id, string Json)>>>(_ => throw new InvalidOperationException("download failed"));

            await seeder.SeedAsync(CancellationToken.None);

            _supportedProfilesStore.Received(1).Refresh();
            testLogger.Received().Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception, string>>());
        }

        private UsCoreProfileSeeder CreateSeeder(
            bool autoSeedProfiles,
            bool downloadFullPackage = false,
            ILogger<UsCoreProfileSeeder> logger = null)
        {
            var configuration = Options.Create(new ImplementationGuidesConfiguration
            {
                USCore = new USCoreConfiguration
                {
                    AutoSeedProfiles = autoSeedProfiles,
                    DownloadFullPackage = downloadFullPackage,
                },
            });

            return new UsCoreProfileSeeder(
                configuration,
                _fhirDataStoreFactory,
                _resourceWrapperFactory,
                _parser,
                _supportedProfilesStore,
                _packageDownloader,
                _fhirRequestContextAccessor,
                _mediator,
                logger ?? _logger,
                () => typeof(VersionSpecificModelInfoProvider).Assembly);
        }

        private ResourceWrapper CreateExistingWrapper(string profileId)
        {
            return new ResourceWrapper(
                profileId,
                "1",
                KnownResourceTypes.StructureDefinition,
                Substitute.For<RawResource>(),
                new ResourceRequest("GET"),
                DateTimeOffset.UtcNow,
                false,
                null,
                null,
                null);
        }

        private void SetupAllRequiredProfilesPresent()
        {
            _fhirDataStore
                .GetAsync(Arg.Any<ResourceKey>(), Arg.Any<CancellationToken>())
                .Returns(callInfo => Task.FromResult(CreateExistingWrapper(callInfo.ArgAt<ResourceKey>(0).Id)));
        }

        private void SetupMissingProfile(string missingProfileId)
        {
            _fhirDataStore
                .GetAsync(Arg.Any<ResourceKey>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var key = callInfo.ArgAt<ResourceKey>(0);
                    if (key.Id == missingProfileId)
                    {
                        return Task.FromResult<ResourceWrapper>(null);
                    }

                    return Task.FromResult(CreateExistingWrapper(key.Id));
                });
        }

        private void SetupSuccessfulUpsert()
        {
            _resourceWrapperFactory
                .Create(Arg.Any<ResourceElement>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>())
                .Returns(callInfo =>
                {
                    var element = callInfo.ArgAt<ResourceElement>(0);
                    return new ResourceWrapper(
                        element,
                        Substitute.For<RawResource>(),
                        new ResourceRequest("PUT"),
                        false,
                        null,
                        null,
                        null);
                });

            _fhirDataStore
                .UpsertAsync(Arg.Any<ResourceWrapperOperation>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var operation = callInfo.ArgAt<ResourceWrapperOperation>(0);
                    return Task.FromResult(new UpsertOutcome(operation.Wrapper, SaveOutcomeType.Created));
                });
        }

        private static string GetProfileIdFromCanonical(string canonicalUrl)
        {
            return canonicalUrl.Substring(canonicalUrl.LastIndexOf('/') + 1);
        }
    }
}
