using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Linki.Server
{
    internal class ClientObject
    {
        TcpClient client;
        ServerObject server;
        TcpListener listener;
        public ClientObject(TcpClient tcpClient, ServerObject serverObject)
        {
            client = tcpClient;
            server = serverObject;
            listener = new TcpListener(new IPEndPoint(IPAddress.Loopback, 0));
            listener.Start();
        }

        public IPEndPoint GetEndPoint()
        {
            var endPoint = (IPEndPoint)(listener.LocalEndpoint);
            return endPoint;
        }

        public async Task ProcessAsync()
        {

        }
    }
}
