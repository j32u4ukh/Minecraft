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

            // TODO: ���� tag ����B�P�_�A�A�M�w�૬�������P�ƾڪ����|
            if (other.gameObject.name.ToLower().Equals("dirt"))
            {
                Debug.Log($"[DetectArea] OnTriggerEnter | Get dirt block.");
                InventoryData data = Resources.Load<InventoryData>("ComponentDatas/ActionData/Dirt");
                store.addAction(data: data, number: 1);
            }
        }
    }
}
