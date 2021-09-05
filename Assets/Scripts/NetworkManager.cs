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
//
// 전송할 정보: 말 위치, 새로운 타일 위치
// 수신할 정보: 말의 이동 방향, 말의 부활 위치
//
//수정이 필요한 기능:
//Onreceived, ParseMessage에 string split으로 받은 패킷을 명령으로 파싱하도록 하는 기능
//SendGameInfo에 게임 정보 플레이어에게 송신하도록 구현

public class NetworkManager : MonoBehaviour
{
    private GameManager gameManager;

    public const int BUFFER_SIZE = 10000;
    TcpListener server = null;
    TcpClient clientSocket = null;
    static int counter = 0;
    private Dictionary<string, string> userList = new Dictionary<string, string>(); //유저와 해당하는 패스워드를 저장하는 딕셔너리. 연결시 보안 목적
    private Dictionary<TcpClient, string> clientList = new Dictionary<TcpClient, string>(); //Tcp Client에 해당하는 유저명을 저장하는 딕셔너리
    private Dictionary<string, TcpClient> clientList_by_username = new Dictionary<string, TcpClient>(); // 유저명에 해당하는 Tcp Client를 저장하는 딕셔너리(위의 딕셔너리와 키, 밸류 위치만 다름)
    private int port_num = 9999;
    public bool isActive;

