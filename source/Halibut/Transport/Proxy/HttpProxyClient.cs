/*
 *  Authors:  Benton Stark
 * 
 *  Copyright (c) 2007-2009 Starksoft, LLC (http://www.starksoft.com) 
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 * 
 */

using System;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Transport.Proxy.Exceptions;
using Halibut.Transport.Streams;

namespace Halibut.Transport.Proxy
{
    /// <summary>
    /// HTTP connection proxy class.  This class implements the HTTP standard proxy protocol.
    /// <para>
    /// You can use this class to set up a connection to an HTTP proxy server.  Calling the 
    /// CreateConnection() method initiates the proxy connection and returns a standard
    /// System.Net.Socks.TcpClient object that can be used as normal.  The proxy plumbing
    /// is all handled for you.
    /// </para>
    /// <code>
    /// 
    /// </code>
    /// </summary>
    public class HttpProxyClient : IProxyClient
    {
        private HttpResponseCodes _respCode;
        private string _respText;
        readonly ILog log;

        private const string HTTP_PROXY_CONNECT_CMD = "CONNECT {0}:{1} HTTP/1.0\r\nHost: {0}:{1}\r\n\r\n";
        private const string HTTP_PROXY_AUTH_CONNECT_CMD = "CONNECT {0}:{1} HTTP/1.0\r\nHost: {0}:{1}\r\nProxy-Authorization: Basic {2}\r\n\r\n";
        private const int WAIT_FOR_DATA_INTERVAL = 50; // 50 ms
        private const int WAIT_FOR_DATA_TIMEOUT = 15000; // 15 seconds
        private const string PROXY_NAME = "HTTP";

        private enum HttpResponseCodes
        {
            None = 0,
            Continue = 100,
            SwitchingProtocols = 101,
            OK = 200,
            Created = 201,
            Accepted = 202,
            NonAuthoritiveInformation = 203,
            NoContent = 204,
            ResetContent = 205,
            PartialContent = 206,
            MultipleChoices = 300,
            MovedPermanetly = 301,
            Found = 302,
            SeeOther = 303,
            NotModified = 304,
            UserProxy = 305,
            TemporaryRedirect = 307,
            BadRequest = 400,
            Unauthorized = 401,
            PaymentRequired = 402,
            Forbidden = 403,
            NotFound = 404,
            MethodNotAllowed = 405,
            NotAcceptable = 406,
            ProxyAuthenticantionRequired = 407,
            RequestTimeout = 408,
            Conflict = 409,
            Gone = 410,
            PreconditionFailed = 411,
            RequestEntityTooLarge = 413,
            RequestURITooLong = 414,
            UnsupportedMediaType = 415,
            RequestedRangeNotSatisfied = 416,
            ExpectationFailed = 417,
            InternalServerError = 500,
            NotImplemented = 501,
            BadGateway = 502,
            ServiceUnavailable = 503,
            GatewayTimeout = 504,
            HTTPVersionNotSupported = 505
        }

        /// <summary>
        /// Constructor.  
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="proxyHost">Host name or IP address of the proxy server.</param>
        /// <param name="proxyPort">Port number for the proxy server.</param>
        /// <param name="proxyUserName">Proxy authentication user name.</param>
        /// <param name="proxyPassword">Proxy authentication password.</param>
        public HttpProxyClient(ILog logger, string proxyHost, int proxyPort, string proxyUserName, string proxyPassword, IStreamFactory streamFactory)
        {
            log = logger;
            if (string.IsNullOrEmpty(proxyHost))
                throw new ArgumentNullException(nameof(proxyHost));

            if (proxyPort <= 0 || proxyPort > 65535)
                throw new ArgumentOutOfRangeException(nameof(proxyPort), "port must be greater than zero and less than 65535");

            ProxyHost = proxyHost;
            ProxyPort = proxyPort;
            ProxyUserName = proxyUserName;
            ProxyPassword = proxyPassword;
            this.streamFactory = streamFactory;
        }

