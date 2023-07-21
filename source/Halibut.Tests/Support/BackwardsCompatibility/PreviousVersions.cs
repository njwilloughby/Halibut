﻿using System;

namespace Halibut.Tests.Support.BackwardsCompatibility
{
    public static class PreviousVersions
    {
        // Version prior to new exception types being added to Halibut. All exceptions are HalibutClientException
        public const string v5_0_429 = "5.0.429";

        // The last release with meaningful changes prior to Script Service V2
        public const string v5_0_236_Used_In_Tentacle_6_3_417 = "5.0.236";

        // The earliest release with a stable WebSocket implementation
        public const string v6_0_658_WebSocket_Stability_Fixes = "6.0.658";


        public static Version StableWebSocketVersion { get; } = new (v6_0_658_WebSocket_Stability_Fixes);
    }
}
