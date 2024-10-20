using System.Net.Sockets;
using System.Net;
using System.Text;

namespace ChatServer
{
    internal class ChatServer
    {
        class ClientInfo
        {
            public TcpClient Client { get; set; }
            public string Name { get; set; }
        }

        class Program
        {
            static TcpListener tcpListener;
            static List<ClientInfo> clients = new List<ClientInfo>();
            static object lockObject = new object();

            static void Main(string[] args)
            {
                tcpListener = new TcpListener(IPAddress.Any, 1010);
                tcpListener.Start();
                Console.WriteLine("Server started on port: 1010");

                Thread acceptClientsThread = new Thread(AcceptClients);
                acceptClientsThread.Start();
            }

            static void AcceptClients()
            {
                while (true)
                {
                    TcpClient newClient = tcpListener.AcceptTcpClient();
                    Thread clientThread = new Thread(() => HandleClient(newClient));
                    clientThread.Start();
                }
            }

            static void HandleClient(TcpClient client)
            {
                string clientName = "";
                NetworkStream stream = client.GetStream();
                byte[] buffer = new byte[1024];

                try
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    clientName = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

                    if (string.IsNullOrEmpty(clientName))
                    {
                        throw new Exception("Invalid client name.");
                    }

                    ClientInfo clientInfo = new ClientInfo { Client = client, Name = clientName };

                    lock (lockObject)
                    {
                        clients.Add(clientInfo);
                    }

                    Console.WriteLine($"Client connected: {clientName}");

                    SendUserListToAll();

                    while (true)
                    {
                        bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            string message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                            Console.WriteLine($"[{clientName}]: {message}");

                            HandleReceivedMessage(message, clientInfo);
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Client disconnected: {clientName}");
                    lock (lockObject)
                    {
                        clients.RemoveAll(c => c.Client == client);
                    }
                    client.Close();
                    SendUserListToAll();
                }
            }

            static void HandleReceivedMessage(string message, ClientInfo sender)
            {
                if (message.StartsWith("MSG:"))
                {
                    string msgText = message.Substring(4);
                    string formattedMessage = $"[{sender.Name}]: {msgText}";
                    BroadcastMessage(formattedMessage, sender.Name); 
                }
                else if (message.StartsWith("PVT:"))
                {
                    string[] parts = message.Split(new char[] { ':' }, 3);
                    if (parts.Length == 3)
                    {
                        string recipientName = parts[1];
                        string msgText = parts[2];

                        string formattedMessage = $"[Private from {sender.Name}]: {msgText}";
                        SendPrivateMessage(formattedMessage, sender.Name, recipientName);
                    }
                }
            }

            static void BroadcastMessage(string message, string exceptClientName)
            {
                byte[] buffer = Encoding.UTF8.GetBytes("MSG:" + message);
                lock (lockObject)
                {
                    foreach (var clientInfo in clients)
                    {
                        if (clientInfo.Name != exceptClientName)
                        {
                            try
                            {
                                NetworkStream stream = clientInfo.Client.GetStream();
                                stream.Write(buffer, 0, buffer.Length);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("Error sending message to a client: " + e.Message);
                            }
                        }
                    }
                }
            }

            static void SendPrivateMessage(string message, string senderName, string recipientName)
            {
                byte[] buffer = Encoding.UTF8.GetBytes("PVT:" + message);
                ClientInfo recipientClient = null;
                lock (lockObject)
                {
                    recipientClient = clients.Find(c => c.Name == recipientName);
                }

                if (recipientClient != null)
                {
                    try
                    {
                        NetworkStream stream = recipientClient.Client.GetStream();
                        stream.Write(buffer, 0, buffer.Length);

                        ClientInfo senderClient = clients.Find(c => c.Name == senderName);
                        if (senderClient != null && senderClient != recipientClient)
                        {
                            stream = senderClient.Client.GetStream();
                            stream.Write(buffer, 0, buffer.Length);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Error sending private message: " + e.Message);
                    }
                }
                else
                {
                    ClientInfo senderClient = clients.Find(c => c.Name == senderName);
                    if (senderClient != null)
                    {
                        try
                        {
                            string errorMessage = $"User '{recipientName}' not found.";
                            byte[] errorBuffer = Encoding.UTF8.GetBytes("SYS:" + errorMessage);
                            NetworkStream stream = senderClient.Client.GetStream();
                            stream.Write(errorBuffer, 0, errorBuffer.Length);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Error sending error message to sender: " + e.Message);
                        }
                    }
                }
            }

            static void SendUserListToAll()
            {
                lock (lockObject)
                {
                    string userList = "USERS:" + string.Join(",", clients.ConvertAll(c => c.Name));
                    byte[] buffer = Encoding.UTF8.GetBytes(userList);
                    foreach (var clientInfo in clients)
                    {
                        try
                        {
                            NetworkStream stream = clientInfo.Client.GetStream();
                            stream.Write(buffer, 0, buffer.Length);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Error sending user list to a client: " + e.Message);
                        }
                    }
                }
            }
        }
    }
}
