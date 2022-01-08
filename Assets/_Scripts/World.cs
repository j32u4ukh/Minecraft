using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public struct PerlinSettings
{
    public float heightScale;
    public float scale;
    public int octaves;
    public float heightOffset;
    public float probability;

    public PerlinSettings(float hs, float s, int o, float ho, float p)
    {
        heightScale= hs;
        scale = s;
        octaves = o;
        heightOffset = ho;
        probability = p;
    }
}

public class World : MonoBehaviour
{
    public static Vector3Int worldDimesions = new Vector3Int(4, 4, 4);
    public static Vector3Int chunkDimensions = new Vector3Int(10, 10, 10);
    public GameObject chunkPrefab;
    public GameObject mCamera;
    public GameObject fpc;
    public Slider loadingBar;

    public static PerlinSettings surfaceSettings;
    public PerlinGrapher surface;

    public static PerlinSettings stoneSettings;
    public PerlinGrapher stone;

    public static PerlinSettings diamondTSettings;
    public PerlinGrapher diamondT;

    public static PerlinSettings diamondBSettings;
    public PerlinGrapher diamondB;

    public static PerlinSettings caveSettings;
    public Perlin3DGrapher caves;

    HashSet<Vector3Int> chunkChecker = new HashSet<Vector3Int>();
    HashSet<Vector2Int> chunkColumns = new HashSet<Vector2Int>();
    Dictionary<Vector3Int, Chunk> chunks = new Dictionary<Vector3Int, Chunk>();

    // �e�@���@�ɫسy�ɡA���a�Ҧb��m
    Vector3Int lastBuildPosition;
    int drawRadius = 3;

    Queue<IEnumerator> buildQueue = new Queue<IEnumerator>();

