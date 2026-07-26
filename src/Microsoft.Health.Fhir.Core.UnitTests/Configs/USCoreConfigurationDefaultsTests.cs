// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Microsoft.Health.Fhir.Core.Configs;
using Xunit;

namespace Microsoft.Health.Fhir.Core.UnitTests.Configs
{
    public class USCoreConfigurationDefaultsTests
    {
        [Fact]
        public void Defaults_EnableAutoSeedAndFullPackageDownload()
        {
            var config = new USCoreConfiguration();
            Assert.True(config.AutoSeedProfiles);
            Assert.True(config.DownloadFullPackage);
        }
    }
}
