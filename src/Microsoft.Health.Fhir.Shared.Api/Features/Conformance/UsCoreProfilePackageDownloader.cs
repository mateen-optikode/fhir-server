// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EnsureThat;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Health.Fhir.Api.Features.Conformance
{
    /// <summary>
    /// Downloads hl7.fhir.us.core@6.1.0 and extracts StructureDefinition JSON payloads.
    /// Primary: https://packages.simplifier.net/hl7.fhir.us.core/6.1.0
    /// Fallback: https://packages.fhir.org/hl7.fhir.us.core/6.1.0 (same npm package).
    /// </summary>
    public sealed class UsCoreProfilePackageDownloader : IUsCoreProfilePackageDownloader
    {
        public const string FallbackPackageDownloadUrl = "https://packages.fhir.org/hl7.fhir.us.core/6.1.0";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<UsCoreProfilePackageDownloader> _logger;

        public UsCoreProfilePackageDownloader(
            IHttpClientFactory httpClientFactory,
            ILogger<UsCoreProfilePackageDownloader> logger)
        {
            _httpClientFactory = EnsureArg.IsNotNull(httpClientFactory, nameof(httpClientFactory));
            _logger = EnsureArg.IsNotNull(logger, nameof(logger));
        }

        public async Task<IReadOnlyList<(string Id, string Json)>> DownloadStructureDefinitionsAsync(CancellationToken cancellationToken)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "uscore-package-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var packageFile = Path.Combine(tempDir, $"{UsCoreRequiredProfiles.PackageId}-{UsCoreRequiredProfiles.PackageVersion}.tgz");
                await DownloadPackageAsync(packageFile, cancellationToken).ConfigureAwait(false);
                return ExtractStructureDefinitions(packageFile);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, recursive: true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to delete temporary US Core package directory {TempDir}", tempDir);
                }
            }
        }

        private async Task DownloadPackageAsync(string destinationPath, CancellationToken cancellationToken)
        {
            var urls = new[] { UsCoreRequiredProfiles.PackageDownloadUrl, FallbackPackageDownloadUrl };
            Exception lastException = null;

            using var httpClient = _httpClientFactory.CreateClient();

            foreach (var url in urls)
            {
                try
                {
                    _logger.LogInformation("Downloading US Core package from {PackageUrl}", url);
                    using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();

                    await using var fileStream = File.Create(destinationPath);
                    await response.Content.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
                    return;
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                    lastException = ex;
                    _logger.LogWarning(ex, "US Core package download failed from {PackageUrl}", url);
                }
            }

            throw new InvalidOperationException(
                $"Failed to download US Core package from {UsCoreRequiredProfiles.PackageDownloadUrl} and {FallbackPackageDownloadUrl}.",
                lastException);
        }

        private static IReadOnlyList<(string Id, string Json)> ExtractStructureDefinitions(string packageFilePath)
        {
            var results = new List<(string Id, string Json)>();

            using var fileStream = File.OpenRead(packageFilePath);
            using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
            using var tarReader = new TarReader(gzipStream);

            while (tarReader.GetNextEntry() is { } entry)
            {
                if (entry.EntryType != TarEntryType.RegularFile)
                {
                    continue;
                }

                var entryPath = entry.Name.Replace('\\', '/');
                if (!entryPath.Contains("/package/", StringComparison.Ordinal) &&
                    !entryPath.StartsWith("package/", StringComparison.Ordinal))
                {
                    continue;
                }

                var fileName = Path.GetFileName(entryPath);
                if (fileName == null ||
                    !fileName.StartsWith("StructureDefinition-", StringComparison.Ordinal) ||
                    !fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                using var entryStream = entry.DataStream;
                if (entryStream == null)
                {
                    continue;
                }

                using var reader = new StreamReader(entryStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
                var json = reader.ReadToEnd();
                if (string.IsNullOrWhiteSpace(json))
                {
                    continue;
                }

                var id = fileName.Substring("StructureDefinition-".Length, fileName.Length - "StructureDefinition-".Length - ".json".Length);
                results.Add((id, json));
            }

            return results;
        }
    }
}
