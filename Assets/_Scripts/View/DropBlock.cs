using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace udemy
{
    public class DropBlock : MonoBehaviour
    {
        [SerializeField] private float life_time;
        float delta_time = 0.02f;
        WaitForSeconds wfs = new WaitForSeconds(0.02f);

        private BlockType block_type;
        private float scale = 0.2f;

        private void OnDestroy()
        {
            // TODO: Invoke system & recycle this resource
            Debug.Log($"[DropBlock] OnDestroy");
        }

        // TODO: IDropItem
        public void init(BlockType block_type, Vector3Int location, float life_time = 10f)
        {
            this.block_type = block_type;
            transform.position = location;

            Block block = new Block(block_type, Vector3Int.zero);
            gameObject.GetComponent<MeshFilter>().mesh = block.mesh;
            transform.localScale = new Vector3(scale, scale, scale);

            this.life_time = life_time;
            wfs = new WaitForSeconds(delta_time);

            StartCoroutine(lifeTimeCoroutine());
        }

        IEnumerator lifeTimeCoroutine()
        {
            while(life_time > 0f)
            {
                life_time -= delta_time;

                yield return wfs;
            }

            Destroy(gameObject);
        }

        public BlockType getBlockType()
        {
            return block_type;
        }
    }
}
