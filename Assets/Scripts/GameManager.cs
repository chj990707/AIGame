using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;


// 유닛 리스트의 유닛 순서는 공개0, 공개1, 공개2, 비공개0 순서로 함.

public class GameManager : MonoBehaviour
{
    public int width = 180;
    public int height = 120;

    public const float TURN_TIME = 5.0f;
    private bool TurnActive;
    Coroutine TurnTimer;

    public class Command
    {
        public enum CommandType { Move, Respawn, Wait };
        public enum Direction { NONE = -1, UP, DOWN, LEFT, RIGHT};

        public bool isPo { get; private set; } //팀
        public int pieceNum { get; private set; }
        public CommandType type { get; private set; }
        public Vector2 pos { get; private set; }
        public Direction dir { get; private set; }

        public static Command waitCommand(bool isPostech, int _pieceNum)
        {
            return new Command(isPostech, _pieceNum, CommandType.Wait, new Vector2(-1, -1), Direction.NONE);
        }

        public static Command respawnCommand(bool isPostech, int _pieceNum, Vector2 _pos)
        {
            return new Command(isPostech, _pieceNum, CommandType.Respawn, _pos, Direction.NONE);
        }

        public static Command moveCommand(bool isPostech, int _pieceNum, Direction _dir)
        {
            return new Command(isPostech, _pieceNum, CommandType.Move, new Vector2(-1, -1), _dir);
        }

        private Command(bool isPostech, int _pieceNum, CommandType _type, Vector2 _pos, Direction _dir)
        {
            isPo = isPostech;
            pieceNum = _pieceNum;
            type = _type;
            pos = _pos;
            dir = _dir;
        }

    }

    private NetworkManager networkManager;
    public ConcurrentQueue<Command> CommandQueue = new ConcurrentQueue<Command>();

    public GameObject ka;
    public GameObject po;
    public GameObject ka_tp;
    public GameObject po_tp;
    public GameObject ka_ar;
    public GameObject po_ar;
    public GameObject ka_ln;
    public GameObject po_ln;

    private List<GameObject> kUnits = new List<GameObject>();
    private List<GameObject> pUnits = new List<GameObject>();
    private List<GameObject> kArea = new List<GameObject>();
    private List<GameObject> pArea = new List<GameObject>();

    public HashSet<GameObject> killList = new HashSet<GameObject>();

    void Awake()
    {

    }

    // Start is called before the first frame update
    void Start()
    {
        networkManager = GameObject.Find("NetworkManager").GetComponent<NetworkManager>();
        pUnits.Add(Instantiate(po, new Vector3(0, 0, 0), Quaternion.identity));
        kUnits.Add(Instantiate(ka, new Vector3(width - 1, height - 1, 0), Quaternion.identity));
        pUnits.Add(Instantiate(po, new Vector3(0, 1, 0), Quaternion.identity));
        kUnits.Add(Instantiate(ka, new Vector3(width - 1, height - 2, 0), Quaternion.identity));
        pUnits.Add(Instantiate(po, new Vector3(0, 2, 0), Quaternion.identity));
        kUnits.Add(Instantiate(ka, new Vector3(width - 1, height - 3, 0), Quaternion.identity));
        pUnits.Add(Instantiate(po_tp, new Vector3(0, 3, 0), Quaternion.identity));
        kUnits.Add(Instantiate(ka_tp, new Vector3(width - 1, height - 4, 0), Quaternion.identity));
        // Draw Initial Area
        for (int x = 0; x < 6; x++){
            for (int y = 0; y < 4; y ++){
                kArea.Add(Instantiate(ka_ar, new Vector3(width - 1 - x, height - 1 - y, 0), Quaternion.identity));
                pArea.Add(Instantiate(po_ar, new Vector3(x, y, 0), Quaternion.identity));
            }
        }
        Time.fixedDeltaTime = 0.005f;
    }

    public void GameStart()
    {
        TurnTimer = StartCoroutine("turnTimer");
        TurnActive = true;
        networkManager.ServerSendGameStart();
    }

