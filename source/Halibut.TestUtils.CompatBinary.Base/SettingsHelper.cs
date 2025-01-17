﻿using System;
using System.Security.Cryptography.X509Certificates;
using Halibut.Logging;
using Halibut.Transport.Proxy;

namespace Halibut.TestUtils.SampleProgram.Base
{
    static class SettingsHelper
    {
        public static ProxyDetails GetProxyDetails()
        {
            var host = GetSetting("proxydetails_host");

            if (!string.IsNullOrWhiteSpace(host))
            {
                var password = GetSetting("proxydetails_password");
                var userName = GetSetting("proxydetails_username");
                var port = GetSetting("proxydetails_port");
                var type = GetSetting("proxydetails_type");

                Console.WriteLine($"Using Proxy Details Hot:{host} Port:{port} Type:{type} Username:{userName} Password:{password}");

                return new ProxyDetails(host, AsInt(port), AsPortType(type), userName, password);

                ProxyType AsPortType(string s)
                {
                    return (ProxyType)Enum.Parse(typeof(ProxyType), s);
                }

                int AsInt(string s)
                {
                    return int.Parse(s);
                }
            }

            Console.WriteLine("Not using an Http Proxy");
            return null;
        }

        public static string GetSetting(string name)
        {
            return Environment.GetEnvironmentVariable(name);
        }
        
        static bool GetMandatoryBool(string name)
        {
            var boolAsString = GetSetting(name);
            if (string.IsNullOrEmpty(boolAsString))
            {
                throw new Exception($"Env var {name} must be set");
            }

            if (bool.TryParse(boolAsString, out var boolValue))
            {
                return boolValue;
            }
            throw new Exception($"Env var {name} must be a bool it was: {boolAsString}");
        }

        public static ServiceConnectionType GetServiceConnectionType()
        {
            return AsServiceConnectionType(GetSetting("ServiceConnectionType"));
        }

        public static LogLevel GetHalibutLogLevel()
        {
            return AsLogLevel(GetSetting("halibutloglevel"));

            LogLevel AsLogLevel(string s)
            {
                return (LogLevel)Enum.Parse(typeof(LogLevel), s);
            }
        }

        public static X509Certificate2 GetClientCertificate()
        {
            var octopusCertPath = GetSetting("octopuscertpath");
            //Console.WriteLine($"Using octopus cert path: {octopusCertPath}");
            var clientCert = new X509Certificate2(octopusCertPath);
            //Console.WriteLine("Octopus/Client cert details " + clientCert);

            return clientCert;
        }

        public static string GetClientThumbprint()
        {
            var thumbprint = GetSetting("octopusthumbprint");
            //Console.WriteLine($"Using octopus thumbprint: {thumbprint}");

            return thumbprint;
        }

        public static X509Certificate2 GetServiceCertificate()
        {
            var tentacleCertPath = GetSetting("tentaclecertpath");
            //Console.WriteLine($"Using tentacle cert path: {tentacleCertPath}");
            var serviceCert = new X509Certificate2(tentacleCertPath);
            //Console.WriteLine("Tentacle/service cert details " + serviceCert);

            return serviceCert;
        }

        static ServiceConnectionType AsServiceConnectionType(string s)
        {
            if (Enum.TryParse(s, out ServiceConnectionType serviceConnectionType))
            {
                return serviceConnectionType;
            }

            throw new Exception($"Unknown service type '{s}'");
        }

        public static string CompatBinaryStayAliveLockFile()
        {
            var stayAliveFilePath = GetSetting("CompatBinaryStayAliveFilePath");
            if (string.IsNullOrEmpty(stayAliveFilePath))
            {
                throw new Exception("Env var CompatBinaryStayAliveFilePath must be set");
            }
            
            Console.WriteLine($"Will die when the following file can be locked or is deleted '{stayAliveFilePath}'.");
            return stayAliveFilePath;
        }

        public static bool IsWithStandardServices()
        {
            return GetMandatoryBool("WithStandardServices");
        }

        public static bool IsWithCachingService()
        {
            return GetMandatoryBool("WithCachingService");
        }

        public static bool IsWithTentacleServices()
        {
            return GetMandatoryBool("WithTentacleServices");
        }

        public static TimeSpan GetTestTimeout()
        {
            var timeoutString = GetSetting("TestTimeout");

            if (string.IsNullOrWhiteSpace(timeoutString))
            {
                // If a test doesn't have a timeout lets assume we should not run forever.
                return TimeSpan.FromMinutes(15);
            }

            return TimeSpan.Parse(timeoutString);
        }
    }
}
