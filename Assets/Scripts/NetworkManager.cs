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
// ������ ����: �� ��ġ, ���ο� Ÿ�� ��ġ
// ������ ����: ���� �̵� ����, ���� ��Ȱ ��ġ
//
//������ �ʿ��� ���:
//Onreceived, ParseMessage�� string split���� ���� ��Ŷ�� �������� �Ľ��ϵ��� �ϴ� ���
//SendGameInfo�� ���� ���� �÷��̾�� �۽��ϵ��� ����

public class NetworkManager : MonoBehaviour
{
    private GameManager gameManager;

    public const int BUFFER_SIZE = 10000;
    TcpListener server = null;
    TcpClient clientSocket = null;
    static int counter = 0;
    private Dictionary<string, string> userList = new Dictionary<string, string>(); //������ �ش��ϴ� �н����带 �����ϴ� ��ųʸ�. ����� ���� ����
    private Dictionary<TcpClient, string> clientList = new Dictionary<TcpClient, string>(); //Tcp Client�� �ش��ϴ� �������� �����ϴ� ��ųʸ�
    private Dictionary<string, TcpClient> clientList_by_username = new Dictionary<string, TcpClient>(); // �������� �ش��ϴ� Tcp Client�� �����ϴ� ��ųʸ�(���� ��ųʸ��� Ű, ��� ��ġ�� �ٸ�)
    private int port_num = 9999;
    public bool isActive;

