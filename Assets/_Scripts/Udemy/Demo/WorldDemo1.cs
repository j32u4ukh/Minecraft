using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace udemy
{
    public class WorldDemo1 : MonoBehaviour
    {
        public static Vector3Int world_dimesions = new Vector3Int(4, 4, 4);
        public static Vector3Int chunk_dimensions = new Vector3Int(10, 10, 10);
        public GameObject chunk_prefab;

        public GameObject main_camera;
        public GameObject fpc;
        public Slider loading_bar;

        public StrataSetting surface_setting;

        void Start() 
        {
            loading_bar.maxValue = world_dimesions.x * world_dimesions.y * world_dimesions.z;
            StartCoroutine(buildWorld());
        }

        IEnumerator buildWorld()
        {
            for (int z = 0; z < world_dimesions.z; z++)
            {
                for (int y = 0; y < world_dimesions.y; y++)
                {
                    for (int x = 0; x < world_dimesions.x; x++)
                    {
                        GameObject chunk_obj = Instantiate(chunk_prefab);
                        Vector3Int location = new Vector3Int(x * chunk_dimensions.x,
                                                             y * chunk_dimensions.y,
                                                             z * chunk_dimensions.z);
                        Chunk1 chunk = chunk_obj.GetComponent<Chunk1>();
                        chunk.createChunk(dimensions: chunk_dimensions, location: location);
                        loading_bar.value++;
                        yield return null;
                    }
                }
            }

            main_camera.SetActive(false);

            // Place the player in the center of map
            int xpos = chunk_dimensions.x * world_dimesions.x / 2;
            int zpos = chunk_dimensions.z * world_dimesions.z / 2;
            float offset = surface_setting.getOffset();
            int ypos = (int)surface_setting.getAltitude(xpos, zpos, offset: offset) + 10;
            fpc.transform.position = new Vector3(xpos, ypos, zpos);
            fpc.SetActive(true);
            loading_bar.gameObject.SetActive(false);
        }
    }
}
