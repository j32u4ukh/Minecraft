using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VertexData = System.Tuple<UnityEngine.Vector3, UnityEngine.Vector3, UnityEngine.Vector2, UnityEngine.Vector2>;

namespace udemy
{
    public class Test : MonoBehaviour
    {
        // Start is called before the first frame update
        void Start()
        {
            int i0 = 1015, i1 = 1017, i2 = 1018;
            Vector3Int v0 = new Vector3Int(5, 1, 10), v1 = new Vector3Int(7, 1, 10), v2 = new Vector3Int(8, 1, 10);

            Vector3Int v3 = WorldDemo3.flatToVector3Int(i0);
            Debug.Log($"i0: {i0} -> {v3}");

            v3 = WorldDemo3.flatToVector3Int(i1);
            Debug.Log($"i1: {i1} -> {v3}");

            v3 = WorldDemo3.flatToVector3Int(i2);
            Debug.Log($"i2: {i2} -> {v3}");

            int i3 = WorldDemo3.vector3IntToFlat(v0);
            Debug.Log($"v0: {v0} -> {i3}");

            i3 = WorldDemo3.vector3IntToFlat(v1);
            Debug.Log($"v1: {v1} -> {i3}");

            i3 = WorldDemo3.vector3IntToFlat(v2);
            Debug.Log($"v2: {v2} -> {i3}");
        }
    }
}
