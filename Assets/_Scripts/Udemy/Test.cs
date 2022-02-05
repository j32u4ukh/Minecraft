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
            int x, z, len = 10000;
            float value = 0f;
            
            for (x = 0; x < len; x++)
            {
                for (z = 0; z < len; z++)
                {
                    value += Mathf.PerlinNoise(x, z);
                }
            }

            value /= (len * len);
            print($"mean: {value}");
        }
    }
}