    IEnumerator BuildCoordinator()
    {
        while (true)
        {
            while(buildQueue.Count > 0)
            {
                yield return StartCoroutine(buildQueue.Dequeue());
            }

            yield return null;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        loadingBar.maxValue = worldDimesions.x * worldDimesions.z;

        surfaceSettings = new PerlinSettings(surface.heightScale, 
                                             surface.scale, 
                                             surface.octaves, 
                                             surface.heightOffset, 
                                             surface.probability);

        stoneSettings = new PerlinSettings(stone.heightScale,
                                           stone.scale,
                                           stone.octaves,
                                           stone.heightOffset,
                                           stone.probability);

        diamondTSettings = new PerlinSettings(diamondT.heightScale,
                                              diamondT.scale,
                                              diamondT.octaves,
                                              diamondT.heightOffset,
                                              diamondT.probability);

        diamondBSettings = new PerlinSettings(diamondB.heightScale,
                                              diamondB.scale,
                                              diamondB.octaves,
                                              diamondB.heightOffset,
                                              diamondB.probability);

        caveSettings = new PerlinSettings(caves.heightScale,
                                          caves.scale,
                                          caves.octaves,
                                          caves.heightOffset,
                                          caves.DrawCutOff);

        StartCoroutine(BuildWorld());
    }

    // �s�W�@�� Chunk
    void BuildChunkColumn(int x, int z)
    {
        for (int y = 0; y < worldDimesions.y; y++)
        {
            Vector3Int position = new Vector3Int(x, chunkDimensions.y * y, z);

            if (!chunkChecker.Contains(position))
            {
                GameObject chunk = Instantiate(chunkPrefab);
                chunk.name = $"Chunk_{position.x}_{position.y}_{position.z}";
                Chunk c = chunk.GetComponent<Chunk>();
                c.CreateChunk(dimensions: chunkDimensions, position: position);
                chunkChecker.Add(position);
                chunks.Add(position, c);
            }
            else
            {
                chunks[position].meshRenderer.enabled = true;
            }           
        }

        chunkColumns.Add(new Vector2Int(x, z));
    }

    IEnumerator BuildWorld()
    {
        for (int z = 0; z < worldDimesions.z; z++)
        {
            for (int x = 0; x < worldDimesions.x; x++)
            {
                BuildChunkColumn(chunkDimensions.x * x, chunkDimensions.z * z);
                loadingBar.value++;
                yield return null;
            }
        }

        mCamera.SetActive(false);
        loadingBar.gameObject.SetActive(false);

        int xpos = chunkDimensions.x * worldDimesions.x / 2;
        int zpos = chunkDimensions.z * worldDimesions.z / 2;
        int ypos = (int)MeshUtils.fBM(xpos, zpos,
                                      surfaceSettings.octaves, 
                                      surfaceSettings.scale, 
                                      surfaceSettings.heightScale, 
                                      surfaceSettings.heightOffset) + 10;
        fpc.transform.position = new Vector3Int(xpos, ypos, zpos);
        fpc.SetActive(true);
        //lastBuildPosition = new Vector3Int(xpos, ypos, zpos);
        lastBuildPosition = Vector3Int.CeilToInt(fpc.transform.position);


        // �̧ǰ��� buildQueue ���� IEnumerator
        StartCoroutine(BuildCoordinator());

        // �N IEnumerator �K�[�� buildQueue ��
        StartCoroutine(UpdateWorld());
    }

    WaitForSeconds wfs = new WaitForSeconds(0.5f);
    IEnumerator UpdateWorld()
    {
        while (true)
        {
            // ���a���ʶZ�����W�@���@�ɥͦ��I lastBuildPosition �j�L�@�� chunk ���Z����
            if ((lastBuildPosition - fpc.transform.position).magnitude > chunkDimensions.x)
            {
                lastBuildPosition = Vector3Int.CeilToInt(fpc.transform.position);
                int posx = (int)(fpc.transform.position.x / chunkDimensions.x) * chunkDimensions.x;
                int posz = (int)(fpc.transform.position.z / chunkDimensions.z) * chunkDimensions.z;
                buildQueue.Enqueue(BuildRecursiveWorld(posx, posz, drawRadius));
                buildQueue.Enqueue(HideColumns(posx, posz));
            }

            yield return wfs;
        }
    }

    public void HideChunkColumn(int x, int z)
    {
        for(int y = 0; y < worldDimesions.y; y++)
        {
            // Chunk position
            Vector3Int pos = new Vector3Int(x, y * chunkDimensions.y, z);

            if (chunkChecker.Contains(pos))
            {
                chunks[pos].meshRenderer.enabled = false;
            }
        }
    }

    IEnumerator HideColumns(int x, int z)
    {
        Vector2Int fpcPos = new Vector2Int(x, z);

        // cc: chunk position
        foreach (Vector2Int cc in chunkColumns)
        {
            if((cc - fpcPos).magnitude >= drawRadius * chunkDimensions.x)
            {
                // ��ڤW�O Z �ȡA�� Vector2Int �����ݩʬ� y
                HideChunkColumn(cc.x, cc.y);
            }
        }

        yield return null;
    }

    IEnumerator BuildRecursiveWorld(int x, int z, int rad)
    {
        int nextrad = rad - 1;

        if(nextrad <= 0)
        {
            yield break;
        }

        BuildChunkColumn(x, z + chunkDimensions.z);

        // Next chunk z position: z + chunkDimensions.z
        buildQueue.Enqueue(BuildRecursiveWorld(x, z + chunkDimensions.z, nextrad));
        yield return null;

        BuildChunkColumn(x, z - chunkDimensions.z);

        buildQueue.Enqueue(BuildRecursiveWorld(x, z - chunkDimensions.z, nextrad));
        yield return null;

        BuildChunkColumn(x + chunkDimensions.x, z);
        buildQueue.Enqueue(BuildRecursiveWorld(x + chunkDimensions.x, z, nextrad));
        yield return null;

        BuildChunkColumn(x - chunkDimensions.x, z);
        buildQueue.Enqueue(BuildRecursiveWorld(x - chunkDimensions.x, z, nextrad));
        yield return null;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
