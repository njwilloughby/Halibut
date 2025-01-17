using System;
using System.Threading;
using Halibut.Tests.Support;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using ILogger = Serilog.ILogger;

namespace Halibut.Tests
{
    public class BaseTest
    {
        CancellationTokenSource? cancellationTokenSource;
        TraceLogFileLogger? traceLogFileLogger;

        public CancellationToken CancellationToken { get; private set; }
        public ILogger Logger { get; private set; } = null!;


        [SetUp]
        public void SetUp()
        {
            traceLogFileLogger = new TraceLogFileLogger();
            Logger = new SerilogLoggerBuilder()
                .SetTraceLogFileLogger(traceLogFileLogger)
                .Build()
                .ForContext(GetType());

            Logger.Information("Test started");
            cancellationTokenSource = new CancellationTokenSource();
            CancellationToken = cancellationTokenSource.Token;
        }

        [TearDown]
        public void TearDown()
        {
            Logger.Information("Staring Test Tearing Down");

            Logger.Information("Cancelling CancellationTokenSource");
            cancellationTokenSource?.Cancel();
            Logger.Information("Disposing CancellationTokenSource");
            cancellationTokenSource?.Dispose();

            if (TestContext.CurrentContext.Result.Outcome != ResultState.Success)
            {
                if (traceLogFileLogger?.CopyLogFileToArtifacts() ?? false)
                {
                    Logger.Information("Copied trace logs to artifacts");
                }
                else
                {
                    Logger.Information("Could not copy trace logs to artifacts");
                }
            }

            Logger.Information("Disposing Trace Log File Logger");
            traceLogFileLogger?.Dispose();

            Logger.Information("Finished Test Tearing Down");
        }
    }
}
