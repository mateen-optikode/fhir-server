// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using EnsureThat;
using Medino;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Health.Fhir.Core.Messages.Search;

namespace Microsoft.Health.Fhir.Api.Features.Conformance
{
    /// <summary>
    /// Seeds US Core StructureDefinition profiles after FHIR storage and search parameters are ready.
    /// </summary>
    public sealed class UsCoreProfileSeedHostedService : BackgroundService, INotificationHandler<SearchParametersInitializedNotification>
    {
        private readonly IUsCoreProfileSeeder _seeder;
        private readonly ILogger<UsCoreProfileSeedHostedService> _logger;
        private bool _storageReady;

        public UsCoreProfileSeedHostedService(
            IUsCoreProfileSeeder seeder,
            ILogger<UsCoreProfileSeedHostedService> logger)
        {
            _seeder = EnsureArg.IsNotNull(seeder, nameof(seeder));
            _logger = EnsureArg.IsNotNull(logger, nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("UsCoreProfileSeedHostedService begin.");

            try
            {
                while (!_storageReady && !stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }

                if (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("UsCoreProfileSeedHostedService cancelled before storage ready.");
                    return;
                }

                _logger.LogInformation("UsCoreProfileSeedHostedService storage ready; invoking seeder.");
                await _seeder.SeedAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("UsCoreProfileSeedHostedService cancelled during seed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UsCoreProfileSeedHostedService failed to seed US Core profiles.");
            }
            finally
            {
                _logger.LogInformation("UsCoreProfileSeedHostedService end.");
            }
        }

        public Task HandleAsync(SearchParametersInitializedNotification notification, CancellationToken cancellationToken)
        {
            _storageReady = true;
            return Task.CompletedTask;
        }
    }
}
