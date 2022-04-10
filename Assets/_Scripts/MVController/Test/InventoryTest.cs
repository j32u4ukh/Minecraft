using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using udemy;

public class InventoryTest : MonoBehaviour
{
    public InventoryData data;
    GameObject player;
    ActionStore store;

    // Start is called before the first frame update
    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        store = player.GetComponent<ActionStore>();

        store.AddAction(item: data, index: 0, number: 1);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
