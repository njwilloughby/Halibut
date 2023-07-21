using System;
using System.Diagnostics;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.TestServices;
using Halibut.Tests.Util;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;
using System.Runtime.InteropServices;

namespace Halibut.Tests.Timeouts
{
    public class SendingAndReceivingRequestMessagesTimeoutsFixture : BaseTest
    {
        [Test]
        [TestCaseSource(typeof(ServiceConnectionTypesToTestExcludingWebSockets))]
        public async Task WhenThenNetworkIsPaused_WhileReadingAResponseMessage_ATcpReadTimeoutOccurs_and_FurtherRequestsCanBeMade(ServiceConnectionType serviceConnectionType)
        {
            using (var clientAndService = await LatestClientAndLatestServiceBuilder
                       .ForServiceConnectionType(serviceConnectionType)
                       .WithPortForwarding(out var portForwarderRef)
                       .WithEchoService()
                       .WithDoSomeActionService(() => portForwarderRef.Value.PauseExistingConnections())
                       .Build(CancellationToken))
            {
                var echo = clientAndService.CreateClient<IEchoService>();
                echo.SayHello("Make a request to make sure the connection is running, and ready. Lets not measure SSL setup cost.");

                var pauseConnections = clientAndService.CreateClient<IDoSomeActionService>(IncreasePollingQueueTimeout());

                var sw = Stopwatch.StartNew();
                var e = Assert.Throws<HalibutClientException>(() => pauseConnections.Action());
                sw.Stop();
                Logger.Error(e, "Received error");
                AssertExceptionMessageLooksLikeAReadTimeout(e);
                sw.Elapsed.Should().BeGreaterThan(HalibutLimits.TcpClientReceiveTimeout - TimeSpan.FromSeconds(2), "The receive timeout should apply, not the shorter heart beat timeout") // -2s give it a little slack to avoid it timed out slightly too early.
                    .And
                    .BeLessThan(HalibutLimits.TcpClientReceiveTimeout + LowerHalibutLimitsForAllTests.HalftheTcpRecieveTimeout, "We should be timing out on the tcp receive timeout");
                
                echo.SayHello("A new request can be made on a new unpaused TCP connection");
            }
        }

        [Test]
        [TestCaseSource(typeof(ServiceConnectionTypesToTestExcludingWebSockets))]
        public async Task WhenThenNetworkIsPaused_WhileReadingAResponseMessageDataStream_ATcpReadTimeoutOccurs_and_FurtherRequestsCanBeMade(ServiceConnectionType serviceConnectionType)
        {
            using (var clientAndService = await LatestClientAndLatestServiceBuilder
                       .ForServiceConnectionType(serviceConnectionType)
                       .WithPortForwarding(out var portForwarderRef)
                       .WithEchoService()
                       .WithReturnSomeDataStreamService(() => DataStreamUtil.From(
                           firstSend: "hello",
                           andThenRun: portForwarderRef.Value!.PauseExistingConnections,
                           thenSend: "All done"))
                       .Build(CancellationToken))
            {
                var echo = clientAndService.CreateClient<IEchoService>();
                echo.SayHello("Make a request to make sure the connection is running, and ready. Lets not measure SSL setup cost.");

                var pauseConnections = clientAndService.CreateClient<IReturnSomeDataStreamService>(IncreasePollingQueueTimeout());

                var sw = Stopwatch.StartNew();
                var e = Assert.Throws<HalibutClientException>(() => pauseConnections.SomeDataStream());
                sw.Stop();
                Logger.Error(e, "Received error");
                AssertExceptionMessageLooksLikeAReadTimeout(e);
                sw.Elapsed.Should().BeGreaterThan(HalibutLimits.TcpClientReceiveTimeout - TimeSpan.FromSeconds(2), "The receive timeout should apply, not the shorter heart beat timeout") // -2s give it a little slack to avoid it timed out slightly too early.
                    .And
                    .BeLessThan(HalibutLimits.TcpClientReceiveTimeout + LowerHalibutLimitsForAllTests.HalftheTcpRecieveTimeout, "We should be timing out on the tcp receive timeout");

                echo.SayHello("A new request can be made on a new unpaused TCP connection");
            }
        }

