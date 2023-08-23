using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Halibut.Transport
{
    class TcpClientManager
    {
        readonly Dictionary<string, HashSet<TcpClient>> activeClients = new();

        public void AddActiveClient(string thumbprint, TcpClient client)
        {
            lock (activeClients)
            {
                if (activeClients.TryGetValue(thumbprint, out var tcpClients))
                {
                    tcpClients.RemoveWhere(c => !c.Connected);
                    tcpClients.Add(client);
                }
                else
                {
                    tcpClients = new HashSet<TcpClient> {client};
                    activeClients.Add(thumbprint, tcpClients);
                }
            }
        }

        // TODO - ASYNC ME UP - Should be obsolete but in use and needs an async chain
        public void Disconnect(string thumbprint)
        {
            lock (activeClients)
            {
                if (activeClients.TryGetValue(thumbprint, out var tcpClients))
                {
                    foreach (var client in tcpClients)
                    {
                        client.Close();
                    }
                }
                activeClients.Remove(thumbprint);
            }
        }

        public async Task DisconnectAsync(string thumbprint)
        {
            HashSet<TcpClient> tcpClients;
            lock (activeClients)
            {
                activeClients.TryGetValue(thumbprint, out tcpClients);
                activeClients.Remove(thumbprint);
            }

            if (tcpClients != null)
            {
                var closeTasks = tcpClients.Select(c => Task.Run(c.Close));
                await Task.WhenAll(closeTasks);
            }
        }

        static readonly TcpClient[] NoClients = Array.Empty<TcpClient>();
        public IReadOnlyCollection<TcpClient> GetActiveClients(string thumbprint)
        {
            lock (activeClients)
            {
                if (activeClients.TryGetValue(thumbprint, out var value))
                {
                    return value.ToArray();
                }
            }

            return NoClients;
        }

        public void RemoveClient(TcpClient client)
        {
            lock (activeClients)
            {
                foreach(var thumbprintClientsPair in activeClients)
                {
                    if (thumbprintClientsPair.Value.Contains(client))
                        thumbprintClientsPair.Value.Remove(client);
                }

                var thumbprintsWithNoClients = activeClients
                    .Where(x => x.Value.Count == 0)
                    .Select(x => x.Key)
                    .ToArray();
                foreach (var thumbprint in thumbprintsWithNoClients)
                    activeClients.Remove(thumbprint);
            }
        }
    }
}
