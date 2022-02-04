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
            VertexData vd1 = new VertexData(new Vector3(0, 0, 0), new Vector3(0, 0, 1), new Vector2(0, 0), new Vector2(0, 0));
            VertexData vd2 = new VertexData(new Vector3(0, 0, 0), new Vector3(0, 0, 0), new Vector2(0, 0), new Vector2(0, 0));
            print(vd1.Equals(vd2));
        }
    }
}
