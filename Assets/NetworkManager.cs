using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;


// 임시 기능: Encode_Send_Coord : "Coord$X0,Y0$X1,Y1$X2,Y2$X3,Y3$" 형식으로 4개의 말 위치 전송
// 서버 종료시(isActive = false) 모든 클라이언트에게 "GameOver$" 전송
// TURN_TIME만큼 대기 후 "TurnTimeout$" 전송, 각 플레이어에게 게임 정보 송신 후 "TurnStart$" 전송
// 임시 기능 : Coord$X좌표,Y좌표 형식의 메세지를 받을 경우 파싱하여 디버그 로그 출력함
//
//수정이 필요한 기능:
//Onreceived, ParseMessage에 string split으로 받은 패킷을 명령으로 파싱하도록 하는 기능
//SendGameInfo에 게임 정보 플레이어에게 송신하도록 구현


public class NetworkManager : MonoBehaviour
{
    public const int BUFFER_SIZE = 10000;
    public const float TURN_TIME = 5.0f;
    TcpListener server = null;
    TcpClient clientSocket = null;
    static int counter = 0;
    private Dictionary<string, string> userList = new Dictionary<string, string>(); //유저와 해당하는 패스워드를 저장하는 딕셔너리. 연결시 보안 목적
    private Dictionary<TcpClient, string> clientList = new Dictionary<TcpClient, string>(); //Tcp Client에 해당하는 유저명을 저장하는 딕셔너리
    private Dictionary<string, TcpClient> clientList_by_username = new Dictionary<string, TcpClient>(); // 유저명에 해당하는 Tcp Client를 저장하는 딕셔너리(위의 딕셔너리와 키, 밸류 위치만 다름)
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
        userList.Add("PHOENIX", "1234");//유저 정보를 수동으로 입력함
        userList.Add("넙죽이", "5678");
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
                DisplayText(user_name + "가 접속했습니다. 패스워드 : " + password + ", 현재 접속자 수 : " + counter);
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

    private void OnReceived(string message, string user_name) // 현재 턴이 활성화된 상태일 시 받은 정보를 파싱해서 넘김 
    {
        string displayMessage = "From client : " + user_name + " : " + message;
        if (isTurnActive())
        {
            ParseMessage(user_name, message);
            DisplayText(displayMessage);
        }
    }

    public void ServerSendMessageAll(string message) //모든 클라이언트에 동일한 내용 송신
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

    public void ServerSendMessage(string message, TcpClient client) // 클라이언트 객체에 기반해 송신
    {
        if (client == null) return;
        NetworkStream stream = client.GetStream();
        byte[] buffer = null;
        buffer = Encoding.Unicode.GetBytes(message);

        stream.Write(buffer, 0, buffer.Length);
        stream.Flush();
    }

    public void ServerSendMessage(string message, string name) // 클라이언트 이름에 기반해 송신
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


    private void ParseMessage(string user_name, string message) // 클라이언트가 송신한 패킷을 받아서 명령어로 파싱함
    {
        String[] split_msg = message.Split('$');
        switch (split_msg[0])
        {
            case "Coord":
                String Output = "좌표 파싱됨: ";
                String[] CoordString = split_msg[1].Split(',');
                Vector2 CoordVector;
                try
                {
                    CoordVector = new Vector2(float.Parse(CoordString[0]), float.Parse(CoordString[1]));
                }
                catch (Exception ex)
                {
                    Debug.Log(String.Format("좌표 파싱 예외 : {0}", ex.Message));
                    break;
                }
                Debug.Log(Output + CoordVector);
                break;
            default:
                break;
        }
    }

    private void SendGameInfo() // 각 플레이어에게 게임 상태 송신함
    {
        foreach (var pair in clientList)
        {
            TcpClient client = pair.Key as TcpClient;
            string username = pair.Value as string;
            ServerSendMessage("", client);
        }
    }

    private bool Encode_Send_Coord(Vector2[] dots, string username) // Map에 현재 상황 데이터, username에 어떤 플레이어에게 전달할 정보인지 구분해 현재 상황 데이터를 전송(Vector2는 placeholder)
    {
        string encoded = string.Empty;
        encoded += "Coord$";
        for(int i = 0; i < 4; i++)
        {
            encoded += ((int)dots[i].x).ToString() + "," + ((int)dots[i].y).ToString() + "$"; // X,Y$ 를 뒤에 붙임
        }
        ServerSendMessage(encoded, username);
        return true;
    }
    private IEnumerator turnTimer() // TURN_TIME 초간 대기하고 플레이어에게 "Timeout$" 전송 후 게임 데이터 전송 (필요시 WaitUntil(isTurnActive)를 사용해서 제어할 것) 
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
