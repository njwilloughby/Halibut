using System;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Exceptions;
using Halibut.ServiceModel;
using Halibut.Transport.Protocol;
using NUnit.Framework;

namespace Halibut.Tests.ServiceModel
{
    public class HalibutProxyFixture
    {
        [Test]
        public void ThrowsSpecificErrorWhenErrorTypeIsSet()
        {
            var serverError = ResponseMessage.ServerErrorFromException(new MethodNotFoundHalibutClientException("not found", "not even mum could find it"));

            Action errorThrower = () => HalibutProxyWithAsync.ThrowExceptionFromReceivedError(serverError, new InMemoryConnectionLog("endpoint"));

            errorThrower.Should().Throw<MethodNotFoundHalibutClientException>();
        }

        [Test]
        public void ThrowsGenericErrorWhenErrorTypeIsUnknown()
        {
            var serverError = new ServerError { Message = "bob", Details = "details", HalibutErrorType = "Foo.BarException" };

            Action errorThrower = () => HalibutProxyWithAsync.ThrowExceptionFromReceivedError(serverError, new InMemoryConnectionLog("endpoint"));

            errorThrower.Should().Throw<HalibutClientException>();
        }

        [Test]
        public void ThrowsGenericErrorWhenErrorTypeIsNull_ForExampleWhenTalkingToAnOlderHalibutVersion()
        {
            var serverError = new ServerError { Message = "bob", Details = "details", HalibutErrorType = null };

            Action errorThrower = () => HalibutProxyWithAsync.ThrowExceptionFromReceivedError(serverError, new InMemoryConnectionLog("endpoint"));

            errorThrower.Should().Throw<HalibutClientException>();
        }

