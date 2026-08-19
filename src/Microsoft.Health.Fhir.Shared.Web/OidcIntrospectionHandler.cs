// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Health.Fhir.Web
{
    /// <summary>
    /// Validates opaque (reference) access tokens via the IdP RFC 7662 introspection endpoint.
    /// </summary>
    internal sealed class OidcIntrospectionHandler : AuthenticationHandler<OidcIntrospectionOptions>
    {
        public const string SchemeName = "OidcIntrospection";
        public const string HttpClientName = "OidcIntrospection";

        private readonly IHttpClientFactory _httpClientFactory;

        public OidcIntrospectionHandler(
            IOptionsMonitor<OidcIntrospectionOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IHttpClientFactory httpClientFactory)
            : base(options, logger, encoder)
        {
            _httpClientFactory = httpClientFactory;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string authorization = Request.Headers.Authorization.ToString();
            if (string.IsNullOrWhiteSpace(authorization) ||
                !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return AuthenticateResult.NoResult();
            }

            string token = authorization["Bearer ".Length..].Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                return AuthenticateResult.NoResult();
            }

            try
            {
                using var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["token"] = token,
                    ["token_type_hint"] = "access_token",
                });

                HttpClient client = _httpClientFactory.CreateClient(HttpClientName);
                using HttpRequestMessage request = new(HttpMethod.Post, Options.IntrospectionEndpoint)
                {
                    Content = content,
                };

                if (!string.IsNullOrWhiteSpace(Options.ClientId))
                {
                    string credentials = Convert.ToBase64String(
                        Encoding.UTF8.GetBytes($"{Options.ClientId}:{Options.ClientSecret ?? string.Empty}"));
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                }

                using HttpResponseMessage response = await client.SendAsync(request, Context.RequestAborted);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.LogWarning("Introspection endpoint returned {StatusCode}", response.StatusCode);
                    return AuthenticateResult.Fail("Token introspection failed.");
                }

                await using var stream = await response.Content.ReadAsStreamAsync(Context.RequestAborted);
                using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: Context.RequestAborted);
                JsonElement root = document.RootElement;

                if (!root.TryGetProperty("active", out JsonElement activeElement) ||
                    activeElement.ValueKind != JsonValueKind.True)
                {
                    return AuthenticateResult.Fail("Token is inactive.");
                }

                string expectedAudience = Options.Audience;
                if (!string.IsNullOrWhiteSpace(expectedAudience) &&
                    root.TryGetProperty("aud", out JsonElement audElement) &&
                    !AudienceMatches(audElement, expectedAudience))
                {
                    return AuthenticateResult.Fail("Token audience is invalid.");
                }

                string rolesClaim = string.IsNullOrWhiteSpace(Options.RolesClaim) ? "roles" : Options.RolesClaim;
                var identity = new ClaimsIdentity(Scheme.Name, ClaimTypes.Name, rolesClaim);

                AddClaim(identity, ClaimTypes.NameIdentifier, GetString(root, "sub"));
                AddClaim(identity, "sub", GetString(root, "sub"));
                AddClaim(identity, "client_id", GetString(root, "client_id"));
                AddClaim(identity, "iss", GetString(root, "iss"));
                AddClaim(identity, "patient", GetString(root, "patient"));
                AddClaim(identity, "encounter", GetString(root, "encounter"));
                AddClaim(identity, "fhirUser", GetString(root, "fhirUser"));

                string scope = GetString(root, "scope");
                if (!string.IsNullOrWhiteSpace(scope))
                {
                    AddClaim(identity, "scope", scope);
                    foreach (string part in scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        AddClaim(identity, "scp", part);
                    }
                }

                AddClaimsFromElement(identity, rolesClaim, root, "roles");
                AddClaimsFromElement(identity, "aud", root, "aud");

                string exp = GetString(root, "exp");
                if (!string.IsNullOrWhiteSpace(exp))
                {
                    AddClaim(identity, "exp", exp);
                }

                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);
                return AuthenticateResult.Success(ticket);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Token introspection threw an exception");
                return AuthenticateResult.Fail("Token introspection failed.");
            }
        }

        private static bool AudienceMatches(JsonElement audElement, string expectedAudience)
        {
            if (audElement.ValueKind == JsonValueKind.String)
            {
                return string.Equals(audElement.GetString(), expectedAudience, StringComparison.OrdinalIgnoreCase);
            }

            if (audElement.ValueKind == JsonValueKind.Array)
            {
                return audElement.EnumerateArray()
                    .Any(item => item.ValueKind == JsonValueKind.String &&
                                 string.Equals(item.GetString(), expectedAudience, StringComparison.OrdinalIgnoreCase));
            }

            return false;
        }

        private static void AddClaimsFromElement(ClaimsIdentity identity, string claimType, JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out JsonElement element))
            {
                return;
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                AddClaim(identity, claimType, element.GetString());
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in element.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        AddClaim(identity, claimType, item.GetString());
                    }
                }
            }
        }

        private static void AddClaim(ClaimsIdentity identity, string type, string value)
        {
            if (!string.IsNullOrWhiteSpace(type) && !string.IsNullOrWhiteSpace(value))
            {
                identity.AddClaim(new Claim(type, value));
            }
        }

        private static string GetString(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out JsonElement element))
            {
                return null;
            }

            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null,
            };
        }
    }
}
