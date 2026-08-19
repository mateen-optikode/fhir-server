// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Health.Fhir.Api.Features.Conformance;
using Microsoft.Health.Fhir.Core.Features.Conformance;
using Microsoft.Health.Fhir.Core.Features.Conformance.Models;
using Microsoft.Health.Fhir.Tests.Common;
using Microsoft.Health.Test.Utilities;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Xunit;

namespace Microsoft.Health.Fhir.Api.UnitTests.Features.Conformance
{
    [Trait(Traits.OwningTeam, OwningTeam.Fhir)]
    [Trait(Traits.Category, Categories.Conformance)]
    public class UsCoreCapabilityProviderTests
    {
        public const string UsCoreServerCapabilityStatement =
            "http://hl7.org/fhir/us/core/CapabilityStatement/us-core-server";

        [Fact]
        public async Task GivenBuilder_WhenBuilding_ThenInstantiatesContainsUsCoreServer()
        {
            ListedCapabilityStatement captured = null;
            var builder = Substitute.For<ICapabilityStatementBuilder>();
            builder.When(b => b.Apply(Arg.Any<Action<ListedCapabilityStatement>>()))
                .Do(ci =>
                {
                    var statement = new ListedCapabilityStatement();
                    ci.Arg<Action<ListedCapabilityStatement>>()(statement);
                    captured = statement;
                });

            var provider = new UsCoreCapabilityProvider();
            await provider.BuildAsync(builder, CancellationToken.None);

            builder.Received(1).Apply(Arg.Any<Action<ListedCapabilityStatement>>());
            Assert.NotNull(captured);
            Assert.True(captured.AdditionalData.TryGetValue("instantiates", out var token));
            var arr = Assert.IsType<JArray>(token);
            Assert.Contains(arr, t => t.Type == JTokenType.String && (string)t == UsCoreServerCapabilityStatement);
            Assert.Contains(arr, t => t.Type == JTokenType.String && (string)t == UsCoreCapabilityProvider.BulkDataCapabilityStatementUrl);
        }
    }
}
