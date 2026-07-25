// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Health.Fhir.Api.Features.Conformance;
using Microsoft.Health.Fhir.Core.Messages.Search;
using Microsoft.Health.Fhir.Tests.Common;
using Microsoft.Health.Test.Utilities;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.Health.Fhir.Api.UnitTests.Features.Conformance
{
    [Trait(Traits.OwningTeam, OwningTeam.Fhir)]
    [Trait(Traits.Category, Categories.Conformance)]
    public sealed class UsCoreProfileSeedHostedServiceTests
    {
        private readonly IUsCoreProfileSeeder _seeder = Substitute.For<IUsCoreProfileSeeder>();

        public UsCoreProfileSeedHostedServiceTests()
        {
            _seeder.SeedAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        }

        [Fact]
        public async Task GivenStorageNotReady_WhenExecuteAsyncRuns_ThenSeederIsNotCalledWithinShortDelay()
        {
            using var cancellationTokenSource = new CancellationTokenSource();
            var service = CreateService();

            var executeTask = service.StartAsync(cancellationTokenSource.Token);
            await Task.Delay(300, cancellationTokenSource.Token);

            await _seeder.DidNotReceive().SeedAsync(Arg.Any<CancellationToken>());

            cancellationTokenSource.Cancel();
            await executeTask;
        }

        [Fact]
        public async Task GivenStorageReadyNotification_WhenExecuteAsyncRuns_ThenSeederIsCalledOnce()
        {
            using var cancellationTokenSource = new CancellationTokenSource();
            var service = CreateService();

            var executeTask = service.StartAsync(cancellationTokenSource.Token);
            await service.Handle(new SearchParametersInitializedNotification(), CancellationToken.None);
            await Task.Delay(1500, cancellationTokenSource.Token);

            await _seeder.Received(1).SeedAsync(Arg.Any<CancellationToken>());

            cancellationTokenSource.Cancel();
            await executeTask;
        }

        [Fact]
        public async Task GivenSeederThrows_WhenExecuteAsyncRuns_ThenHostDoesNotCrashAndErrorIsLogged()
        {
            using var cancellationTokenSource = new CancellationTokenSource();
            var logger = Substitute.For<ILogger<UsCoreProfileSeedHostedService>>();
            _seeder.SeedAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("seed failed"));

            var service = CreateService(logger);

            var executeTask = service.StartAsync(cancellationTokenSource.Token);
            await service.Handle(new SearchParametersInitializedNotification(), CancellationToken.None);
            await Task.Delay(1500, cancellationTokenSource.Token);

            await executeTask;

            logger.Received().Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString().Contains("UsCoreProfileSeedHostedService failed to seed US Core profiles.")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception, string>>());
        }

        private UsCoreProfileSeedHostedService CreateService(ILogger<UsCoreProfileSeedHostedService> logger = null)
        {
            return new UsCoreProfileSeedHostedService(_seeder, logger ?? NullLogger<UsCoreProfileSeedHostedService>.Instance);
        }
    }
}