    Coroutine NetworkCoroutine;
    // Start is called before the first frame update
    void Start()
    {
        gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
        isActive = true;
        NetworkCoroutine = StartCoroutine("InitSocket");
        userList.Add("POSTECH", "1234");//유저 정보를 수동으로 입력함
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
                user_info = user_info.Trim();
                user_info = user_info.Trim(new char[] { '\uFEFF', '\u200B', '\u0000' });
                string[] user_info_split = user_info.Split('/', '$');
                string user_name = user_info_split[0];
                string password = user_info_split[1];
                string server_password;
                if(!userList.TryGetValue(user_name, out server_password))
                {
                    clientSocket.Close();
                    counter--;
                    Debug.Log("Non-appropriate user information " + user_name);
                    continue;
                }
                else if(server_password != password)
                {
                    clientSocket.Close();
                    counter--;
                    Debug.Log("Non-appropriate user information " + user_name + ", "+ password);
                    continue;
                }
                ServerSendMessage("Logged", clientSocket);
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

    private void OnReceived(string message, string user_name) // 현재 턴이 활성화된 상태일 시 받은 정보를 파싱해서 넘김 
    {
        string trim_message = message.Trim();
        trim_message = trim_message.Trim(new char[] { '\uFEFF', '\u200B', '\u0000' });
        string displayMessage = "From client : " + user_name + " : " + trim_message;
        Debug.Log(displayMessage);
        if (gameManager.isTurnActive())
        {
            ParseMessage(user_name, trim_message);
        }
    }

    public void ServerSendMessageAll(string message) //모든 클라이언트에 동일한 내용 송신
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

    public void ServerSendMessage(string message, TcpClient client) // 클라이언트 객체에 기반해 송신
    {
        if (client == null) return;
        NetworkStream stream = client.GetStream();
        byte[] buffer = null;
        buffer = Encoding.Unicode.GetBytes(message + "/");

        stream.Write(buffer, 0, buffer.Length);
        stream.Flush();
    }

    public void ServerSendMessage(string message, string name) // 클라이언트 이름에 기반해 송신
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
    /// 클라이언트가 송신한 메세지를 받아서 명령으로 파싱해 게임매니저에 전달
    /// </summary>
    /// <param name="user_name">유저명</param>
    /// <param name="message">메세지 내용</param>
    private void ParseMessage(string user_name, string message)
    {
        string[] split_cmd = message.Split('/');
        foreach(string cmd in split_cmd)
        {
            String trim_cmd = cmd.Trim(new char[] { '\uFEFF', '\u200B', '\u0000' });
            String[] split_msg = trim_cmd.Split('$');
            String Output = string.Empty;
            int pieceNum;
            Vector2 CoordVector;
            String[] CoordString = split_msg[1].Split(',');
            switch (split_msg[0])
            {
                case "Init":
                    Debug.Log("Initializing : " + message);
                    List<(int x, int y)> temp = new List<(int x, int y)>();
                    for (int i = 0; i < 3; i++)
                    {
                        CoordString = split_msg[1 + i].Split(',');
                        try
                        {
                            temp.Add((int.Parse(CoordString[0]), int.Parse(CoordString[1])));
                        }
                        catch (Exception ex)
                        {
                            Debug.Log(String.Format("좌표 파싱 예외 : {0}", ex.Message));
                            break;
                        }
                    }
                    if (user_name == "POSTECH")
                    {
                        gameManager.po_init_pos = temp;
                    }
                    else
                    {
                        gameManager.ka_init_pos = temp;
                    }
                    break;
                case "Move":
                    Output = "이동 명령: ";
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
                    //Debug.Log(Output + user_name + " , " + split_msg[1] + "번 말, " + split_msg[2]);
                    break;
                case "Respawn":
                    Output = "부활 명령: ";
                    pieceNum = int.Parse(split_msg[1]);
                    CoordString = split_msg[2].Split(',');
                    try
                    {
                        CoordVector = new Vector2(float.Parse(CoordString[0]), float.Parse(CoordString[1]));
                    }
                    catch (Exception ex)
                    {
                        Debug.Log(String.Format("좌표 파싱 예외 : {0}", ex.Message));
                        break;
                    }
                    //Debug.Log(Output + user_name + " , " + split_msg[1] + "번 말, " + CoordVector);
                    gameManager.CommandQueue.Enqueue(GameManager.Command.respawnCommand(user_name == "POSTECH", pieceNum, CoordVector));
                    break;
                case "Wait":
                    Debug.Log(Output + user_name + " , " + split_msg[1] + "번 말, " + "대기 명령");
                    break;
                default:
                    Debug.Log("알 수 없는 명령: " + user_name + " , " + message);
                    break;
            }
        }
    }
    /// <summary>
    /// 각 플레이어에게 게임의 상태 송신
    /// </summary>
    /// <param name="poUnit">포스텍 유닛 리스트</param>
    /// <param name="kaUnit">카이스트 유닛 리스트</param>
    /// <param name="poArea">포스텍 영역</param>
    /// <param name="kaArea">카이스트 영역</param>
    public void SendGameInfo(List<GameObject>poUnit, List<GameObject>kaUnit, List<GameObject> poArea, List<GameObject>kaArea)
    {
        string po_new_area_msg = string.Empty;
        string ka_new_area_msg = string.Empty;
        foreach(GameObject obj in poArea)
        {
            po_new_area_msg += obj.transform.position.x + "," + obj.transform.position.y + "$";
        }
        foreach (GameObject obj in kaArea)
        {
            ka_new_area_msg += obj.transform.position.x + "," + obj.transform.position.y + "$";
        }
        foreach (var pair in clientList)
        {
            TcpClient client = pair.Key as TcpClient;
            string username = pair.Value as string;
            string friendly_unit_msg = string.Empty;
            string enemy_unit_msg = string.Empty;
            string friendly_line_msg = string.Empty;
            string enemy_line_msg = string.Empty;
            if (username == "POSTECH")
            {
                for(int i=0; i < 3; i++)
                {
                    friendly_unit_msg += (poUnit[i].activeSelf ? "L" : "D") + "," + poUnit[i].transform.position.x.ToString() + "," + poUnit[i].transform.position.y.ToString() + "$";
                    foreach(GameObject line in poUnit[i].GetComponent<Unit>().line)
                    {
                        friendly_line_msg += line.transform.position.x.ToString() + "," + line.transform.position.y.ToString() + "$";
                    }
                }
                for (int i = 0; i < 2; i++)
                {
                    enemy_unit_msg += (kaUnit[i].activeSelf ? "L" : "D") + "," + kaUnit[i].transform.position.x.ToString() + "," + kaUnit[i].transform.position.y.ToString() + "$";
                    foreach (GameObject line in kaUnit[i].GetComponent<Unit>().line)
                    {
                        enemy_line_msg += line.transform.position.x.ToString() + "," + line.transform.position.y.ToString() + "$";
                    }
                }
                enemy_unit_msg += (kaUnit[2].activeSelf ? "L" : "D") + "$";
                ServerSendMessage("Friendly_Unit$" + friendly_unit_msg, client);
                ServerSendMessage("Enemy_Unit$" + enemy_unit_msg, client);
                ServerSendMessage("Friendly_Line$" + friendly_line_msg, client);
                ServerSendMessage("Enemy_Line$" + enemy_line_msg, client);
                ServerSendMessage("Friendly_Area$" + po_new_area_msg, client);
                ServerSendMessage("Enemy_Area$" + ka_new_area_msg, client);
            }
            else if (username == "KAIST")
            {
                for (int i = 0; i < 3; i++)
                {
                    friendly_unit_msg += (kaUnit[i].activeSelf ? "L" : "D") + "," + kaUnit[i].transform.position.x.ToString() + "," + kaUnit[i].transform.position.y.ToString() + "$";
                    foreach (GameObject line in kaUnit[i].GetComponent<Unit>().line)
                    {
                        friendly_line_msg += line.transform.position.x.ToString() + "," + line.transform.position.y.ToString() + "$";
                    }
                }
                for (int i = 0; i < 2; i++)
                {
                    enemy_unit_msg += (poUnit[i].activeSelf ? "L" : "D") + "," + poUnit[i].transform.position.x.ToString() + "," + poUnit[i].transform.position.y.ToString() + "$";
                    foreach (GameObject line in poUnit[i].GetComponent<Unit>().line)
                    {
                        enemy_line_msg += line.transform.position.x.ToString() + "," + line.transform.position.y.ToString() + "$";
                    }
                }
                enemy_unit_msg += (poUnit[2].activeSelf ? "L" : "D") + "$";
                ServerSendMessage("Friendly_Unit$" + friendly_unit_msg, client);
                ServerSendMessage("Enemy_Unit$" + enemy_unit_msg, client);
                ServerSendMessage("Friendly_Line$" + friendly_line_msg, client);
                ServerSendMessage("Enemy_Line$" + enemy_line_msg, client);
                ServerSendMessage("Friendly_Area$" + ka_new_area_msg, client);
                ServerSendMessage("Enemy_Area$" + po_new_area_msg, client);
            }
        }
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
