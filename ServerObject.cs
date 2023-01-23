using Linki.SharedResources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Linki.Server
{
    internal class ServerObject
    {
        TcpListener clientConnectionListener = new TcpListener(8888);
        List<ClientObject> clients = new List<ClientObject>();
        
        public async Task ListenAsync()
        {
            try
            {
                clientConnectionListener.Start();
                Console.WriteLine("Сервер запущен. Ожидание подключений...");

                while (true)
                {
                    TcpClient tcpClient = await clientConnectionListener.AcceptTcpClientAsync();
                    int clientNumber = clients.Count + 1;
                    string clientEndPoint = tcpClient.Client.RemoteEndPoint.ToString();
                    Console.WriteLine($"Клиент #{clientNumber} ({clientEndPoint}) подключился. Обработка...");
                    ClientObject clientObject = new ClientObject(tcpClient, this);
                    clients.Add(clientObject);
                    Task.Run(clientObject.ProcessAsync);
                    IPEndPoint clientObjectIPEndPoint = clientObject.GetEndPoint();
                    Query query = new Query();
                    string quertyTypeName = typeof(ServerConnectionResponse).AssemblyQualifiedName;
                    query.applicationMessageType = quertyTypeName;
                    query.serializedApplicationMessage = JsonConvert.SerializeObject(new ServerConnectionResponse(clientObjectIPEndPoint));
                    string jsonQuery = JsonConvert.SerializeObject(query);
                    byte[] data = Encoding.UTF8.GetBytes(jsonQuery);
                    await tcpClient.Client.SendAsync(data, SocketFlags.None);
                    Console.WriteLine("Обработка клиента завершена. Ожидание следующего клиента...");
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

    }
}
