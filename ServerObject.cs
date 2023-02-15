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
using System.Collections;
using System.Diagnostics;

namespace Linki.Server
{
    internal class ServerObject
    {
        private TcpListener clientConnectionListener = new TcpListener(8888);
        private List<ClientObject> clientConnections = new List<ClientObject>();
        private Dictionary<string, ClientObject> authorizedClients = new Dictionary<string, ClientObject>();
        public async Task ListenAsync()
        {
            clientConnectionListener.Start();
            Console.WriteLine("Сервер запущен.");

            while (true)
            {
                try
                {
                    Console.WriteLine("Ожидание подключения клиента...");
                    TcpClient tcpClient = await clientConnectionListener.AcceptTcpClientAsync();
                    Console.WriteLine($"Происходит подключение {tcpClient.Client.RemoteEndPoint.ToString()}. Обработка...");
                    ClientObject clientObject = new ClientObject(this);
                    clientConnections.Add(clientObject);
                    IPEndPoint clientObjectIPEndPoint = clientObject.GetEndPoint();
                    ServerConnectionResponse serverConnectionQueryMessage = new ServerConnectionResponse(clientObjectIPEndPoint);
                    string responseQueryJson = QueryJsonConverter.SerializeQueryMessage(serverConnectionQueryMessage);
                    byte[] responseData = Encoding.UTF8.GetBytes(responseQueryJson + '\n');
                    await tcpClient.Client.SendAsync(responseData, SocketFlags.None);
                    byte[] confirmData = new byte[17];
                    string? confirmMessage = null;
                    const string confirm = "CONFIRM ENDPOINT\n";

                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();
                    while (true)
                    {
                        if (stopwatch.ElapsedMilliseconds >= 15000)
                            break;
                        if(tcpClient.Available == 0)
                        {
                            continue;
                        }
                        else
                        {
                            await tcpClient.Client.ReceiveAsync(confirmData, SocketFlags.None);
                            confirmMessage = Encoding.UTF8.GetString(confirmData);
                            break;
                        }
                    }
                    stopwatch.Stop();

                    if (confirmMessage == confirm)
                    {
                        clientObject.StartProcess();
                        Console.WriteLine("Обработка клиента завершена. Ожидание следующего клиента...");
                    }
                    else
                    {
                        clientObject.Close();
                        clientConnections.Remove(clientObject);
                        throw new Exception($"Корректного подтверждение от клиента не пришло. Пришедшее сообщение: {confirmMessage}");
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                Thread.Sleep(10);
            }
        }

        public async Task CheckClientObjectConnectionsAsync()
        {
            while (true)
            {
                List<ClientObject> disconnectedConnections = new List<ClientObject>();
                foreach (ClientObject connection in clientConnections)
                {
                    if (!connection.IsConnectedToClient())
                    {
                        connection.Close();
                        disconnectedConnections.Add(connection);
                    }
                }

                foreach (ClientObject disconnection in disconnectedConnections)
                {
                    clientConnections.Remove(disconnection);
                }

                int waitSeconds = 2;
                Thread.Sleep(waitSeconds * 1000);
            }
        }
    }
}
