namespace Halibut.Tests.Support
{
    public enum ServiceConnectionType
    {
        Polling,
        PollingOverWebSocket,
        Listening
    }

    public static class ServiceConnectionTypes
    {
        public static ServiceConnectionType[] All => new[]
            {
               ServiceConnectionType.Listening,
               ServiceConnectionType.Polling,
// Disabled while these are causing flakey Team City Tests
//#if SUPPORTS_WEB_SOCKET_CLIENT
//             ServiceConnectionType.PollingOverWebSocket
//#endif
        };

        public static ServiceConnectionType[] AllExceptWebSockets => new[]
        {
            ServiceConnectionType.Listening,
            ServiceConnectionType.Polling
        };

    }
}