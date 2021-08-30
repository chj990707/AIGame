using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public int width = 180;
    public int height = 120;

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
        pUnits.Add(Instantiate(po, new Vector3(0, 0, 0), Quaternion.identity));
        kUnits.Add(Instantiate(ka, new Vector3(width - 1, height - 1, 0), Quaternion.identity));
        // Draw Initial Area
        for (int x = 0; x < 6; x++){
            for (int y = 0; y < 4; y ++){
                kArea.Add(Instantiate(ka_ar, new Vector3(width - 1 - x, height - 1 - y, 0), Quaternion.identity));
                pArea.Add(Instantiate(po_ar, new Vector3(x, y, 0), Quaternion.identity));
            }
        }
        Time.fixedDeltaTime = 0.005f;
    }

    void FixedUpdate()
    {
        // Client에게 좌표 받고 Unit 이동
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

        // 승리하면 캐릭터 일러 띄우고 종료/반복
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
}
