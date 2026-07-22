// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;
using EnsureThat;
using Microsoft.Health.Fhir.Core.Features.Conformance;
using Newtonsoft.Json.Linq;

namespace Microsoft.Health.Fhir.Api.Features.Conformance
{
    /// <summary>
    /// Declares US Core Server CapabilityStatement conformance via Instantiates
    /// (required by Inferno ONC (g)(10) US Core 6.1 test 10.1.06).
    /// </summary>
    public sealed class UsCoreCapabilityProvider : IProvideCapability
    {
        public const string UsCoreServerCapabilityStatementUrl =
            "http://hl7.org/fhir/us/core/CapabilityStatement/us-core-server";

        public Task BuildAsync(ICapabilityStatementBuilder builder, CancellationToken cancellationToken)
        {
            EnsureArg.IsNotNull(builder, nameof(builder));

            builder.Apply(statement =>
            {
                statement.AdditionalData["instantiates"] = new JArray(UsCoreServerCapabilityStatementUrl);
            });

            return Task.CompletedTask;
        }
    }
}
