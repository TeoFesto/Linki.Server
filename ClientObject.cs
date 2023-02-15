using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Linki.SharedResources;
using Microsoft.Data.SqlClient;

namespace Linki.Server
{
    internal class ClientObject
    {
        public TcpClient client { get; set; }
        private ServerObject serverObject { get; set; }
        private TcpListener clientListener { get; set; }
        private Queue<Request> requests { get; set; } = new Queue<Request>();
        private Queue<Response> responses { get; set; } = new Queue<Response>();
        public static readonly string sqlConnectionString = new string("Server=(localdb)\\mssqllocaldb;Database=LinkiDB;Trusted_Connection=True;");
        public SqlConnection databaseConnection { get; set; } = new SqlConnection(sqlConnectionString);

        public string connectionID { get; } = Guid.NewGuid().ToString();

        public ClientObject(ServerObject serverObject)
        {
            this.serverObject = serverObject;
            clientListener = new TcpListener(new IPEndPoint(IPAddress.Loopback, 0));
            try
            {
                databaseConnection.Open();
                clientListener.Start();
                Task.Run(AcceptClientConnection);
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                this.Close();
            }
        }
        public IPEndPoint GetEndPoint()
        {
            var endPoint = (IPEndPoint)(clientListener.LocalEndpoint);
            return endPoint;
        }

        public async void AcceptClientConnection()
        {
            client = await clientListener.AcceptTcpClientAsync();
            Console.WriteLine($"Клиент {connectionID} ({client.Client.RemoteEndPoint.ToString()}) подключился");
            clientListener.Server.Close();
            clientListener = null;
        }

        public void Close()
        {
            client.Close();
            client.Dispose();
            clientListener?.Stop();
            clientListener?.Server?.Close();
            clientListener?.Server?.Dispose();
            databaseConnection.Close();
            Console.WriteLine($"Подключение {connectionID} закрыто.");
        }

        public bool IsConnectedToClient()
        {
            return client.Connected && !client.Client.Poll(10, SelectMode.SelectRead);
        }

        public void StartProcess()
        {
            Task.Run(HandleRequests);
            Task.Run(ReceiveRequests);
            Task.Run(SendResponses);
        }

        public void AddResponse(Response response)
        {
            responses.Enqueue(response);
        }

        private async Task ReceiveRequests()
        {
            List<byte> bytes = new List<byte>();
            string jsonRequestQuery;
            Request request = new Request();
            while (true)
            {
                if(client.Available > 0)
                {
                    try
                    {
                        byte[] bufferByte = new byte[1];
                        await client.Client.ReceiveAsync(bufferByte, SocketFlags.None);
                        char c = (char)bufferByte[0];
                        if (c != '\n')
                        {
                            bytes.Add(bufferByte[0]);
                        }
                        else
                        {
                            jsonRequestQuery = Encoding.UTF8.GetString(bytes.ToArray());
                            bytes.Clear();
                            request = (Request)QueryJsonConverter.DeserializeQuery(jsonRequestQuery);
                            requests.Enqueue(request);
                        }
                    }

                    catch (Exception ex)
                    {

                    }
                }
                else
                {
                    Thread.Sleep(10);
                    continue;
                }
            }
        }

        private async Task HandleRequests()
        {
            RequestHandler requestHandler = new RequestHandler(this);
            while (true)
            {
                if (requests.Count != 0)
                {
                    Request request = requests.Dequeue();
                    requestHandler.Handle(request);
                }
                else
                {
                    Thread.Sleep(10);
                    continue;
                }
            }
        }

        private async Task SendResponses()
        {
            while (true)
            {
                if (responses.Count != 0)
                {
                    try
                    {
                        Response response = responses.Dequeue();
                        string jsonResponse = QueryJsonConverter.SerializeQueryMessage(response) + "\n";
                        byte[] data = Encoding.UTF8.GetBytes(jsonResponse);
                        await client.Client.SendAsync(data, SocketFlags.None);

                    }
                    catch(Exception ex)
                    {

                    }
                }
                else
                {
                    Thread.Sleep(10);
                    continue;
                }
            }
        }
    }
}
