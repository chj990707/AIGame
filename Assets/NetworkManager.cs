using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;


// �ӽ� ���: Encode_Send_Coord : "Coord$X0,Y0$X1,Y1$X2,Y2$X3,Y3$" �������� 4���� �� ��ġ ����
// ���� �����(isActive = false) ��� Ŭ���̾�Ʈ���� "GameOver$" ����
// TURN_TIME��ŭ ��� �� "TurnTimeout$" ����, �� �÷��̾�� ���� ���� �۽� �� "TurnStart$" ����
// �ӽ� ��� : Coord$X��ǥ,Y��ǥ ������ �޼����� ���� ��� �Ľ��Ͽ� ����� �α� �����
//
//������ �ʿ��� ���:
//Onreceived, ParseMessage�� string split���� ���� ��Ŷ�� ������� �Ľ��ϵ��� �ϴ� ���
//SendGameInfo�� ���� ���� �÷��̾�� �۽��ϵ��� ����


public class NetworkManager : MonoBehaviour
{
    public const int BUFFER_SIZE = 10000;
    public const float TURN_TIME = 5.0f;
    TcpListener server = null;
    TcpClient clientSocket = null;
    static int counter = 0;
    private Dictionary<string, string> userList = new Dictionary<string, string>(); //������ �ش��ϴ� �н����带 �����ϴ� ��ųʸ�. ����� ���� ����
    private Dictionary<TcpClient, string> clientList = new Dictionary<TcpClient, string>(); //Tcp Client�� �ش��ϴ� �������� �����ϴ� ��ųʸ�
    private Dictionary<string, TcpClient> clientList_by_username = new Dictionary<string, TcpClient>(); // ������ �ش��ϴ� Tcp Client�� �����ϴ� ��ųʸ�(���� ��ųʸ��� Ű, ��� ��ġ�� �ٸ�)
    private int port_num = 9999;
    bool isActive;
    private bool TurnActive;

