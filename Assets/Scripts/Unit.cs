using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Unit : MonoBehaviour
{
    public bool isTriggerd = false;
    public List<GameObject> objects = new List<GameObject>();
    public List<GameObject> line = new List<GameObject>();

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnTriggerEnter(Collider other) {
        isTriggerd = true;
        objects.Add(other.gameObject);
    }

    public void Killed()
    {
        foreach (var item in line)
        {
            Destroy(item);
        }
        line = new List<GameObject>();
        gameObject.SetActive(false);
    }
}