    Coroutine NetworkCoroutine;
    // Start is called before the first frame update
    void Start()
    {
        gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
        isActive = true;
        NetworkCoroutine = StartCoroutine("InitSocket");
        userList.Add("POSTECH", "1234");//���� ������ �������� �Է���
        userList.Add("KAIST", "5678");
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnDestroy()
    {
        isActive = false;
        Debug.Log("Server is closing!");
        ServerSendMessageAll("GameOver$");
        foreach (var pair in clientList)
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


    private IEnumerator InitSocket()
    {
        server = new TcpListener(IPAddress.Any, port_num);
        clientSocket = default(TcpClient);
        server.Start();
        DisplayText(">> Server Started");
        while (isActive)
        {
            yield return new WaitForFixedUpdate();
            if (clientList_by_username.ContainsKey("KAIST") && clientList_by_username.ContainsKey("POSTECH")) continue;
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
            if (clientList_by_username.ContainsKey("KAIST") && clientList_by_username.ContainsKey("POSTECH"))
            {
                gameManager.GameStart();
            }
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
        if (gameManager.isTurnActive())
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
            buffer = Encoding.Unicode.GetBytes(message + "/");

            stream.Write(buffer, 0, buffer.Length);
            stream.Flush();
        }
    }

    public void ServerSendMessage(string message, TcpClient client) // Ŭ���̾�Ʈ ��ü�� ����� �۽�
    {
        if (client == null) return;
        NetworkStream stream = client.GetStream();
        byte[] buffer = null;
        buffer = Encoding.Unicode.GetBytes(message + "/");

        stream.Write(buffer, 0, buffer.Length);
        stream.Flush();
    }

    public void ServerSendMessage(string message, string name) // Ŭ���̾�Ʈ �̸��� ����� �۽�
    {
        TcpClient client;
        if (!clientList_by_username.TryGetValue(name, out client)) return;
        NetworkStream stream = client.GetStream();
        byte[] buffer = null;
        buffer = Encoding.Unicode.GetBytes(message + "/");

        stream.Write(buffer, 0, buffer.Length);
        stream.Flush();
    }
    private void DisplayText(string text)
    {
        Debug.Log(text);
    }

    /// <summary>
    /// Ŭ���̾�Ʈ�� �۽��� �޼����� �޾Ƽ� �������� �Ľ��� ���ӸŴ����� ����
    /// </summary>
    /// <param name="user_name">������</param>
    /// <param name="message">�޼��� ����</param>
    private void ParseMessage(string user_name, string message) 
    {
        string[] split_cmd = message.Split('/');
        foreach(string cmd in split_cmd)
        {
            String[] split_msg = cmd.Split('$');
            String Output = string.Empty;
            int pieceNum;
            Vector2 CoordVector;
            String[] CoordString = split_msg[1].Split(',');
            switch (split_msg[0])
            {
                case "Move":
                    Output = "�̵� ����: ";
                    pieceNum = int.Parse(split_msg[1]);
                    string dir = split_msg[2];
                    switch (dir)
                    {
                        case "U":
                            gameManager.CommandQueue.Enqueue(GameManager.Command.moveCommand(user_name == "POSTECH", pieceNum, GameManager.Command.Direction.UP));
                            break;
                        case "D":
                            gameManager.CommandQueue.Enqueue(GameManager.Command.moveCommand(user_name == "POSTECH", pieceNum, GameManager.Command.Direction.DOWN));
                            break;
                        case "L":
                            gameManager.CommandQueue.Enqueue(GameManager.Command.moveCommand(user_name == "POSTECH", pieceNum, GameManager.Command.Direction.LEFT));
                            break;
                        case "R":
                            gameManager.CommandQueue.Enqueue(GameManager.Command.moveCommand(user_name == "POSTECH", pieceNum, GameManager.Command.Direction.RIGHT));
                            break;
                        default:
                            break;
                    }
                    Debug.Log(Output + user_name + " , " + split_msg[1] + "�� ��, " + split_msg[2]);
                    break;
                case "Respawn":
                    Output = "��Ȱ ����: ";
                    pieceNum = int.Parse(split_msg[1]);
                    CoordString = split_msg[2].Split(',');
                    try
                    {
                        CoordVector = new Vector2(float.Parse(CoordString[0]), float.Parse(CoordString[1]));
                    }
                    catch (Exception ex)
                    {
                        Debug.Log(String.Format("��ǥ �Ľ� ���� : {0}", ex.Message));
                        break;
                    }
                    Debug.Log(Output + user_name + " , " + split_msg[1] + "�� ��, " + CoordVector);
                    gameManager.CommandQueue.Enqueue(GameManager.Command.respawnCommand(user_name == "POSTECH", pieceNum, CoordVector));
                    break;
                case "Wait":
                    Debug.Log(Output + user_name + " , " + split_msg[1] + "�� ��, " + "��� ����");
                    break;
                default:
                    Debug.Log("�� �� ���� ����: " + user_name + " , " + message);
                    break;
            }
        }
    }
    /// <summary>
    /// �� �÷��̾�� ������ ���� �۽�
    /// </summary>
    /// <param name="poUnit">������ ���� ����Ʈ</param>
    /// <param name="kaUnit">ī�̽�Ʈ ���� ����Ʈ</param>
    /// <param name="poNewArea">�̹� �Ͽ� ���� ������ ������ ����</param>
    /// <param name="kaNewArea">�̹� �Ͽ� ���� ������ ī�̽�Ʈ ����</param>
    public void SendGameInfo(List<GameObject>poUnit, List<GameObject>kaUnit, List<GameObject> poNewArea, List<GameObject>kaNewArea)
    {
        string po_new_area_msg = string.Empty;
        string ka_new_area_msg = string.Empty;
        foreach(GameObject obj in poNewArea)
        {
            po_new_area_msg += obj.transform.position.x + "," + obj.transform.position.y + "$";
        }
        foreach (GameObject obj in kaNewArea)
        {
            ka_new_area_msg += obj.transform.position.x + "," + obj.transform.position.y + "$";
        }
        foreach (var pair in clientList)
        {
            TcpClient client = pair.Key as TcpClient;
            string username = pair.Value as string;
            string friendly_unit_msg = string.Empty;
            string enemy_unit_msg = string.Empty;
            if (username == "POSTECH")
            {
                for(int i=0; i < 3; i++)
                {
                    friendly_unit_msg += poUnit[i].transform.position.x.ToString() + "," + poUnit[i].transform.position.y.ToString() + "$";
                }
                for (int i = 0; i < 2; i++)
                {
                    enemy_unit_msg += kaUnit[i].transform.position.x.ToString() + "," + kaUnit[i].transform.position.y.ToString() + "$";
                }
                ServerSendMessage("Friendly_Unit$" + friendly_unit_msg, client);
                ServerSendMessage("Enemy_Unit$" + enemy_unit_msg, client);
                ServerSendMessage("Friendly_New_Area$" + po_new_area_msg, client);
                ServerSendMessage("Enemy_New_Area$" + ka_new_area_msg, client);
            }
            else if (username == "KAIST")
            {
                for (int i = 0; i < 3; i++)
                {
                    friendly_unit_msg += kaUnit[i].transform.position.x.ToString() + "," + kaUnit[i].transform.position.y.ToString() + "$";
                }
                for (int i = 0; i < 2; i++)
                {
                    enemy_unit_msg += poUnit[i].transform.position.x.ToString() + "," + poUnit[i].transform.position.y.ToString() + "$";
                }
                ServerSendMessage("Friendly_Unit$" + friendly_unit_msg, client);
                ServerSendMessage("Enemy_Unit$" + enemy_unit_msg, client);
                ServerSendMessage("Friendly_New_Area$" + ka_new_area_msg, client);
                ServerSendMessage("Enemy_New_Area$" + po_new_area_msg, client);
            }
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

    public void ServerSendTurnStart()
    {
        ServerSendMessageAll("TurnStart");
    }

    public void ServerSendTurnover()
    {
        ServerSendMessageAll("TurnTimeOut");
    }

    public void ServerSendGameStart()
    {
        ServerSendMessageAll("GameStart");
    }

    public void ServerSendGameOver()
    {
        ServerSendMessageAll("GameOver");
    }
}