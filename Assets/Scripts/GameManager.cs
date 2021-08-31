using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;


// 유닛 리스트의 유닛 순서는 공개0, 공개1, 비공개0 순서로 함.

public class GameManager : MonoBehaviour
{
    public const int width = 40;
    public const int height = 30;
    public int turncount;
    public const int MAX_TURN = 600;

    public const float TURN_TIME = 0.5f;
    private bool TurnActive;
    Coroutine TurnTimer;

    public class Command
    {
        public enum CommandType { Move, Respawn, Wait };
        public enum Direction { NONE = -1, UP, DOWN, LEFT, RIGHT };

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
    private int kStocks;
    private int pStocks;
    private bool kWin;
    private bool pWin;

    private HashSet<GameObject> killList = new HashSet<GameObject>();
    private HashSet<(int x, int y)> wholeField = new HashSet<(int, int)>();
    private HashSet<(int x, int y)> kField = new HashSet<(int, int)>();
    private HashSet<(int x, int y)> pField = new HashSet<(int, int)>();
    private HashSet<(int x, int y)> floodField = new HashSet<(int, int)>();

    void Awake()
    {

    }

    // Start is called before the first frame update
    void Start()
    {
        turncount = 0;
        kStocks = 4;
        pStocks = 4;
        networkManager = GameObject.Find("NetworkManager").GetComponent<NetworkManager>();
        pUnits.Add(Instantiate(po, new Vector3(0, 0, 0), Quaternion.identity));
        kUnits.Add(Instantiate(ka, new Vector3(width - 1, height - 1, 0), Quaternion.identity));
        pUnits.Add(Instantiate(po, new Vector3(0, 1, 0), Quaternion.identity));
        kUnits.Add(Instantiate(ka, new Vector3(width - 1, height - 2, 0), Quaternion.identity));
        pUnits.Add(Instantiate(po_tp, new Vector3(0, 2, 0), Quaternion.identity));
        kUnits.Add(Instantiate(ka_tp, new Vector3(width - 1, height - 3, 0), Quaternion.identity));
        // Draw Initial Area
        for (int x = 0; x < 4; x++)
        {
            for (int y = 0; y < 3; y++)
            {
                kArea.Add(Instantiate(ka_ar, new Vector3(width - 1 - x, height - 1 - y, 0), Quaternion.identity));
                kField.Add((x, y));
                pArea.Add(Instantiate(po_ar, new Vector3(x, y, 0), Quaternion.identity));
                pField.Add((x, y));
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

    public void MoveUnit(bool isPo, GameObject unit)
    {
        var pos = unit.transform.position;
        unit.transform.Translate(unit.GetComponent<Unit>().nextMove);
        foreach (Collider obj in Physics.OverlapBox(pos, new Vector3(0.4f, 0.4f, 0.1f), Quaternion.identity))
        {
            if (obj.GetComponent<Area>() != null) return;
        }
        GameObject ln;
        if (isPo)
        {
            ln = Instantiate(po_ln, pos, Quaternion.identity);
        }
        else
        {
            ln = Instantiate(ka_ln, pos, Quaternion.identity);
        }
        ln.GetComponent<Line>().owner = unit;
        unit.GetComponent<Unit>().line.Add(ln);
    }

    void TurnUpdate()
    {
        turncount++;
        // Client에게 좌표 받고 Unit 이동
        {
            bool[] isKaCommanded = new bool[3] { false, false, false };
            bool[] isPoCommanded = new bool[3] { false, false, false };
            while(CommandQueue.Count > 0)
            {
                Command Cur_Com;
                CommandQueue.TryDequeue(out Cur_Com);
                GameObject piece;
                if (Cur_Com.pieceNum > 3)
                {
                    Debug.Log(Cur_Com.isPo?"포스텍":"카이스트" + " 말의 번호가 지나치게 큼: " + Cur_Com.pieceNum);
                    continue;
                }
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
                        if (!piece.activeSelf)
                        {
                            Debug.Log(String.Format("{0} 사망한 말을 이동하려고 시도함", Cur_Com.isPo ? "포스텍이" : "카이스트가"));
                            if (Cur_Com.isPo)
                            {
                                isPoCommanded[Cur_Com.pieceNum] = false;
                            }
                            else
                            {
                                isKaCommanded[Cur_Com.pieceNum] = false;
                            }
                            break;
                        }
                        Vector3 pos = piece.transform.position;
                        Vector3 direction = new Vector3(0, 0, 0);
                        switch (Cur_Com.dir)
                        {
                            case Command.Direction.NONE:
                                break;
                            case Command.Direction.UP:
                                direction = new Vector3(0, 1, 0);
                                break;
                            case Command.Direction.DOWN:
                                direction = new Vector3(0, -1, 0);
                                break;
                            case Command.Direction.LEFT:
                                direction = new Vector3(-1, 0, 0);
                                break;
                            case Command.Direction.RIGHT:
                                direction = new Vector3(1, 0, 0);
                                break;
                            default:
                                break;
                        }
                        if(direction != new Vector3(0, 0, 0))
                        {
                            piece.GetComponent<Unit>().nextMove = direction;
                        }
                        break;
                    case Command.CommandType.Respawn:
                        if (piece.activeSelf)
                        {
                            Debug.Log(String.Format("{0} 생존한 말을 부활하려고 시도함", Cur_Com.isPo ? "포스텍이" : "카이스트가"));
                            if (Cur_Com.isPo)
                            {
                                isPoCommanded[Cur_Com.pieceNum] = false;
                            }
                            else
                            {
                                isKaCommanded[Cur_Com.pieceNum] = false;
                            }
                            break;
                        }
                        if ((Cur_Com.isPo ? pStocks : kStocks) < 1) break;
                        bool isinArea = false;
                        Collider[] Lapsing = Physics.OverlapSphere(new Vector3(Cur_Com.pos.x, Cur_Com.pos.y), 0.2f);
                        foreach (Collider obj in Lapsing)
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
                        if (Cur_Com.isPo)
                        {
                            isPoCommanded[Cur_Com.pieceNum] = false;
                        }
                        else
                        {
                            isKaCommanded[Cur_Com.pieceNum] = false;
                        }
                        break;
                    default:
                        break;
                }
            }
            //말을 다음에 이동할 방향으로 이동
            for(int i = 0; i < 3; i++)
            {
                if (kUnits[i].activeSelf)
                {
                    var pos = kUnits[i].transform.position;
                    MoveUnit(false, kUnits[i]);
                    if(kUnits[i].transform.position.x < 0 ||
                       kUnits[i].transform.position.x >= width ||
                       kUnits[i].transform.position.y < 0 ||
                       kUnits[i].transform.position.y >= height)
                    {
                        killList.Add(kUnits[i]);
                    }
                }
                if (pUnits[i].activeSelf)
                {
                    var pos = pUnits[i].transform.position;
                    MoveUnit(true, pUnits[i]);
                    if (pUnits[i].transform.position.x < 0 ||
                       pUnits[i].transform.position.x >= width ||
                       pUnits[i].transform.position.y < 0 ||
                       pUnits[i].transform.position.y >= height)
                    {
                        killList.Add(pUnits[i]);
                    }
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
                        Debug.Log(i + "번 포스텍 유닛 다른 유닛과 충돌");
                        killList.Add(obj);
                        killList.Add(pUnits[i]);
                    }
                    else if (obj.tag == "Line")
                    {
                        Debug.Log(i + "번 포스텍 유닛 선 밟음");
                        killList.Add(obj.GetComponent<Line>().owner);
                    }
                    else if (obj.tag == "Area")
                    {
                        if (obj.name.Substring(0, 2) == "po" && pUnits[i].GetComponent<Unit>().line.Count > 0)
                        {
                            Debug.Log(i + "번 포스텍 유닛 채우기 함수 실행");
                            foreach (var line in pUnits[i].GetComponent<Unit>().line)
                            {
                                Debug.Log(i + "번 포스텍 유닛 선을 파괴하고 영역으로 대체");
                                pArea.Add(Instantiate(po_ar, line.transform.position, Quaternion.identity));
                                pField.Add(((int)line.transform.position.x, (int)line.transform.position.y));
                                Destroy(line);
                            }
                            if (pUnits[i].GetComponent<Unit>().line.Count > 0)
                            {
                                Debug.Log(i + "번 포스텍 유닛 영역 채우기");
                                floodField = new HashSet<(int, int)>();
                                HashSet<(int, int)> tempSet = new HashSet<(int, int)>();
                                tempSet.UnionWith(wholeField);
                                floodField.UnionWith(pField);
                                FloodFill(width, height);
                                tempSet.ExceptWith(floodField);
                                foreach ((int x, int y) item in tempSet)
                                {
                                    pArea.Add(Instantiate(po_ar, new Vector3(item.x, item.y, 0), Quaternion.identity));
                                    pField.Add(item);
                                }
                            }
                            pUnits[i].GetComponent<Unit>().line = new List<GameObject>();
                        }
                        else if (obj.name.Substring(0, 2) == "ka")
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
                        Debug.Log(i + "번 카이스트 유닛 다른 유닛에 충돌함");
                        killList.Add(obj);
                        killList.Add(kUnits[i]);
                    }
                    else if (obj.tag == "Line")
                    {
                        Debug.Log(i + "번 카이스트 유닛 선 밟음");
                        killList.Add(obj.GetComponent<Line>().owner);
                    }
                    else if (obj.tag == "Area")
                    {
                        if (obj.name.Substring(0, 2) == "ka" && kUnits[i].GetComponent<Unit>().line.Count > 0)
                        {
                            foreach (var line in kUnits[i].GetComponent<Unit>().line)
                            {
                                Debug.Log(i + "번 카이스트 유닛 선을 영역으로 바꿈");
                                kArea.Add(Instantiate(ka_ar, line.transform.position, Quaternion.identity));
                                kField.Add(((int)line.transform.position.x, (int)line.transform.position.y));
                                Destroy(line);
                            }
                            if (kUnits[i].GetComponent<Unit>().line.Count != 0)
                            {
                                Debug.Log(i + "번 카이스트 유닛 영역 채우기");
                                floodField = new HashSet<(int, int)>();
                                HashSet<(int, int)> tempSet = new HashSet<(int, int)>();
                                tempSet.UnionWith(wholeField);
                                floodField.UnionWith(kField);
                                FloodFill(-1, -1);
                                tempSet.ExceptWith(floodField);
                                foreach ((int x, int y) item in tempSet)
                                {
                                    kArea.Add(Instantiate(ka_ar, new Vector3(item.x, item.y, 0), Quaternion.identity));
                                    kField.Add(item);
                                }
                            }
                            kUnits[i].GetComponent<Unit>().line = new List<GameObject>();
                        }
                        else if (obj.name.Substring(0, 2) == "po")
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
        // 승리하면 캐릭터 일러 띄우고 종료/반복
        if (turncount >= MAX_TURN || kField.Count >= width * height / 2 || pField.Count >= width * height / 2)
        {
            if (kStocks < 1)
            {
                pWin = true;
                foreach (var item in kUnits)
                {
                    if (!item.Equals(null))
                    {
                        pWin = false;
                        break;
                    }
                }
            }
            if (pStocks < 1)
            {
                kWin = true;
                foreach (var item in pUnits)
                {
                    if (!item.Equals(null))
                    {
                        kWin = false;
                        break;
                    }
                }
            }
            if (kField.Count >= pField.Count) kWin = true;
            if (pField.Count >= kField.Count) pWin = true;
            if (kWin) ; // k 에게 승리 메시지
            if (pWin) ; // p 에게 승리 메시지
            networkManager.ServerSendGameOver();
            networkManager.isActive = false;
        }
        else
        {
            TurnActive = true;
        }
        killList = new HashSet<GameObject>();
        // 업데이트 마친 후에 변경된 좌표값 출력 => Client에게 전달
        {
            networkManager.SendGameInfo(pUnits, kUnits, pArea, kArea);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.anyKeyDown && !pUnits[0].activeSelf)
        {
            pUnits[0].transform.position = new Vector3(0, 0, 0);
            pUnits[0].SetActive(true);
        }
        var pos = pUnits[0].transform.position;
        if (Input.GetKeyDown(KeyCode.UpArrow))
            pUnits[0].transform.Translate(0, 1, 0);
        if (Input.GetKeyDown(KeyCode.DownArrow))
            pUnits[0].transform.Translate(0, -1, 0);
        if (Input.GetKeyDown(KeyCode.LeftArrow))
            pUnits[0].transform.Translate(-1, 0, 0);
        if (Input.GetKeyDown(KeyCode.RightArrow))
            pUnits[0].transform.Translate(1, 0, 0);
        if (Input.anyKeyDown && Physics.OverlapBox(pos, new Vector3(0.4f, 0.4f, 0.1f), Quaternion.identity).Length == 1)
        {
            GameObject ln = Instantiate(po_ln, pos, Quaternion.identity);
            ln.GetComponent<Line>().owner = pUnits[0];
            pUnits[0].GetComponent<Unit>().line.Add(ln);
        }
    }

    private void FloodFill(int x, int y)
    {
        Stack<(int x, int y)> ps = new Stack<(int, int)>();
        ps.Push((x, y));
        while (ps.Count != 0)
        {
            var pop = ps.Pop();
            if (pop.x < -1 || pop.x >= width + 1) continue;
            if (pop.y < -1 || pop.y >= height + 1) continue;
            if (!floodField.Contains((pop.x, pop.y)))
            {
                floodField.Add((pop.x, pop.y));
                ps.Push((pop.x + 1, pop.y));
                ps.Push((pop.x, pop.y + 1));
                ps.Push((pop.x - 1, pop.y));
                ps.Push((pop.x, pop.y - 1));
            }
        }
    }
    // pictureBox1.Refresh();

    public bool isTurnActive()
    {
        return TurnActive;
    }

    private IEnumerator turnTimer() // TURN_TIME 초간 대기하고 플레이어에게 "TurnTimeOut$" 전송 후 게임 데이터 전송 (필요시 WaitUntil(isTurnActive)를 사용해서 제어할 것) 
    {
        while (true)
        {
            networkManager.ServerSendTurnStart();
            yield return new WaitForSecondsRealtime(TURN_TIME);
            networkManager.ServerSendTurnover();
            TurnActive = false;
            TurnUpdate();
            yield return new WaitUntil(isTurnActive);
        }
    }
}
