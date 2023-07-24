using System;
using System.Diagnostics;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Tests.Support;
using Halibut.Tests.Support.PortForwarding;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class ListeningTentaclesUseAPoolOfConnectionsFixture : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testPolling: false, testWebSocket: false, testNetworkConditions: false)]
        public async Task TestOnlyHealthConnectionsAreKeptInThePool(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            TcpConnectionsCreatedCounter tcpConnectionsCreatedCounter = null;
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithPortForwarding(port => PortForwarderUtil.ForwardingToLocalPort(port)
                           .WithCountTcpConnectionsCreated(out tcpConnectionsCreatedCounter)
                           .Build())
                       .WithStandardServices()
                       .AsLatestClientAndLatestServiceBuilder()
                       .WithPortForwarding(out var portForwarder)
                       .WithDoSomeActionService(() => portForwarder.Value.PauseExistingConnections())
                       .Build(CancellationToken))
            {
                var echoService = clientAndService.CreateClient<IEchoService>(point =>
                {
                    // This test should never need to make use of this since no bad connections should be in the pool
                    point.RetryListeningSleepInterval = TimeSpan.FromMinutes(10);
                });
                var pauseCurrentTcpConnections = clientAndService.CreateClient<IDoSomeActionService>();

                echoService.SayHello("This should make one connection");
                tcpConnectionsCreatedCounter.ConnectionsCreatedCount.Should().Be(1);
                
                echoService.SayHello("Should re-use the same connection");
                tcpConnectionsCreatedCounter.ConnectionsCreatedCount.Should().Be(1, "We should use the same connection since the last was healthy");

                Assert.Throws<HalibutClientException>(() => pauseCurrentTcpConnections.Action());
                // Connection should not be put back into the pool
                tcpConnectionsCreatedCounter.ConnectionsCreatedCount.Should().Be(1, "This should still be using the same connection since it is on this call we break the connection.");

                var sw = Stopwatch.StartNew();
                echoService.SayHello("This should immediately create a new connection");
                sw.Stop();
                
                tcpConnectionsCreatedCounter.ConnectionsCreatedCount.Should().Be(2, "Since the last connection should not have been put back into the pool.");

                sw.Elapsed.Should().BeLessThan(HalibutLimits.TcpClientHeartbeatReceiveTimeout, "we should not be putting the bad connection back into the pool, " +
                                                                                               "then pulling it out detecting it is bad and then attempting to create a new connection");
            }
        }
    }
}