using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using Linki.SharedResources;
using Microsoft.Data.SqlClient;

namespace Linki.Server
{

    
    internal class RequestHandler
    {
        private ClientObject clientObject;
        public RequestHandler(ClientObject clientObject)
        {
            this.clientObject = clientObject;
        }
        public async Task Handle(Request request)
        {
            string requestName = request.GetType().Name;
            string requestHandlerName = "Handle" + requestName;
            var RequestHandlerType = typeof(RequestHandler);
            MethodInfo handler = RequestHandlerType.GetMethod(requestHandlerName);
            await Task.Run(() =>
            {
                handler.Invoke(this, new object[] { request });
            });
        }

        public async Task HandleSignUpRequest(SignUpRequest signUpRequest)
        {
           bool isSingedUp;
           string statusMessage = "";

           string sqlExpression = "SELECT COUNT(Login) FROM Users WHERE Login = @login";
           
           SqlCommand command = new SqlCommand(sqlExpression, clientObject.databaseConnection);
           command.Parameters.Add(new SqlParameter("@login", signUpRequest.Login));
           int loginCount = (int)(await command.ExecuteScalarAsync());
           if (loginCount > 0)
           {
               statusMessage += "- Логин уже занят\n";
           }

           sqlExpression = "SELECT COUNT(Email) FROM Users WHERE Email = @email";
           command = new SqlCommand(sqlExpression, clientObject.databaseConnection);
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
               command = new SqlCommand(sqlExpression, clientObject.databaseConnection);
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
           clientObject.AddResponse(signUpResponse);
        }
    }
}
