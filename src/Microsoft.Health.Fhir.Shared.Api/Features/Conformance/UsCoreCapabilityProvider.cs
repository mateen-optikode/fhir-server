// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Linq;
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

        /// <summary>
        /// Bulk Data Access IG CapabilityStatement (required by Inferno ONC (g)(10) scenario 8.2.02).
        /// </summary>
        public const string BulkDataCapabilityStatementUrl =
            "http://hl7.org/fhir/uv/bulkdata/CapabilityStatement/bulk-data";

        public Task BuildAsync(ICapabilityStatementBuilder builder, CancellationToken cancellationToken)
        {
            EnsureArg.IsNotNull(builder, nameof(builder));

            builder.Apply(statement =>
            {
                var instantiates = new JArray();
                if (statement.AdditionalData.TryGetValue("instantiates", out JToken existing) && existing is JArray existingArray)
                {
                    foreach (JToken item in existingArray.Where(item => item.Type == JTokenType.String))
                    {
                        instantiates.Add(item);
                    }
                }

                AddInstantiatesUrlIfMissing(instantiates, UsCoreServerCapabilityStatementUrl);
                AddInstantiatesUrlIfMissing(instantiates, BulkDataCapabilityStatementUrl);

                statement.AdditionalData["instantiates"] = instantiates;
            });

            return Task.CompletedTask;
        }

        private static void AddInstantiatesUrlIfMissing(JArray instantiates, string url)
        {
            if (!instantiates.Any(token => token.Type == JTokenType.String && (string)token == url))
            {
                instantiates.Add(url);
            }
        }
    }
}
