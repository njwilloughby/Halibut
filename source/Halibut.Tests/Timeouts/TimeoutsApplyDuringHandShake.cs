using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Tests.Builders;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
using Halibut.Tests.Util;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;
using Octopus.TestPortForwarder;

namespace Halibut.Tests.Timeouts
{
    /// <summary>
    /// Where handshake means early on in setting up a TCP connection.
    /// </summary>
    public class TimeoutsApplyDuringHandShake : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, additionalParameters: new object[] { true, 1 })]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, additionalParameters: new object[] { false, 1 })]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, additionalParameters: new object[] { true, 2 })]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, additionalParameters: new object[] { false, 2 })]
        public async Task WhenTheFirstWriteOverTheWireOccursOnAConnectionThatImmediatelyPauses_AWriteTimeoutShouldApply(
            ClientAndServiceTestCase clientAndServiceTestCase,
            bool onClientToOrigin, // Don't dwell on what this means, we just want to test all combinations of where the timeout can occur.
            int writeNumberToPauseOn // Ie pause on the first or second write
            ) 
        {
            var dataTransferObserverPauser = new DataTransferObserverBuilder()
                .WithWritePausing(Logger, writeNumberToPauseOn)
                .Build();
            var dataTransferObserverDoNothing = new DataTransferObserverBuilder().Build();
            
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .As<LatestClientAndLatestServiceBuilder>()
                       .WithPortForwarding(port => PortForwarderUtil.ForwardingToLocalPort(port)
                           .WithDataObserver(() =>
                           {
                               if(onClientToOrigin) return new BiDirectionalDataTransferObserver(dataTransferObserverPauser,dataTransferObserverDoNothing);
                               return new BiDirectionalDataTransferObserver(dataTransferObserverDoNothing, dataTransferObserverPauser);
                           })
                           .Build())
                       .WithPendingRequestQueueFactory(logFactory => new FuncPendingRequestQueueFactory(uri => new PendingRequestQueueBuilder()
                           .WithLog(logFactory.ForEndpoint(uri))
                           .WithPollingQueueWaitTimeout(TimeSpan.FromSeconds(1))
                           .WithSyncOrAsync(clientAndServiceTestCase.SyncOrAsync)
                           .Build()))
                       .WithEchoService()
                       .Build(CancellationToken))
            {
                var echo = clientAndService.CreateClient<IEchoService, IAsyncClientEchoService>(IncreasePollingQueueTimeout);
                var sw = Stopwatch.StartNew();
                try
                {
                    await echo.SayHelloAsync("Make a request to make sure the connection is running, and ready. Lets not measure SSL setup cost.");
                }
                catch (Exception e)
                {
                    Logger.Information(e, "An exception was raised during the request, this is not an issue since we are concerned about the timings. " +
                                          "Exceptions only occur occasionally and probably only on listening.");
                }

                sw.Stop();
                sw.Elapsed.Should().BeCloseTo(HalibutLimits.TcpClientReceiveTimeout, TimeSpan.FromSeconds(15), "Since a paused connection early on should not hang forever.");

                await echo.SayHelloAsync("The pump wont be paused here so this should work.");
            }
        }
        
        static void IncreasePollingQueueTimeout(ServiceEndPoint point)
        {
            // We don't want to measure the polling queue timeouts.
            point.PollingRequestMaximumMessageProcessingTimeout = TimeSpan.FromMinutes(10);
            point.PollingRequestQueueTimeout = TimeSpan.FromMinutes(10);
        }
    }
}
