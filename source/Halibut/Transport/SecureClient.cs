using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Transport.Protocol;
using Halibut.Transport.Streams;
using Halibut.Util;

namespace Halibut.Transport
{
    public class SecureClient : ISecureClient
    {
        readonly ILog log;
        readonly IConnectionManager connectionManager;
        readonly X509Certificate2 clientCertificate;
        readonly ExchangeProtocolBuilder protocolBuilder;
        readonly TcpConnectionFactory tcpConnectionFactory;
        
        public SecureClient(ExchangeProtocolBuilder protocolBuilder, 
            ServiceEndPoint serviceEndpoint,
            X509Certificate2 clientCertificate,
            ILog log,
            IConnectionManager connectionManager,
            TcpConnectionFactory tcpConnectionFactory)
        {
            this.protocolBuilder = protocolBuilder;
            this.ServiceEndpoint = serviceEndpoint;
            this.clientCertificate = clientCertificate;
            this.log = log;
            this.connectionManager = connectionManager;
            this.tcpConnectionFactory = tcpConnectionFactory;
        }

        public ServiceEndPoint ServiceEndpoint { get; }
        
        public async Task ExecuteTransactionAsync(ExchangeActionAsync protocolHandler, CancellationToken requestCancellationToken)
        {
            var retryInterval = ServiceEndpoint.RetryListeningSleepInterval;

            Exception lastError = null;

            // retryAllowed is also used to indicate if the error occurred before or after the connection was made
            var retryAllowed = true;
            var watch = Stopwatch.StartNew();
            for (var i = 0; i < ServiceEndpoint.RetryCountLimit && retryAllowed && watch.Elapsed < ServiceEndpoint.ConnectionErrorRetryTimeout; i++)
            {
                if (i > 0)
                {
                    await Task.Delay(retryInterval, requestCancellationToken).ConfigureAwait(false);
                    log.Write(EventType.OpeningNewConnection, $"Retrying connection to {ServiceEndpoint.Format()} - attempt #{i}.");
                }

                try
                {
                    lastError = null;

                    IConnection connection = null;
                    try
                    {
                        connection = await connectionManager.AcquireConnectionAsync(
                            protocolBuilder, 
                            tcpConnectionFactory, 
                            ServiceEndpoint,
                            log, 
                            requestCancellationToken).ConfigureAwait(false);

                        // Beyond this point, we have no way to be certain that the server hasn't tried to process a request; therefore, we can't retry after this point
                        retryAllowed = false;
                        await protocolHandler(connection.Protocol, requestCancellationToken).ConfigureAwait(false);
                    }
                    catch
                    {
                        if (connection is not null)
                        {
                            await connection.DisposeAsync();
                        }

                        if (connectionManager.IsDisposed)
                        {
                            return;
                        }

                        throw;
                    }

                    // Only return the connection to the pool if all went well
                    await connectionManager.ReleaseConnectionAsync(ServiceEndpoint, connection, requestCancellationToken);
                }
                catch (AuthenticationException ex)
                {
                    lastError = ex;
                    retryAllowed = false;
                    break;
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionRefused)
                {
                    log.Write(EventType.Error, $"The remote host at {ServiceEndpoint.Format()} refused the connection. This may mean that the expected listening service is not running.");
                    lastError = ex;
                    retryAllowed = false;
                }
                catch (SocketException ex)
                {
                    log.WriteException(EventType.Error, $"Socket communication error while connecting to {ServiceEndpoint.Format()}", ex);
                    lastError = ex;
                    // When the host is not found an immediate retry isn't going to help
                    if (ex.SocketErrorCode == SocketError.HostNotFound)
                    {
                        break;
                    }
                }
                catch (ConnectionInitializationFailedException ex)
                {
                    log.WriteException(EventType.Error, $"Connection initialization failed while connecting to {ServiceEndpoint.Format()}", ex);
                    lastError = ex;
                    retryAllowed = true;

                    // If this is the second failure, clear the pooled connections as a precaution
                    // against all connections in the pool being bad
                    if (i == 1)
                    {
                        await connectionManager.ClearPooledConnectionsAsync(ServiceEndpoint, log, requestCancellationToken);
                    }
                }
                catch (IOException ex) when (ex.IsSocketConnectionReset())
                {
                    log.Write(EventType.Error, $"The remote host at {ServiceEndpoint.Format()} reset the connection. This may mean that the expected listening service does not trust the thumbprint {clientCertificate.Thumbprint} or was shut down.");
                    lastError = ex;
                }
                catch (IOException ex) when (ex.IsSocketConnectionTimeout())
                {
                    // Received on a polling client when the network connection is lost.
                    log.Write(EventType.Error, $"The connection to the host at {ServiceEndpoint.Format()} timed out. There may be problems with the network. The connection will be retried.");
                    lastError = ex;
                }
                catch (Exception ex)
                {
                    log.WriteException(EventType.Error, "Unexpected exception executing transaction.", ex);
                    lastError = ex;
                }
            }

            HandleError(lastError, retryAllowed);
        }

        void HandleError(Exception lastError, bool retryAllowed)
        {
            if (lastError == null)
                return;

            lastError = lastError.UnpackFromContainers();

            var innermost = lastError;
            while (innermost.InnerException != null)
                innermost = innermost.InnerException;
            
            if (innermost is SocketException se && !retryAllowed)
                if (se.SocketErrorCode == SocketError.ConnectionAborted || se.SocketErrorCode == SocketError.ConnectionReset)
                    throw new HalibutClientException($"The server {ServiceEndpoint.BaseUri} aborted the connection before it was fully established. This usually means that the server rejected the certificate that we provided. We provided a certificate with a thumbprint of '{clientCertificate.Thumbprint}'.");

            
            var error = new StringBuilder();
            error.Append("An error occurred when sending a request to '").Append(ServiceEndpoint.BaseUri).Append("', ");
            error.Append(retryAllowed ? "before the request could begin: " : "after the request began: ");
            error.Append(lastError.Message);
            
            throw new HalibutClientException(error.ToString(), lastError);
        }
    }
}
