// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

namespace Microsoft.Health.Fhir.Core.Configs
{
    public sealed class USCoreConfiguration
    {
        public bool MissingData { get; set; } = false;

        public bool EnableDocRef { get; set; } = false;

        /// <summary>
        /// When true, seed US Core StructureDefinitions on startup if required profiles are missing.
        /// </summary>
        public bool AutoSeedProfiles { get; set; } = true;

        /// <summary>
        /// When true, after embedded seed, attempt to download hl7.fhir.us.core@6.1.0 and upsert remaining profiles.
        /// Failures are logged; embedded seed is kept.
        /// </summary>
        public bool DownloadFullPackage { get; set; } = true;
    }
}
