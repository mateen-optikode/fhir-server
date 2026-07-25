// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Collections.Generic;

namespace Microsoft.Health.Fhir.Api.Features.Conformance
{
    /// <summary>
    /// Inferno 10.1.05 required US Core 6.1 StructureDefinition canonical URLs.
    /// Keep in sync with tools/UsCoreProfileUploader/required-profiles-10.1.05.json.
    /// </summary>
    public static class UsCoreRequiredProfiles
    {
        public const string PackageId = "hl7.fhir.us.core";
        public const string PackageVersion = "6.1.0";
        public const string PackageDownloadUrl = "https://packages.simplifier.net/hl7.fhir.us.core/6.1.0";

        public static readonly IReadOnlyList<string> CanonicalUrls = new[]
        {
            "http://hl7.org/fhir/us/core/StructureDefinition/us-core-careplan",
            "http://hl7.org/fhir/us/core/StructureDefinition/us-core-condition-encounter-diagnosis",
            "http://hl7.org/fhir/us/core/StructureDefinition/us-core-condition-problems-health-concerns",
            "http://hl7.org/fhir/us/core/StructureDefinition/us-core-coverage",
            "http://hl7.org/fhir/us/core/StructureDefinition/us-core-diagnosticreport-lab",
            "http://hl7.org/fhir/us/core/StructureDefinition/us-core-diagnosticreport-note",
            "http://hl7.org/fhir/us/core/StructureDefinition/us-core-documentreference",
            "http://hl7.org/fhir/us/core/StructureDefinition/us-core-medicationrequest",
            "http://hl7.org/fhir/us/core/StructureDefinition/us-core-blood-pressure",
            "http://hl7.org/fhir/us/core/StructureDefinition/us-core-pulse-oximetry",
            "http://hl7.org/fhir/us/core/StructureDefinition/us-core-smokingstatus",
            "http://hl7.org/fhir/us/core/StructureDefinition/us-core-observation-clinical-result",
            "http://hl7.org/fhir/us/core/StructureDefinition/us-core-observation-occupation",
            "http://hl7.org/fhir/us/core/StructureDefinition/us-core-observation-pregnancyintent",
            "http://hl7.org/fhir/us/core/StructureDefinition/us-core-observation-pregnancystatus",
            "http://hl7.org/fhir/us/core/StructureDefinition/us-core-observation-screening-assessment",
            "http://hl7.org/fhir/us/core/StructureDefinition/us-core-organization",
            "http://hl7.org/fhir/us/core/StructureDefinition/us-core-practitioner",
            "http://hl7.org/fhir/us/core/StructureDefinition/us-core-provenance",
            "http://hl7.org/fhir/us/core/StructureDefinition/us-core-relatedperson",
            "http://hl7.org/fhir/us/core/StructureDefinition/us-core-servicerequest",
        };
    }
}
