using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace udemy
{
    public class ChunkDemo : MonoBehaviour
    {
        public Material atlas;
        public int width = 2;
        public int height = 2;
        public int depth = 2;

        // Start is called before the first frame update
        void Start()
        {
            MeshFilter filter = gameObject.AddComponent<MeshFilter>();
            MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.material = atlas;
        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}
