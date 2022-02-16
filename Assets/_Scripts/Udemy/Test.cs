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
            Data data = new Data(2, 5);
            outTest(data);
            print(data.count);
        }

        void outTest(Data data)
        {
            data.count = 5;
        }
    }

    class Data
    {
        public int count;
        public int[] values;

        public Data(params int[] values)
        {
            count = values.Length;
            this.values = values;
            //this.values = new int[values.Length];
            //int i, len = values.Length;

            //for(i = 0; i < len; i++)
            //{
            //    this.values[i] = values[i];
            //}
        }
    }
}
