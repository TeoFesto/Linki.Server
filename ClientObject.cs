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
        private TcpClient client;
        private ServerObject serverObject;
        private TcpListener clientListener;
        private Queue<Request> requests = new Queue<Request>();
        private Queue<Response> responses = new Queue<Response>();
        private const string sqlConnectionString = "Server=(localdb)\\mssqllocaldb;Database=LinkiDB;Trusted_Connection=True;";
        private SqlConnection databaseConnection = new SqlConnection(sqlConnectionString);

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
            }
        }

        private async Task HandleRequests()
        {
            while (true)
            {
                if (requests.Count != 0)
                {
                    Request request = requests.Dequeue();
                    if(request is SignUpRequest signUpRequest)
                    {
                        bool isSingedUp;
                        string statusMessage = "";

                        string sqlExpression = "SELECT COUNT(Login) FROM Users WHERE Login = @login";
                        SqlCommand command = new SqlCommand(sqlExpression, databaseConnection);
                        command.Parameters.Add(new SqlParameter("@login", signUpRequest.Login));
                        int loginCount = (int)(await command.ExecuteScalarAsync());
                        if (loginCount > 0)
                        {
                            statusMessage += "- Логин уже занят\n";
                        }

                        sqlExpression = "SELECT COUNT(Email) FROM Users WHERE Email = @email";
                        command = new SqlCommand(sqlExpression, databaseConnection);
                        command.Parameters.Add(new SqlParameter("@email", signUpRequest.Email));
                        int emailCount = (int)(await command.ExecuteScalarAsync());
                        if (emailCount > 0)
                            statusMessage += "- E-mail уже занят";

                        SignUpResponse signUpResponse = new SignUpResponse();
                        if (statusMessage != "")
                        {
                            isSingedUp = false;
                        }
                        else
                        {
                            sqlExpression = "INSERT INTO Users (Login, Password, Nickname, Email) values " +
                                "(@login, @password, @nickname, @email)";
                            command = new SqlCommand(sqlExpression, databaseConnection);
                            command.Parameters.Add(new SqlParameter("@login", signUpRequest.Login));
                            command.Parameters.Add(new SqlParameter("@password", signUpRequest.Password));
                            command.Parameters.Add(new SqlParameter("@nickname", signUpRequest.Nickname));
                            command.Parameters.Add(new SqlParameter("@email", signUpRequest.Email));
                            
                            // нет проверки на длину данных, ну да ладно
                            await command.ExecuteNonQueryAsync();
                            statusMessage = "Регистрация успешно завершена. Можете войти в аккаунт.";
                            isSingedUp = true;
                        }
                        signUpResponse.StatusMessage = statusMessage;
                        signUpResponse.isSignedUp = isSingedUp;
                        responses.Enqueue(signUpResponse);
                    }
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
