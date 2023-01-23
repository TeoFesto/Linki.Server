using Linki.Server;
using System;
using System.Net.Sockets;
using Linki.SharedResources;


ServerObject server = new ServerObject();
await server.ListenAsync();
