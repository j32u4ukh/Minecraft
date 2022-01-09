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
    public static Vector3Int worldDimesions = new Vector3Int(5, 5, 5);
    public static Vector3Int extraWorldDimesions = new Vector3Int(5, 5, 5);
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

    // 前一次世界建造時，玩家所在位置
    Vector3Int lastBuildPosition;
    int drawRadius = 3;

    Queue<IEnumerator> buildQueue = new Queue<IEnumerator>();
    WaitForSeconds wfs = new WaitForSeconds(0.5f);
    MeshUtils.BlockType buildType = MeshUtils.BlockType.DIRT;

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

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
        {
            RaycastHit hit;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out hit, 10f))
            {
                Vector3 hitBlock;

                if (Input.GetMouseButtonDown(0))
                {
                    hitBlock = hit.point - hit.normal / 2.0f;
                }
                else
                {
                    hitBlock = hit.point + hit.normal / 2.0f;
                }

                // Debug.Log($"Block location: {hitBlock}");
                Chunk thisChunk = hit.collider.gameObject.GetComponent<Chunk>();
                int bx = (int)(Mathf.Round(hitBlock.x) - thisChunk.location.x);
                int by = (int)(Mathf.Round(hitBlock.y) - thisChunk.location.y);
                int bz = (int)(Mathf.Round(hitBlock.z) - thisChunk.location.z);

                Vector3Int neighbour;

                if(bx == chunkDimensions.x)
                {
                    neighbour = new Vector3Int((int)thisChunk.location.x + chunkDimensions.x, (int)thisChunk.location.y, (int)thisChunk.location.z);
                    thisChunk = chunks[neighbour];
                    bx = 0;
                }
                else if (bx == -1)
                {
                    neighbour = new Vector3Int((int)thisChunk.location.x - chunkDimensions.x, (int)thisChunk.location.y, (int)thisChunk.location.z);
                    thisChunk = chunks[neighbour];
                    bx = chunkDimensions.x - 1;
                }
                else if (by == chunkDimensions.y)
                {
                    neighbour = new Vector3Int((int)thisChunk.location.x, (int)thisChunk.location.y + chunkDimensions.y, (int)thisChunk.location.z);
                    thisChunk = chunks[neighbour];
                    by = 0;
                }
                else if (by == -1)
                {
                    neighbour = new Vector3Int((int)thisChunk.location.x, (int)thisChunk.location.y - chunkDimensions.y, (int)thisChunk.location.z);
                    thisChunk = chunks[neighbour];
                    by = chunkDimensions.y - 1;
                }
                else if (bz == chunkDimensions.z)
                {
                    neighbour = new Vector3Int((int)thisChunk.location.x, (int)thisChunk.location.y, (int)thisChunk.location.z + chunkDimensions.z);
                    thisChunk = chunks[neighbour];
                    bz = 0;
                }
                else if (bz == -1)
                {
                    neighbour = new Vector3Int((int)thisChunk.location.x, (int)thisChunk.location.y, (int)thisChunk.location.z - chunkDimensions.z);
                    thisChunk = chunks[neighbour];
                    bz = chunkDimensions.z - 1;
                }

                int i = bx + chunkDimensions.x * (by + chunkDimensions.z * bz);

                if (Input.GetMouseButtonDown(0))
                {
                    // TODO: 教學中為了避免 health 為 -1 的方塊被刪除，因此加了這個判斷，但其實根本沒必要。health 從 NOCRACK(10) 開始往上加，本來就不可能加到 -1
                    if (MeshUtils.blockTypeHealth[(int)thisChunk.chunkData[i]] != -1)
                    {
                        thisChunk.healthData[i]++;

                        if (thisChunk.healthData[i] == MeshUtils.BlockType.NOCRACK + MeshUtils.blockTypeHealth[(int)thisChunk.chunkData[i]])
                        {
                            thisChunk.chunkData[i] = MeshUtils.BlockType.AIR;
                            //thisChunk.healthData[i] = MeshUtils.BlockType.NOCRACK;
                        }
                    }
                }
                else
                {
                    thisChunk.chunkData[i] = buildType;
                    thisChunk.healthData[i] = MeshUtils.BlockType.NOCRACK;
                }

                DestroyImmediate(thisChunk.GetComponent<MeshFilter>());
                DestroyImmediate(thisChunk.GetComponent<MeshRenderer>());
                DestroyImmediate(thisChunk.GetComponent<Collider>());
                thisChunk.CreateChunk(chunkDimensions, thisChunk.location, false);
            }
        }
    }


    public void SetBuildType(int type)
    {
        buildType = (MeshUtils.BlockType)type;
        Debug.Log($"buildType: {buildType}, type: {type}");
    }

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

    // 新增一欄 Chunk
    void BuildChunkColumn(int x, int z, bool meshEnabled = true)
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

            chunks[position].meshRenderer.enabled = meshEnabled;
        }

        chunkColumns.Add(new Vector2Int(x, z));
    }

    IEnumerator BuildExtraWorld()
    {
        int zStart = worldDimesions.z;
        int zEnd = worldDimesions.z + extraWorldDimesions.z;
        int xStart = worldDimesions.x;
        int xEnd = worldDimesions.x + extraWorldDimesions.x;

        for (int z = zStart; z < zEnd; z++)
        {
            for (int x = 0; x < xEnd; x++)
            {
                BuildChunkColumn(chunkDimensions.x * x, chunkDimensions.z * z, meshEnabled: false);
                yield return null;
            }
        }

        for (int z = 0; z < zEnd; z++)
        {
            for (int x = xStart; x < xEnd; x++)
            {
                BuildChunkColumn(chunkDimensions.x * x, chunkDimensions.z * z, meshEnabled: false);
                yield return null;
            }
        }

        //for (int z = 0; z < worldDimesions.z + extraWorldDimesions.z; z++)
        //{
        //    for (int x = 0; x < worldDimesions.x + extraWorldDimesions.x; x++)
        //    {
        //        if ((z < worldDimesions.z) && (x < worldDimesions.x))
        //        {
        //            continue;
        //        }

        //        BuildChunkColumn(chunkDimensions.x * x, chunkDimensions.z * z);
        //        yield return null;
        //    }
        //}
    }

    IEnumerator BuildWorld()
    {
        for (int z = 0; z < worldDimesions.z; z++)
        {
            for (int x = 0; x < worldDimesions.x; x++)
            {
                BuildChunkColumn(chunkDimensions.x * x, chunkDimensions.z * z, meshEnabled: true);
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


        // 依序執行 buildQueue 當中的 IEnumerator
        StartCoroutine(BuildCoordinator());

        // 將 IEnumerator 添加到 buildQueue 當中
        //StartCoroutine(UpdateWorld());
        
        // 在背景中持續生成環境
        StartCoroutine(BuildExtraWorld());
    }

    IEnumerator UpdateWorld()
    {
        while (true)
        {
            // 當玩家移動距離離上一次世界生成點 lastBuildPosition 大過一個 chunk 的距離時
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
                // 實際上是 Z 值，但 Vector2Int 本身屬性為 y
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

        BuildChunkColumn(x, z + chunkDimensions.z, meshEnabled: true);

        // Next chunk z position: z + chunkDimensions.z
        buildQueue.Enqueue(BuildRecursiveWorld(x, z + chunkDimensions.z, nextrad));
        yield return null;

        BuildChunkColumn(x, z - chunkDimensions.z, meshEnabled: true);

        buildQueue.Enqueue(BuildRecursiveWorld(x, z - chunkDimensions.z, nextrad));
        yield return null;

        BuildChunkColumn(x + chunkDimensions.x, z, meshEnabled: true);
        buildQueue.Enqueue(BuildRecursiveWorld(x + chunkDimensions.x, z, nextrad));
        yield return null;

        BuildChunkColumn(x - chunkDimensions.x, z, meshEnabled: true);
        buildQueue.Enqueue(BuildRecursiveWorld(x - chunkDimensions.x, z, nextrad));
        yield return null;
    }
}
