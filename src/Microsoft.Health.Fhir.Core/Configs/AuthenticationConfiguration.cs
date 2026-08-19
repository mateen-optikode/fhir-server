// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

namespace Microsoft.Health.Fhir.Core.Configs
{
    public class AuthenticationConfiguration
    {
        public string Audience { get; set; }

        public string Authority { get; set; }

        /// <summary>
        /// When true, opaque (reference) bearer tokens are validated via RFC 7662 introspection.
        /// JWT bearer tokens continue to use the standard JwtBearer handler.
        /// </summary>
        public bool UseIntrospection { get; set; }

        /// <summary>
        /// Absolute introspection endpoint URL (e.g. https://auth.optikode.com/connect/introspect).
        /// When empty and <see cref="UseIntrospection"/> is true, defaults to {Authority}/connect/introspect.
        /// </summary>
        public string IntrospectionEndpoint { get; set; }

        /// <summary>
        /// Optional client id used as HTTP Basic credentials when calling the introspection endpoint.
        /// </summary>
        public string IntrospectionClientId { get; set; }

        /// <summary>
        /// Optional client secret used as HTTP Basic credentials when calling the introspection endpoint.
        /// </summary>
        public string IntrospectionClientSecret { get; set; }
    }
}
