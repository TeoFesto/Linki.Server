using Linki.Server;
using System;
using System.Net.Sockets;
using Linki.SharedResources;


ServerObject server = new ServerObject();
var checkClientConnectionsTask = Task.Run(server.CheckClientObjectConnectionsAsync);
var serverListenerTask = Task.Run(server.ListenAsync);

await Task.WhenAll(serverListenerTask, checkClientConnectionsTask);
