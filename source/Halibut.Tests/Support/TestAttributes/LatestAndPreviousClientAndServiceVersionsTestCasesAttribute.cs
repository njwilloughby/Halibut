﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Halibut.Tests.Support.BackwardsCompatibility;
using Halibut.Tests.Support.TestCases;
using Halibut.Util;
using Xunit.Sdk;
using static Halibut.Tests.Support.TestAttributes.LatestAndPreviousClientAndServiceVersionsTestCasesAttribute;

namespace Halibut.Tests.Support.TestAttributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class LatestAndPreviousClientAndServiceVersionsTestCasesAttribute : HalibutTestCaseSourceAttribute
    {
        public LatestAndPreviousClientAndServiceVersionsTestCasesAttribute(
            bool testWebSocket = true,
            bool testNetworkConditions = true,
            bool testListening = true,
            bool testPolling = true,
            bool testAsyncAndSyncClients = true,
            bool testAsyncServicesAsWell = false // False means only the sync service will be tested.
        ) :
            base(
                typeof(LatestAndPreviousClientAndServiceVersionsTestCases),
                nameof(LatestAndPreviousClientAndServiceVersionsTestCases.GetEnumerator),
                new object[] { testWebSocket, testNetworkConditions, testListening, testPolling, testAsyncAndSyncClients, testAsyncServicesAsWell })
        {
        }

        public static class LatestAndPreviousClientAndServiceVersionsTestCases
        {
            //TODO: @server-at-scale - When NUnit is removed, remove this class, and make this method the body of LatestAndPreviousClientAndServiceVersionsTestCasesXUnitAttribute.GetData
            public static IEnumerable<object[]> GetEnumerator(bool testWebSocket, bool testNetworkConditions, bool testListening, bool testPolling, bool testAsyncAndSyncClients, bool testAsyncServicesAsWell)
            {
                var serviceConnectionTypes = ServiceConnectionTypes.All.ToList();

                if (!testWebSocket)
                {
                    serviceConnectionTypes.Remove(ServiceConnectionType.PollingOverWebSocket);
                }

                if (!testListening)
                {
                    serviceConnectionTypes.Remove(ServiceConnectionType.Listening);
                }

                if (!testPolling)
                {
                    serviceConnectionTypes.Remove(ServiceConnectionType.Polling);
                }

                var clientProxyTypesToTest = Array.Empty<ForceClientProxyType>();
                if (testAsyncAndSyncClients)
                {
                    clientProxyTypesToTest = ForceClientProxyTypeValues.All;
                }

                var serviceAsyncHalibutFeatureTestCases = AsyncHalibutFeatureValues.All().ToList();
                if (!testAsyncServicesAsWell)
                {
                    serviceAsyncHalibutFeatureTestCases.Remove(AsyncHalibutFeature.Enabled);
                }

                var builder = new ClientAndServiceTestCasesBuilder(
                    new[]
                    {
                        ClientAndServiceTestVersion.Latest(),
                        ClientAndServiceTestVersion.ClientOfVersion(PreviousVersions.v5_0_236_Used_In_Tentacle_6_3_417.ClientVersion),
                        ClientAndServiceTestVersion.ServiceOfVersion(PreviousVersions.v5_0_236_Used_In_Tentacle_6_3_417.ServiceVersion),
                        ClientAndServiceTestVersion.ServiceOfVersion(PreviousVersions.v4_4_8.ServiceVersion),
                    },
                    serviceConnectionTypes.ToArray(),
                    testNetworkConditions ? NetworkConditionTestCase.All : new[] { NetworkConditionTestCase.NetworkConditionPerfect },
                    clientProxyTypesToTest,
                    serviceAsyncHalibutFeatureTestCases
                );

                return builder.Build();
            }
        }
    }

    //TODO: @server-at-scale - When NUnit is removed, replace the above LatestAndPreviousClientAndServiceVersionsTestCasesAttribute with this.
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class LatestAndPreviousClientAndServiceVersionsTestCasesXUnitAttribute : DataAttribute
    {
        readonly bool testWebSocket;
        readonly bool testNetworkConditions;
        readonly bool testListening;
        readonly bool testPolling;
        readonly bool testAsyncAndSyncClients;
        readonly bool testAsyncServicesAsWell;

        public LatestAndPreviousClientAndServiceVersionsTestCasesXUnitAttribute(
            bool testWebSocket = true,
            bool testNetworkConditions = true,
            bool testListening = true,
            bool testPolling = true,
            bool testAsyncAndSyncClients = true,
            bool testAsyncServicesAsWell = false // False means only the sync service will be tested.
        )
        {
            this.testWebSocket = testWebSocket;
            this.testNetworkConditions = testNetworkConditions;
            this.testListening = testListening;
            this.testPolling = testPolling;
            this.testAsyncAndSyncClients = testAsyncAndSyncClients;
            this.testAsyncServicesAsWell = testAsyncServicesAsWell;
        }

        public override IEnumerable<object[]> GetData(MethodInfo testMethod)
        {
            //TODO: @server-at-scale - When NUnit is removed, move LatestClientAndLatestServiceTestCases.GetEnumerator into here.

            return LatestAndPreviousClientAndServiceVersionsTestCases.GetEnumerator(
                testWebSocket,
                testNetworkConditions,
                testListening,
                testPolling,
                testAsyncAndSyncClients,
                testAsyncServicesAsWell);
        }
    }

}