        /// <summary>
        /// Gets or sets host name or IP address of the proxy server.
        /// </summary>
        public string ProxyHost { get; set; }

        /// <summary>
        /// Gets or sets port number for the proxy server.
        /// </summary>
        public int ProxyPort { get; set; }

        /// <summary>
        /// Gets String representing the name of the proxy. 
        /// </summary>
        /// <remarks>This property will always return the value 'HTTP'</remarks>
        public string ProxyName
        {
            get { return PROXY_NAME; }
        }

        /// <summary>
        /// Gets or sets proxy authentication user name.
        /// </summary>
        public string ProxyUserName { get; set; }

        /// <summary>
        /// Gets or sets proxy authentication password.
        /// </summary>
        public string ProxyPassword { get; set; }

        /// <summary>
        /// Gets or sets the TcpClient object. 
        /// This property can be set prior to executing CreateConnection to use an existing TcpClient connection.
        /// </summary>
        public TcpClient TcpClient { get; set; }
        
        readonly IStreamFactory streamFactory;

        Func<TcpClient> tcpClientFactory = () => new TcpClient();

        public IProxyClient WithTcpClientFactory(Func<TcpClient> tcpClientfactory)
        {
            tcpClientFactory = tcpClientfactory;
            return this;
        }
        
        public async Task<TcpClient> CreateConnectionAsync(string destinationHost, int destinationPort, TimeSpan timeout, CancellationToken cancellationToken)
        {
            try
            {
                // if we have no connection, create one
                if (TcpClient == null)
                {
                    if (string.IsNullOrEmpty(ProxyHost))
                        throw new ProxyException("ProxyHost property must contain a value", false);

                    if (ProxyPort <= 0 || ProxyPort > 65535)
                        throw new ProxyException("ProxyPort value must be greater than zero and less than 65535", false);

                    if(ProxyHost.Contains("://"))
                        throw new ProxyException("The proxy's hostname cannot contain a protocol prefix (eg http://)", false);


                    //  create new tcp client object to the proxy server
                    TcpClient = tcpClientFactory();

                    // attempt to open the connection
                    log.Write(EventType.Diagnostic, "Connecting to proxy at {0}:{1}", ProxyHost, ProxyPort);
                    await TcpClient.ConnectWithTimeoutAsync(ProxyHost, ProxyPort, timeout, cancellationToken);
                    log.Write(EventType.Diagnostic, "Connected to proxy at {0}:{1}", ProxyHost, ProxyPort);
                }

                //  send connection command to proxy host for the specified destination host and port
                await SendConnectionCommandAsync(destinationHost, destinationPort, cancellationToken);

                // return the open proxied tcp client object to the caller for normal use
                return TcpClient;
            }
            catch (AggregateException ae)
            {
                var se = ae.InnerExceptions.OfType<SocketException>().FirstOrDefault();
                if (se != null)
                    throw new ProxyException($"Connection to proxy host {ProxyHost} on port {ProxyPort} failed: {se.Message}", se, true);

                throw;
            }
            catch (SocketException ex)
            {
                throw new ProxyException($"Connection to proxy host {ProxyHost} on port {ProxyPort} failed: {ex.Message}", ex, true);
            }
        }
        
        async Task SendConnectionCommandAsync(string host, int port, CancellationToken cancellationToken)
        {
            var stream = streamFactory.CreateStream(TcpClient);
            var connectCmd = GetConnectCmd(host, port);
            var request = Encoding.ASCII.GetBytes(connectCmd);

            // send the connect request
            await stream.WriteAsync(request, 0, request.Length, cancellationToken);

            // PROXY SERVER RESPONSE
            // =======================================================================
            //HTTP/1.0 200 Connection Established<CR><LF>
            //[.... other HTTP header lines ending with <CR><LF>..
            //ignore all of them]
            //<CR><LF>    // Last Empty Line

            var sbuilder = new StringBuilder();

            using (var _ = stream.WithTemporaryReadTimeout(WAIT_FOR_DATA_TIMEOUT)) {
                var response = new byte[1]; // Read 1 byte at a time to prevent over read. (This is not efficient but this is only done once per stream)
                while (!sbuilder.ToString().EndsWith("\r\n\r\n"))
                {
                    var bytes = await stream.ReadAsync(response, 0, response.Length, cancellationToken);
                    sbuilder.Append(Encoding.UTF8.GetString(response, 0, bytes));
                }
            }

            ParseResponse(sbuilder.ToString());
            
            //  evaluate the reply code for an error condition
            if (_respCode != HttpResponseCodes.OK)
                HandleProxyCommandError(host, port);
        }

