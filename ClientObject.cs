using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Linki.SharedResources;

namespace Linki.Server
{
    internal class ClientObject
    {
        private TcpClient client;
        private ServerObject serverObject;
        private TcpListener clientListener;
        private Queue<Request> requests = new Queue<Request>();
        private Queue<Response> responses = new Queue<Response>();

        public string connectionID { get; } = Guid.NewGuid().ToString();

        public ClientObject(TcpClient tcpClient, ServerObject serverObject)
        {
            client = tcpClient;
            this.serverObject = serverObject;
            clientListener = new TcpListener(new IPEndPoint(IPAddress.Loopback, 0));
            clientListener.Start();
        }
        public IPEndPoint GetEndPoint()
        {
            var endPoint = (IPEndPoint)(clientListener.LocalEndpoint);
            return endPoint;
        }

        public void Close()
        {
            client.Close();
            client.Dispose();
            clientListener.Stop();
            clientListener.Server.Close();
            clientListener.Server.Dispose();
        }

        public bool IsConnectedToClient()
        {
            return client.Connected && !client.Client.Poll(10, SelectMode.SelectRead);
        }

        public async Task ProcessAsync()
        {
            HandleRequests();
            ReceiveRequests();
            SendResponses();
        }

        public void AddResponse(Response response)
        {
            responses.Enqueue(response);
        }

        private async Task ReceiveRequests()
        {
            StringBuilder builder = new StringBuilder();
            string jsonRequestQuery;
            Request request = new Request();
            while (true)
            {
                try
                {
                    byte[] bufferByte = new byte[1];
                    await client.Client.ReceiveAsync(bufferByte, SocketFlags.None);
                    char c = (char)bufferByte[0];
                    if (c != '\n')
                    {
                        builder.Append(c);
                    }
                    else
                    {
                        jsonRequestQuery = builder.ToString();
                        builder.Clear();
                        request = (Request)QueryJsonConverter.DeserializeQuery(jsonRequestQuery);
                        requests.Enqueue(request);
                    }
                }

                catch (Exception ex)
                {

                }
            }
        }

        private async Task HandleRequests()
        {
            while (true)
            {
                if (requests.Count != 0)
                {
                    Request request = requests.Dequeue();
                    //.. обработка реквестов разного типа
                }
                else
                    continue;
            }
        }

        private async Task SendResponses()
        {
            while (true)
            {
                try
                {
                    if (responses.Count != 0)
                    {
                        Response response = responses.Dequeue();
                        string jsonResponse = QueryJsonConverter.SerializeQueryMessage(response) + "\n";
                        byte[] data = Encoding.UTF8.GetBytes(jsonResponse);
                        await client.Client.SendAsync(data, SocketFlags.None);
                    }
                }
                catch (Exception ex)
                {

                }
            }
        }
    }
}
