using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class World : MonoBehaviour
{
    public static Vector3 worldDimesions = new Vector3(3, 3, 3);
    public static Vector3 chunkDimensions = new Vector3(10, 10, 10);
    public GameObject chunkPrefab;
    public GameObject mCamera;
    public GameObject fpc;
    public Slider loadingBar;


    // Start is called before the first frame update
    void Start()
    {
        loadingBar.maxValue = worldDimesions.x * worldDimesions.y * worldDimesions.z;
        StartCoroutine(BuildWorld());
    }

    IEnumerator BuildWorld()
    {
        for (int z = 0; z < worldDimesions.z; z++)
        {
            for (int y = 0; y < worldDimesions.y; y++)
            {
                for (int x = 0; x < worldDimesions.x; x++)
                {
                    GameObject chunk = Instantiate(chunkPrefab);
                    Vector3 position = new Vector3(chunkDimensions.x * x, chunkDimensions.y * y, chunkDimensions.z * z);
                    chunk.GetComponent<Chunk>().CreateChunk(dimensions: chunkDimensions, position: position);
                    loadingBar.value++;
                    yield return null;
                }
            }
        }

        mCamera.SetActive(false);
        loadingBar.gameObject.SetActive(false);

        float xpos = chunkDimensions.x * worldDimesions.x / 2.0f;
        float zpos = chunkDimensions.z * worldDimesions.z / 2.0f;
        Chunk c = chunkPrefab.GetComponent<Chunk>();
        float ypos = MeshUtils.fBM(xpos, zpos, c.octaves, c.scale, c.hightScale, c.heightOffset) + 10f;
        fpc.transform.position = new Vector3(xpos, ypos, zpos);
        fpc.SetActive(true);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
