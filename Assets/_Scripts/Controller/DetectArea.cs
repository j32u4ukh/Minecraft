using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DetectArea : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[DetectArea] OnTriggerEnter | {other.gameObject.name}");
    }
}
