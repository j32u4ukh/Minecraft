using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace udemy
{
    public class DropItemsTest : MonoBehaviour
    {
        public GameObject drop_block_prefab;

        // Start is called before the first frame update
        void Start()
        {
            DropBlock drop_block = Instantiate(drop_block_prefab).GetComponent<DropBlock>();
            drop_block.init(BlockType.Dirt, location: new Vector3Int(1, 5, 1));
        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}
