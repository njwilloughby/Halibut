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
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Transport.Proxy
{
    /// <summary>
    /// Proxy client interface.  This is the interface that all proxy clients must implement.
    /// </summary>
    public interface IProxyClient
    {
        /// <summary>
        /// Gets or sets proxy host name or IP address.
        /// </summary>
        string ProxyHost { get; set; }
        
        /// <summary>
        /// Gets or sets proxy port number.
        /// </summary>
        int ProxyPort { get; set; }

        /// <summary>
        /// Gets String representing the name of the proxy.
        /// </summary>
        string ProxyName { get; }

        /// <summary>
        /// Gets or set the TcpClient object if one was specified in the constructor.
        /// </summary>
        TcpClient TcpClient { get; }

        IProxyClient WithTcpClientFactory(Func<TcpClient> tcpClientfactory);

        Task<TcpClient> CreateConnectionAsync(string destinationHost, int destinationPort, TimeSpan timeout, CancellationToken cancellationToken);
    }
}
    