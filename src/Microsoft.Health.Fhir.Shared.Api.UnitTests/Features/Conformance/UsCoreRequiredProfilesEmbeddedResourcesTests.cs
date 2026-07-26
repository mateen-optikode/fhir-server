// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Linq;
using Microsoft.Health.Fhir.Api.Features.Conformance;
using Microsoft.Health.Fhir.Core;
using Microsoft.Health.Fhir.Tests.Common;
using Microsoft.Health.Test.Utilities;
using Xunit;

namespace Microsoft.Health.Fhir.Api.UnitTests.Features.Conformance
{
    [Trait(Traits.OwningTeam, OwningTeam.Fhir)]
    [Trait(Traits.Category, Categories.Conformance)]
    public class UsCoreRequiredProfilesEmbeddedResourcesTests
    {
        [Fact]
        public void GivenRequiredProfiles_WhenLoadingR4CoreAssembly_ThenEmbeddedStructureDefinitionsExist()
        {
            var assembly = typeof(VersionSpecificModelInfoProvider).Assembly;
            var resourceNames = assembly.GetManifestResourceNames();

            Assert.Equal(21, UsCoreRequiredProfiles.CanonicalUrls.Count);

            foreach (var canonicalUrl in UsCoreRequiredProfiles.CanonicalUrls)
            {
                var profileId = canonicalUrl.Substring(canonicalUrl.LastIndexOf('/') + 1);
                var suffix = $"StructureDefinition-{profileId}.json";
                Assert.Contains(
                    resourceNames,
                    name => name.EndsWith(suffix, StringComparison.Ordinal));
            }
        }
    }
}
