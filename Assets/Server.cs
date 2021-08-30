using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Game_server
{
    public class Server
    {
        TcpListener server = null;
        TcpClient clientSocket = null;
        static int counter = 0;
        public Dictionary<TcpClient, string> clientList = new Dictionary<TcpClient, string>();
        bool isActive = true;
        Thread thread;

        public Server()
        {
            thread = new Thread(InitSocket);
            thread.Start();
        }

        ~Server()
        {
            isActive = false;
            thread.Join();
        }


        private void InitSocket()
        {
            server = new TcpListener(IPAddress.Any, 9999);
            clientSocket = default(TcpClient);
            server.Start();
            DisplayText(">> Server Started");
            while (isActive)
            {
                try
                {
                    counter++;
                    clientSocket = server.AcceptTcpClient();
                    DisplayText(">> Accept connection from client");
                    NetworkStream stream = clientSocket.GetStream();
                    byte[] buffer = new byte[1024];
                    int bytes = stream.Read(buffer, 0, buffer.Length);
                    string user_name = Encoding.Unicode.GetString(buffer, 0, bytes);
                    user_name = user_name.Substring(0, user_name.IndexOf("$"));
                    clientList.Add(clientSocket, user_name);
                    SendMessageAll(user_name + "가 접속했습니다.");
                    handleClient h_client = new handleClient();
                    h_client.OnReceived += new handleClient.MessageDisplayHandler(OnReceived);
                    h_client.OnDisconnected += new handleClient.DisconnectedHandler(h_client_OnDisconnection);
                    h_client.startClient(clientSocket, clientList);
                }
                catch (SocketException se) { break; }
                catch (Exception ex) { break; }
            }
            clientSocket.Close();
            server.Stop();
        }
        void h_client_OnDisconnection(TcpClient clientSocket)
        {
            if (clientList.ContainsKey(clientSocket))
                clientList.Remove(clientSocket);
        }
        private void OnReceived(string message, string user_name)
        {
            if (message.Equals("leave"))
            {
                string displayMessage = "leave user : " + user_name;
                DisplayText(displayMessage);
            }
            else
            {
                string displayMessage = "From client : " + user_name + " : " + message;
                DisplayText(displayMessage);
            }
        }
        public void SendMessageAll(string message)
        {
            foreach (var pair in clientList)
            {
                TcpClient client = pair.Key as TcpClient;
                NetworkStream stream = client.GetStream();
                byte[] buffer = null;
                buffer = Encoding.Unicode.GetBytes(message);

                stream.Write(buffer, 0, buffer.Length);
                stream.Flush();
            }
        }
        private void DisplayText(string text)
        {
            Console.WriteLine(text + Environment.NewLine);
        }
    }
}