        [Test]
        [TestCaseSource(typeof(ServiceConnectionTypesToTestExcludingWebSockets))]
        public async Task WhenThenNetworkIsPaused_WhileSendingARequestMessage_ATcpWriteTimeoutOccurs_and_FurtherRequestsCanBeMade(ServiceConnectionType serviceConnectionType)
        {
            int numberOfBytesBeforePausingAStream = 1024 * 1024; // 1MB
            using (var clientAndService = await LatestClientAndLatestServiceBuilder
                       .ForServiceConnectionType(serviceConnectionType)
                       .WithPortForwarding(port => PortForwarderUtil.ForwardingToLocalPort(port)
                           .PauseSingleStreamAfterANumberOfBytesHaveBeenSet(numberOfBytesBeforePausingAStream)
                           .Build())
                       .WithEchoService()
                       .Build(CancellationToken))
            {
                var echo = clientAndService.CreateClient<IEchoService>();
                echo.SayHello("Make a request to make sure the connection is running, and ready. Lets not measure SSL setup cost.");

                var echoServiceTheErrorWillHappenOn = clientAndService.CreateClient<IEchoService>(IncreasePollingQueueTimeout());

                var stringToSend = Some.RandomAsciiStringOfLength(numberOfBytesBeforePausingAStream * 100);
                var sw = Stopwatch.StartNew();
                var e = Assert.Throws<HalibutClientException>(() => echoServiceTheErrorWillHappenOn.SayHello(stringToSend));
                AssertExceptionLooksLikeAWriteTimeout(e);
                sw.Stop();
                Logger.Error(e, "Received error when making the request (as expected)");

                var addControlMessageTimeout = TimeSpan.Zero;
                if (serviceConnectionType == ServiceConnectionType.Listening)
                {
                    // When an error occurs in listening mode, the dispose method in SecureConnection.Dispose
                    // will be called resulting in a END control message being sent over the wire. Since the
                    // connection is paused we must additionally wait HalibutLimits.TcpClientHeartbeatSendTimeout 
                    // for that to complete.
                    addControlMessageTimeout += HalibutLimits.TcpClientHeartbeatSendTimeout;
                }

                var expecedTimeOut = HalibutLimits.TcpClientSendTimeout // What we actually expected. 
                                     + HalibutLimits.TcpClientSendTimeout // an extra timeout because of the dispose method of the zip stream (see below)
                                     + addControlMessageTimeout;
                sw.Elapsed.Should().BeGreaterThan( expecedTimeOut- TimeSpan.FromSeconds(2), 
                        "We 'should' wait the send timeout amount of time NOT the heart beat timeout, however when an error occurs writing to the zip (deflate)" +
                                "stream we also call dispose which again attempts to write to the stream. Thus we wait 2 times the TcpClientSendTimeout.") // -2s give it a little slack to avoid it timed out slightly too early.
                    .And
                    .BeLessThan(expecedTimeOut + LowerHalibutLimitsForAllTests.HalftheTcpRecieveTimeout, 
                        "We 'should' wait the send timeout amount of time, however when an error occurs writing to the zip (deflate)" +
                            "stream we also call dispose which again attempts to write to the stream. Thus we wait 2 times the TcpClientSendTimeout.");

                echo.SayHello("A new request can be made on a new unpaused TCP connection");
            }
        }
        
        [Test]
        [TestCaseSource(typeof(ServiceConnectionTypesToTestExcludingWebSockets))]
        public async Task WhenThenNetworkIsPaused_WhileSendingADataStreamAsPartOfARequestMessage_ATcpWriteTimeoutOccurs_and_FurtherRequestsCanBeMade(ServiceConnectionType serviceConnectionType)
        {
            using (var clientAndService = await LatestClientAndLatestServiceBuilder
                       .ForServiceConnectionType(serviceConnectionType)
                       .WithPortForwarding(out var portForwarderRef)
                       .WithEchoService()
                       .Build(CancellationToken))
            {
                var echo = clientAndService.CreateClient<IEchoService>();
                echo.SayHello("Make a request to make sure the connection is running, and ready. Lets not measure SSL setup cost.");

                var echoServiceTheErrorWillHappenOn = clientAndService.CreateClient<IEchoService>(IncreasePollingQueueTimeout());

                
                var sw = Stopwatch.StartNew();
                var e = Assert.Throws<HalibutClientException>(() => echoServiceTheErrorWillHappenOn.CountBytes(DataStreamUtil.From(
                    firstSend: "hello",
                    andThenRun: portForwarderRef.Value!.PauseExistingConnections,
                    thenSend: "All done" + Some.RandomAsciiStringOfLength(100*1024*1024)
                )));
                AssertExceptionLooksLikeAWriteTimeout(e);
                sw.Stop();
                Logger.Error(e, "Received error when making the request (as expected)");
                
                // It is not clear why listening doesn't seem to wait to send a control message here.
                var addControlMessageTimeout = TimeSpan.Zero;
                if (serviceConnectionType == ServiceConnectionType.Listening)
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        // It is not clear why windows appears to add this timeout.
                        addControlMessageTimeout += HalibutLimits.TcpClientHeartbeatSendTimeout;                        
                    }
                }

                var expectedTimeout = HalibutLimits.TcpClientSendTimeout + addControlMessageTimeout;
                sw.Elapsed.Should().BeGreaterThan(expectedTimeout - TimeSpan.FromSeconds(2), "The receive timeout should apply, not the shorter heart beat timeout") // -2s give it a little slack to avoid it timed out slightly too early.
                    .And
                    .BeLessThan(expectedTimeout + LowerHalibutLimitsForAllTests.HalftheTcpRecieveTimeout, "We should be timing out on the tcp receive timeout");
                
                
                echo.SayHello("A new request can be made on a new unpaused TCP connection");
            }
        }

        static void AssertExceptionLooksLikeAWriteTimeout(HalibutClientException? e)
        {
            
            e.Message.Should().ContainAny(
        "Unable to write data to the transport connection: Connection timed out.",
                    " Unable to write data to the transport connection: A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond");
        }
        
        static void AssertExceptionMessageLooksLikeAReadTimeout(HalibutClientException? e)
        {
            e.Message.Should().ContainAny(
                "Unable to read data from the transport connection: Connection timed out.",
                "Unable to read data from the transport connection: A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond.");
        }

        static Action<ServiceEndPoint> IncreasePollingQueueTimeout()
        {
            return point =>
            {
                // We don't want to measure the polling queue timeouts.
                point.PollingRequestMaximumMessageProcessingTimeout = TimeSpan.FromMinutes(10);
                point.PollingRequestQueueTimeout = TimeSpan.FromMinutes(10);
            };
        }
    }
}