    void TurnUpdate()
    {
        // Client에게 좌표 받고 Unit 이동
        {
            bool[] isKaCommanded = new bool[4] { false, false, false, false };
            bool[] isPoCommanded = new bool[4] { false, false, false, false };
            while(CommandQueue.Count > 0)
            {
                Command Cur_Com;
                CommandQueue.TryDequeue(out Cur_Com);
                Debug.Log("대기중인 명령 수 : " + CommandQueue.Count);
                GameObject piece;
                Debug.Log(Cur_Com);
                if (Cur_Com.isPo)
                {
                    if (isPoCommanded[Cur_Com.pieceNum])
                    {
                        Debug.Log("포스텍이 중복 명령 실행 시도");
                        continue;
                    }
                    else
                    {
                        isPoCommanded[Cur_Com.pieceNum] = true;
                    }
                    piece = pUnits[Cur_Com.pieceNum];
                }
                else
                {
                    if (isKaCommanded[Cur_Com.pieceNum])
                    {
                        Debug.Log("카이스트가 중복 명령 실행 시도");
                        continue;
                    }
                    else
                    {
                        isKaCommanded[Cur_Com.pieceNum] = true;
                    }
                    piece = kUnits[Cur_Com.pieceNum];
                }
                switch (Cur_Com.type)
                {
                    case Command.CommandType.Move:
                        if (!piece.activeInHierarchy)
                        {
                            Debug.Log(String.Format("{0} 사망한 말을 이동하려고 시도함", Cur_Com.isPo ? "포스텍이" : "카이스트가"));
                        }
                        Debug.Log("이동 명령:" + Cur_Com.dir);
                        Vector3 pos = piece.transform.position;
                        Vector3 direction = new Vector3(0, 0, 0);
                        switch (Cur_Com.dir)
                        {
                            case Command.Direction.NONE:
                                break;
                            case Command.Direction.UP:
                                if (pos.y >= height - 1) break;
                                direction = new Vector3(0, 1, 0);
                                break;
                            case Command.Direction.DOWN:
                                if (pos.y <= 0) break;
                                direction = new Vector3(0, -1, 0);
                                break;
                            case Command.Direction.LEFT:
                                if (pos.x <= 0) break;
                                direction = new Vector3(-1, 0, 0);
                                break;
                            case Command.Direction.RIGHT:
                                if (pos.x >= width - 1) break;
                                direction = new Vector3(1, 0, 0);
                                break;
                            default:
                                break;
                        }
                        piece.transform.Translate(direction);
                        if(piece.transform.position != pos)
                        {
                            GameObject ln = Instantiate(po_ln, pos, Quaternion.identity);
                            ln.GetComponent<Line>().owner = pUnits[0];
                            pUnits[0].GetComponent<Unit>().line.Add(ln);
                        }
                        break;
                    case Command.CommandType.Respawn:
                        if (piece.activeSelf)
                        {
                            Debug.Log(String.Format("{0} 생존한 말을 부활하려고 시도함", Cur_Com.isPo ? "포스텍이" : "카이스트가"));
                        }
                        Debug.Log("부활 명령:" + Cur_Com.pos);
                        bool isinArea = false;
                        Collider2D[] inPosition = Physics2D.OverlapCircleAll(Cur_Com.pos, 0.2f);
                        foreach(Collider2D obj in inPosition)
                        {
                            if (obj.gameObject.GetComponent<Area>() != null) 
                            {
                                isinArea = (obj.gameObject.GetComponent<Area>().isPo == Cur_Com.isPo);
                            }
                        }
                        if (!isinArea) break; 
                        piece.transform.position = Cur_Com.pos;
                        piece.SetActive(true);
                        break;
                    case Command.CommandType.Wait:
                        break;
                    default:
                        break;
                }
            }
        }

        //**최현준 주석: 위의 명령 처리부에서 선을 그리도록 수정했습니다. 이 부분은 확인 후 제거 부탁합니다.
        //
        // for (int i = 0; i < pUnits[i].GetComponent<Unit>().line.Count; i++)
        // {
            // 움직일 때마다 선 그리기 (Line object 생성)
            
            // pLines[i].Add(Instantiate(po_ln, pUnits[i].transform.position, pUnits[i].transform.rotation));

            // xMove = positions[i].x -  pUnits[i].x;
            // yMove = positions[i].y -  pUnits[i].y;

            // if (positions[i].x < 0 || positions[i].x >= 180 || positions[i].y < 0 || positions[i].y >= 120)
            // {
            //     Debug.Log("Wrong Input");
            //     continue;
            // }
            // if (Math.Abs(xMove) + Math.Abs(yMove) == 1)
            // {
            //     pUnits[i] = (positions[i].x, positions[i].y);
            // }
            // else
            // {
            //     Debug.Log("Wrong ")
            // }

            
        // }



        // 충돌 판정 => Kill (한번 업데이트할 때마다!!)
            // 충돌 이후 영역 계산해서 색칠하기(Area)/점수 계산(UI)
        for (int i = 0; i < pUnits.Count; i++)
        {
            if (pUnits[i].GetComponent<Unit>().isTriggerd)
            {
                foreach (GameObject obj in pUnits[i].GetComponent<Unit>().objects)
                {
                    if (obj.tag == "Unit")
                    {
                        killList.Add(obj);
                        killList.Add(pUnits[i]);
                    }
                    else if (obj.tag == "Line")
                    {
                        killList.Add(obj.GetComponent<Line>().owner);
                    }
                    else if (obj.tag == "Area")
                    {
                        if (obj.name.Substring(0, 2) == "po")
                        {
                            foreach (var line in pUnits[i].GetComponent<Unit>().line)
                            {                              
                                pArea.Add(Instantiate(po_ar, line.transform.position, Quaternion.identity));
                                Destroy(line);
                            }
                            pUnits[i].GetComponent<Unit>().line = new List<GameObject>();
                        }
                        else
                        {
                            killList.Add(pUnits[i]);
                        }
                    }
                }
                pUnits[i].GetComponent<Unit>().objects = new List<GameObject>();
                pUnits[i].GetComponent<Unit>().isTriggerd = false;
            }
        }
        for (int i = 0; i < kUnits.Count; i++)
        {
            if (kUnits[i].GetComponent<Unit>().isTriggerd)
            {
                foreach (GameObject obj in kUnits[i].GetComponent<Unit>().objects)
                {
                    if (obj.tag == "Unit")
                    {
                        killList.Add(obj);
                        killList.Add(kUnits[i]);
                    }
                    else if (obj.tag == "Line")
                    {
                        killList.Add(obj.GetComponent<Line>().owner);
                    }
                    else if (obj.tag == "Area")
                    {
                        if (obj.name.Substring(0, 2) == "ka")
                        {
                            foreach (var line in kUnits[i].GetComponent<Unit>().line)
                            {
                                pArea.Add(Instantiate(ka_ar, line.transform.position, Quaternion.identity));
                                Destroy(line);
                            }
                            kUnits[i].GetComponent<Unit>().line = new List<GameObject>();
                        }
                        else
                        {
                            killList.Add(kUnits[i]);
                        }
                    }
                }
                kUnits[i].GetComponent<Unit>().objects = new List<GameObject>();
                kUnits[i].GetComponent<Unit>().isTriggerd = false;
            }
        }

        foreach (var item in killList)
        {
            item.GetComponent<Unit>().Killed();
        }

        killList = new HashSet<GameObject>();
        // 업데이트 마친 후에 변경된 좌표값 출력 => Client에게 전달
        {
            networkManager.SendGameInfo(pUnits,kUnits,pArea,kArea);
        }
        // 승리하면 캐릭터 일러 띄우고 종료/반복
        if (false)
        {
            networkManager.ServerSendGameOver();
            networkManager.isActive = false;
        }
        else
        {
            TurnActive = true;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.anyKeyDown && !pUnits[0].activeSelf){
            pUnits[0].transform.position = new Vector3(0, 0, 0);
            pUnits[0].SetActive(true);
        }
        var pos = pUnits[0].transform.position;
        if(Input.GetKeyDown(KeyCode.UpArrow))
            pUnits[0].transform.Translate(0, 1, 0);
        if(Input.GetKeyDown(KeyCode.DownArrow))
            pUnits[0].transform.Translate(0, -1, 0);
        if(Input.GetKeyDown(KeyCode.LeftArrow))
            pUnits[0].transform.Translate(-1, 0, 0);
        if(Input.GetKeyDown(KeyCode.RightArrow))
            pUnits[0].transform.Translate(1, 0, 0);
        if(Input.anyKeyDown)
        {
            GameObject ln = Instantiate(po_ln, pos, Quaternion.identity);
            ln.GetComponent<Line>().owner = pUnits[0];
            pUnits[0].GetComponent<Unit>().line.Add(ln);
        }
    }