        string GetConnectCmd(string host, int port)
        {
            if (string.IsNullOrWhiteSpace(ProxyUserName))
            {
                log.Write(EventType.Diagnostic, "Sending unauthorized server CONNECT command for {0}:{1} to proxy", host, port.ToString(CultureInfo.InvariantCulture));
                return string.Format(CultureInfo.InvariantCulture, HTTP_PROXY_CONNECT_CMD, host, port.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                log.Write(EventType.Diagnostic, "Sending authorized server CONNECT command for {0}:{1} to proxy", host, port.ToString(CultureInfo.InvariantCulture));
                var userNameAndPassword = EncodeTo64(ProxyUserName + ":" + ProxyPassword);
                return string.Format(CultureInfo.InvariantCulture, HTTP_PROXY_AUTH_CONNECT_CMD, host, port.ToString(CultureInfo.InvariantCulture), userNameAndPassword);
            }
        }

        static string EncodeTo64(string toEncode)
        {
            var bytes = Encoding.ASCII.GetBytes(toEncode);
            return Convert.ToBase64String(bytes);
        }

        void HandleProxyCommandError(string host, int port)
        {
            string msg;

            switch (_respCode)
            {
                case HttpResponseCodes.None:
                    msg = string.Format(CultureInfo.InvariantCulture, "Proxy destination {0} on port {1} failed to return a recognized HTTP response code.  Server response: {2}", Utils.GetHost(TcpClient), Utils.GetPort(TcpClient), _respText);
                    break;

                case HttpResponseCodes.BadGateway:
                    //HTTP/1.1 502 Proxy Error (The specified Secure Sockets Layer (SSL) port is not allowed. ISA Server is not configured to allow SSL requests from this port. Most Web browsers use port 443 for SSL requests.)
                    msg = string.Format(CultureInfo.InvariantCulture, "Proxy destination {0} on port {1} responded with a 502 code - Bad Gateway.  If you are connecting to a Microsoft ISA destination please refer to knowledge based article Q283284 for more information.  Server response: {2}", Utils.GetHost(TcpClient), Utils.GetPort(TcpClient), _respText);
                    break;

                default:
                    msg = string.Format(CultureInfo.InvariantCulture, "Proxy destination {0} on port {1} responded with a {2} code - {3}", Utils.GetHost(TcpClient), Utils.GetPort(TcpClient), ((int)_respCode).ToString(CultureInfo.InvariantCulture), _respText);
                    break;
            }

            //  throw a new application exception 
            throw new ProxyException(msg, false);
        }

        void ParseResponse(string response)
        {
            //  get rid of the LF character if it exists and then split the string on all CR
            var data = response.Replace('\n', ' ').Split('\r');
            
            ParseCodeAndText(data[0]);
        }

        void ParseCodeAndText(string line)
        {
            if (line.IndexOf("HTTP") == -1)
                throw new ProxyException(string.Format("No HTTP response received from proxy destination.  Server response: {0}.", line), false);

            var begin = line.IndexOf(" ") + 1;
            var end = line.IndexOf(" ", begin);

            var val = line.Substring(begin, end - begin);

            int code;
            if (!int.TryParse(val, out code))
                throw new ProxyException(string.Format("An invalid response code was received from proxy destination.  Server response: {0}.", line), false);

            _respCode = (HttpResponseCodes)code;
            _respText = line.Substring(end + 1).Trim();
        }
    }
}