    Coroutine NetworkCoroutine;
    Coroutine TurnTimer;
    // Start is called before the first frame update
    void Start()
    {
        isActive = true;
        TurnTimer = StartCoroutine("turnTimer");
        NetworkCoroutine = StartCoroutine("InitSocket");
        userList.Add("PHOENIX", "1234");//���� ������ �������� �Է���
        userList.Add("������", "5678");
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnDestroy()
    {
        isActive = false;
    }


    private IEnumerator InitSocket()
    {
        server = new TcpListener(IPAddress.Any, port_num);
        clientSocket = default(TcpClient);
        server.Start();
        DisplayText(">> Server Started");
        while (isActive)
        {
            yield return new WaitForFixedUpdate();
            try
            {
                if (!server.Pending())
                {
                    continue; 
                }
                counter++;
                clientSocket = server.AcceptTcpClient();
                DisplayText(">> Accept connection from client");
                NetworkStream stream = clientSocket.GetStream();
                byte[] buffer = new byte[BUFFER_SIZE];
                int bytes = stream.Read(buffer, 0, buffer.Length);
                string user_info = Encoding.Unicode.GetString(buffer, 0, bytes);
                string user_name = user_info.Substring(0, user_info.IndexOf("$"));
                string password = user_info.Substring(user_info.IndexOf("$") + 1);
                string server_password;
                if(!userList.TryGetValue(user_name, out server_password) || server_password != password)
                {
                    clientSocket.Close();
                    counter--;
                    Debug.Log("Non-appropriate user information");
                    continue;
                }
                ServerSendMessage("Logged$", clientSocket);
                clientList.Add(clientSocket, user_name);
                clientList_by_username.Add(user_name, clientSocket);
                DisplayText(user_name + "�� �����߽��ϴ�. �н����� : " + password + ", ���� ������ �� : " + counter);
                handleClient h_client = new handleClient();
                h_client.OnReceived += new handleClient.MessageDisplayHandler(OnReceived);
                h_client.OnDisconnected += new handleClient.DisconnectedHandler(h_client_OnDisconnection);
                h_client.startClient(clientSocket, clientList);
            }
            catch (SocketException se) { break; }
            catch (Exception ex) { break; }
        }
        Debug.Log("Server is closing!");
        ServerSendMessageAll("GameOver$");
        foreach(var pair in clientList)
        {
            TcpClient client = pair.Key as TcpClient;
            client.Close();
        }
        if (clientSocket != null)
        {
            clientSocket.Close();
        }
        server.Stop();
    }

    void h_client_OnDisconnection(TcpClient clientSocket)
    {
        string name;
        if (clientList.TryGetValue(clientSocket, out name))
        {
            counter--;
            clientList.Remove(clientSocket);
            clientList_by_username.Remove(name);
            DisplayText(name + " has Disconnected, Users : " + clientList.Count);
        }
    }

    private void OnReceived(string message, string user_name) // ���� ���� Ȱ��ȭ�� ������ �� ���� ������ �Ľ��ؼ� �ѱ� 
    {
        string displayMessage = "From client : " + user_name + " : " + message;
        if (isTurnActive())
        {
            ParseMessage(user_name, message);
            DisplayText(displayMessage);
        }
    }

    public void ServerSendMessageAll(string message) //��� Ŭ���̾�Ʈ�� ������ ���� �۽�
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

    public void ServerSendMessage(string message, TcpClient client) // Ŭ���̾�Ʈ ��ü�� ����� �۽�
    {
        if (client == null) return;
        NetworkStream stream = client.GetStream();
        byte[] buffer = null;
        buffer = Encoding.Unicode.GetBytes(message);

        stream.Write(buffer, 0, buffer.Length);
        stream.Flush();
    }

    public void ServerSendMessage(string message, string name) // Ŭ���̾�Ʈ �̸��� ����� �۽�
    {
        TcpClient client;
        if (!clientList_by_username.TryGetValue(name, out client)) return;
        NetworkStream stream = client.GetStream();
        byte[] buffer = null;
        buffer = Encoding.Unicode.GetBytes(message);

        stream.Write(buffer, 0, buffer.Length);
        stream.Flush();
    }
    private void DisplayText(string text)
    {
        Debug.Log(text);
    }


    private void ParseMessage(string user_name, string message) // Ŭ���̾�Ʈ�� �۽��� ��Ŷ�� �޾Ƽ� ��ɾ�� �Ľ���
    {
        String[] split_msg = message.Split('$');
        switch (split_msg[0])
        {
            case "Coord":
                String Output = "��ǥ �Ľ̵�: ";
                String[] CoordString = split_msg[1].Split(',');
                Vector2 CoordVector;
                try
                {
                    CoordVector = new Vector2(float.Parse(CoordString[0]), float.Parse(CoordString[1]));
                }
                catch (Exception ex)
                {
                    Debug.Log(String.Format("��ǥ �Ľ� ���� : {0}", ex.Message));
                    break;
                }
                Debug.Log(Output + CoordVector);
                break;
            default:
                break;
        }
    }

    private void SendGameInfo() // �� �÷��̾�� ���� ���� �۽���
    {
        foreach (var pair in clientList)
        {
            TcpClient client = pair.Key as TcpClient;
            string username = pair.Value as string;
            ServerSendMessage("", client);
        }
    }

    private bool Encode_Send_Coord(Vector2[] dots, string username) // Map�� ���� ��Ȳ ������, username�� � �÷��̾�� ������ �������� ������ ���� ��Ȳ �����͸� ����(Vector2�� placeholder)
    {
        string encoded = string.Empty;
        encoded += "Coord$";
        for(int i = 0; i < 4; i++)
        {
            encoded += ((int)dots[i].x).ToString() + "," + ((int)dots[i].y).ToString() + "$"; // X,Y$ �� �ڿ� ����
        }
        ServerSendMessage(encoded, username);
        return true;
    }
    private IEnumerator turnTimer() // TURN_TIME �ʰ� ����ϰ� �÷��̾�� "Timeout$" ���� �� ���� ������ ���� (�ʿ�� WaitUntil(isTurnActive)�� ����ؼ� ������ ��) 
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(TURN_TIME);
            ServerSendMessageAll("TurnTimeOut$");
            TurnActive = false;
            SendGameInfo();
            //yield return new WaitUntil(isTurnActive);
            TurnActive = true;
            ServerSendMessageAll("TurnStart$");
        }
    }

    public bool isTurnActive()
    {
        return TurnActive;
    }

    public void TestFunc_SendCoord()
    {
        Vector2[] CoordList = {new Vector2(0, 0), new Vector2(2, 3), new Vector2(4, 5), new Vector2(6, 7)};
        Encode_Send_Coord(CoordList, "PHOENIX");
    }

    public void TestFunc_KillServer()
    {
        isActive = false;
    }
}
