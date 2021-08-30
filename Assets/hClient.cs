using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class handleClient
{
    TcpClient clientSocket = null;
    public Dictionary<TcpClient, string> clientList = null;
    Thread t_handler;

    public void startClient(TcpClient clientSocket, Dictionary<TcpClient, string> clientList)
    {
        this.clientSocket = clientSocket;
        this.clientList = clientList;

        t_handler = new Thread(receive);
        t_handler.IsBackground = true;
        t_handler.Start();
    }

    public void stopClient()
    {
        t_handler.Join();
    }

    public delegate void MessageDisplayHandler(string message, string user_name);
    public event MessageDisplayHandler OnReceived;

    public delegate void DisconnectedHandler(TcpClient clientSocket);
    public event DisconnectedHandler OnDisconnected;

    private void receive()
    {
        NetworkStream stream = null;
        try
        {
            byte[] buffer = new byte[10000];
            string msg = string.Empty;
            int bytes = 0;
            int MessageCount = 0;

            while (clientSocket.Connected)
            {
                Thread.Yield();
                stream = clientSocket.GetStream();
                MessageCount++;
                bytes = stream.Read(buffer, 0, buffer.Length);
                if (bytes == 0) break;//0바이트 수신시 상대측 소켓이 끊어진 것이므로 종료함
                msg = Encoding.Unicode.GetString(buffer, 0, bytes);

                if (OnReceived != null)
                    OnReceived(msg, clientList[clientSocket].ToString());
            }
        }
        catch (SocketException se)
        {
            Console.WriteLine(string.Format("SocketException : {0}", se.Message));
        }
        catch (Exception ex)
        {
            Console.WriteLine(string.Format("Exception : {0}", ex.Message));
        }
        if (clientSocket != null)
        {
            if (OnDisconnected != null)
                OnDisconnected(clientSocket);
            clientSocket.Close();
            stream.Close();
        }
    }

}