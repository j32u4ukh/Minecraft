using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace udemy
{
    public class DetectArea : MonoBehaviour
    {
        GameObject player;
        ActionStore store;

        private void Start()
        {
            player = GameObject.FindGameObjectWithTag("Player");
            store = player.GetComponent<ActionStore>();
        }

        private void OnTriggerEnter(Collider other)
        {
            Debug.Log($"[DetectArea] OnTriggerEnter | {other.gameObject.name}");

            // TODO: 先用 tag 做初步判斷，再決定轉型的類型與數據的路徑
            if (other.gameObject.name.ToLower().Equals("dirt"))
            {
                Debug.Log($"[DetectArea] OnTriggerEnter | Get dirt block.");
                InventoryData data = Resources.Load<InventoryData>("ComponentDatas/ActionData/Dirt");
                store.addAction(data: data, number: 1);
            }
        }
    }
}
