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
            Vector3Int v = new Vector3Int(3, 4, 5);
            print(v);

            v.y += 10;
            print(v);
        }
    }
}
