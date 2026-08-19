// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Authentication;

namespace Microsoft.Health.Fhir.Web
{
    /// <summary>
    /// Options for <see cref="OidcIntrospectionHandler"/>.
    /// </summary>
    internal sealed class OidcIntrospectionOptions : AuthenticationSchemeOptions
    {
        public string IntrospectionEndpoint { get; set; }

        public string ClientId { get; set; }

        public string ClientSecret { get; set; }

        public string Audience { get; set; }

        public string RolesClaim { get; set; } = "roles";
    }
}