    private void FloodFill(GameObject startPoint)
    {
        bool team = startPoint.name == "po_ar";  // 0: ka, 1: po

        Stack<(int X, int Y)> pixels = new Stack<(int, int)>();
        
        pixels.Push((width - (width + 1) * Convert.ToInt32(team), height - (height + 1) * Convert.ToInt32(team)));
        while (pixels.Count != 0)
        {
            var temp = pixels.Pop();
            int y1 = temp.Y;
            // bool check = Physics.OverlapBox(new Vector3(temp.X, y1, 0f), new Vector3(0.4f, 0.4f, 0.1f), Quaternion.identity, 6);
            // temp x, y 값 사용해서 벡터 종류 찾기
            while (y1 >= -2 && Physics.OverlapBox(new Vector3(temp.X, y1, 0f), new Vector3(0.4f, 0.4f, 0.1f), Quaternion.identity, 6).Length > 0)
            {
                y1--;
            }
            y1++;
            bool spanLeft = false;
            bool spanRight = false;
            while (y1 < height + 1 && Physics.OverlapBox(new Vector3(temp.X, y1, 0f), new Vector3(0.4f, 0.4f, 0.1f), Quaternion.identity, 6).Length > 0)
            {
                // bmp.SetPixel(temp.X, y1, replacementColor);
                pArea.Add(Instantiate(po_ar, new Vector3(temp.X, y1, 0), Quaternion.identity));
                var check1 = Physics.OverlapBox(new Vector3(temp.X - 1, y1, 0f), new Vector3(0.4f, 0.4f, 0.1f), Quaternion.identity, 6).Length > 0;
                var check2 = Physics.OverlapBox(new Vector3(temp.X + 1, y1, 0f), new Vector3(0.4f, 0.4f, 0.1f), Quaternion.identity, 6).Length > 0;

                if (!spanLeft && temp.X > 0 && check1)
                {
                    pixels.Push((temp.X - 1, y1));
                    spanLeft = true;
                }
                else if(spanLeft && temp.X - 1 == 0 && !check1)
                {
                    spanLeft = false;
                }
                if (!spanRight && temp.X < width - 1 && check2)
                {
                    pixels.Push((temp.X + 1, y1));
                    spanRight = true;
                }
                else if (spanRight && temp.X == width && !check2)
                {
                    spanRight = false;
                } 
                y1++;
            }

        }
        // pictureBox1.Refresh();
    }

    public bool isTurnActive()
    {
        return TurnActive;
    }

    private IEnumerator turnTimer() // TURN_TIME 초간 대기하고 플레이어에게 "Timeout$" 전송 후 게임 데이터 전송 (필요시 WaitUntil(isTurnActive)를 사용해서 제어할 것) 
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(TURN_TIME);
            networkManager.ServerSendTurnover();
            TurnActive = false;
            TurnUpdate();
            yield return new WaitUntil(isTurnActive);
            networkManager.ServerSendTurnStart();
        }
    }
}