        [Test]
        public void BackwardCompatibility_ServiceNotFound_IsThrownAsA_ServiceNotFoundHalibutClientException()
        {
            var serverError = new ServerError
            {
                Message = "Service not found: IEchoService",
                Details = @"System.Exception: Service not found: IEchoService
   at Halibut.ServiceModel.DelegateServiceFactory.GetService(String name) in /home/auser/Documents/octopus/Halibut4/source/Halibut/ServiceModel/DelegateServiceFactory.cs:line 32
   at Halibut.ServiceModel.DelegateServiceFactory.CreateService(String serviceName) in /home/auser/Documents/octopus/Halibut4/source/Halibut/ServiceModel/DelegateServiceFactory.cs:line 24
   at Halibut.ServiceModel.ServiceInvoker.Invoke(RequestMessage requestMessage) in /home/auser/Documents/octopus/Halibut4/source/Halibut/ServiceModel/ServiceInvoker.cs:line 23
   at Halibut.HalibutRuntime.HandleIncomingRequest(RequestMessage request) in /home/auser/Documents/octopus/Halibut4/source/Halibut/HalibutRuntime.cs:line 256
   at Halibut.Transport.Protocol.MessageExchangeProtocol.InvokeAndWrapAnyExceptions(RequestMessage request, Func`2 incomingRequestProcessor) in /home/auser/Documents/octopus/Halibut4/source/Halibut/Transport/Protocol/MessageExchangeProtocol.cs:line 266",
                HalibutErrorType = null
            };

            Action errorThrower = () => HalibutProxyWithAsync.ThrowExceptionFromReceivedError(serverError, new InMemoryConnectionLog("endpoint"));

            errorThrower.Should().Throw<ServiceNotFoundHalibutClientException>();
        }

        [Test]
        public void BackwardsCompatibility_MethodNotFound_IsThrownAsA_MethodNotFoundHalibutClientException()
        {
            var serverError = new ServerError
            {
                Message = "Service System.Object::SayHello not found",
                Details = null,
                HalibutErrorType = null
            };

            Action errorThrower = () => HalibutProxyWithAsync.ThrowExceptionFromReceivedError(serverError, new InMemoryConnectionLog("endpoint"));

            errorThrower.Should().Throw<MethodNotFoundHalibutClientException>();
        }

        [Test]
        public void BackwardCompatibility_AmbiguousMethodMatch_IsThrownAsA_NoMatchingServiceOrMethodHalibutClientException()
        {
            var serverError = new ServerError
            {
                Message = @"More than one possible match for the requested service method was found given the argument types. The matches were:
 - System.String Ambiguous(System.String, System.String)
 - System.String Ambiguous(System.String, System.Tuple`2[System.String,System.String])
The request arguments were:
String, <null>
",
                Details = @"System.Reflection.AmbiguousMatchException: More than one possible match for the requested service method was found given the argument types. The matches were:
 - System.String Ambiguous(System.String, System.String)
 - System.String Ambiguous(System.String, System.Tuple`2[System.String,System.String])
The request arguments were:
String, <null>

   at Halibut.ServiceModel.ServiceInvoker.SelectMethod(IList`1 methods, RequestMessage requestMessage) in /home/auser/Documents/octopus/Halibut4/source/Halibut/ServiceModel/ServiceInvoker.cs:line 102
   at Halibut.ServiceModel.ServiceInvoker.Invoke(RequestMessage requestMessage) in /home/auser/Documents/octopus/Halibut4/source/Halibut/ServiceModel/ServiceInvoker.cs:line 31
   at Halibut.HalibutRuntime.HandleIncomingRequest(RequestMessage request) in /home/auser/Documents/octopus/Halibut4/source/Halibut/HalibutRuntime.cs:line 256
   at Halibut.Transport.Protocol.MessageExchangeProtocol.InvokeAndWrapAnyExceptions(RequestMessage request, Func`2 incomingRequestProcessor) in /home/auser/Documents/octopus/Halibut4/source/Halibut/Transport/Protocol/MessageExchangeProtocol.cs:line 266",
                HalibutErrorType = null
            };

            Action errorThrower = () => HalibutProxyWithAsync.ThrowExceptionFromReceivedError(serverError, new InMemoryConnectionLog("endpoint"));

            errorThrower.Should().Throw<AmbiguousMethodMatchHalibutClientException>();
        }

        [Test]
        public void BackwardsCompatibility_ServiceThrewAnException_IsThrownAsA_ServiceInvocationHalibutClientException()
        {
            var serverError = new ServerError
            {
                Message = "Attempted to divide by zero.",
                Details = @"System.Reflection.TargetInvocationException: Exception has been thrown by the target of an invocation.
 ---> System.DivideByZeroException: Attempted to divide by zero.
   at Halibut.TestUtils.Contracts.EchoService.Crash() in /home/auser/Documents/octopus/Halibut4/source/Halibut.Tests/TestServices/EchoService.cs:line 16
   --- End of inner exception stack trace ---
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Span`1& arguments, Signature sig, Boolean constructor, Boolean wrapExceptions)
   at System.Reflection.RuntimeMethodInfo.Invoke(Object obj, BindingFlags invokeAttr, Binder binder, Object[] parameters, CultureInfo culture)
   at System.Reflection.MethodBase.Invoke(Object obj, Object[] parameters)
   at Halibut.ServiceModel.ServiceInvoker.Invoke(RequestMessage requestMessage) in /home/auser/Documents/octopus/Halibut4/source/Halibut/ServiceModel/ServiceInvoker.cs:line 34
   at Halibut.HalibutRuntime.HandleIncomingRequest(RequestMessage request) in /home/auser/Documents/octopus/Halibut4/source/Halibut/HalibutRuntime.cs:line 256
   at Halibut.Transport.Protocol.MessageExchangeProtocol.InvokeAndWrapAnyExceptions(RequestMessage request, Func`2 incomingRequestProcessor) in /home/auser/Documents/octopus/Halibut4/source/Halibut/Transport/Protocol/MessageExchangeProtocol.cs:line 266",
                HalibutErrorType = null
            };

            Action errorThrower = () => HalibutProxyWithAsync.ThrowExceptionFromReceivedError(serverError, new InMemoryConnectionLog("endpoint"));

            errorThrower.Should().Throw<ServiceInvocationHalibutClientException>();
        }
    }
}