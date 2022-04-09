using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace udemy
{
    [ExecuteInEditMode]
    public class DisplayOneBlock : MonoBehaviour
    {
        public Material material;
        public BlockType block_type = BlockType.DIRT;
        public Vector3Int location = Vector3Int.zero;

        MeshFilter mesh_filter;

        // Start is called before the first frame update
        void Start()
        {
            mesh_filter = gameObject.GetComponent<MeshFilter>();
            gameObject.GetComponent<MeshRenderer>().material = material;
        }

        private void OnValidate()
        {
            Block block = new Block(block_type, Vector3Int.zero);
            mesh_filter.mesh = block.mesh;
        }
    }
}
