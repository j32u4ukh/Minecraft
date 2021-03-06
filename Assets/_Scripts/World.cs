using System;
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
    public bool laodFromFile = false;
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

    public static PerlinSettings treeSettings;
    public Perlin3DGrapher tree;

    public static PerlinSettings biomeSettings;
    public Perlin3DGrapher biome;

    public HashSet<Vector3Int> chunkChecker = new HashSet<Vector3Int>();
    public HashSet<Vector2Int> chunkColumns = new HashSet<Vector2Int>();
    public Dictionary<Vector3Int, Chunk> chunks = new Dictionary<Vector3Int, Chunk>();

    // 前一次世界建造時，玩家所在位置
    Vector3Int lastBuildPosition;
    int drawRadius = 5;

    Queue<IEnumerator> buildQueue = new Queue<IEnumerator>();
    MeshUtils.BlockType buildType = MeshUtils.BlockType.DIRT;

    // For UpdateWorld
    WaitForSeconds wfs = new WaitForSeconds(0.5f);

    // For HealBlock
    WaitForSeconds threeSeconds = new WaitForSeconds(3.0f);

    // For (failling block)
    WaitForSeconds dropDelay = new WaitForSeconds(0.1f);


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

        treeSettings = new PerlinSettings(tree.heightScale,
                                          tree.scale,
                                          tree.octaves,
                                          tree.heightOffset,
                                          tree.DrawCutOff);

        biomeSettings = new PerlinSettings(biome.heightScale,
                                           biome.scale,
                                           biome.octaves,
                                           biome.heightOffset,
                                           biome.DrawCutOff);

        if (laodFromFile)
        {
            StartCoroutine(LoadWorldFromFile());
        }
        else
        {
            StartCoroutine(BuildWorld());
        }
    }

    // Update is called once per frame
    void Update()
    {
        // 左鍵(0)：挖掘方塊；右鍵(1)：放置方塊
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
                //Chunk thisChunk = hit.collider.gameObject.GetComponent<Chunk>();
                Chunk thisChunk = hit.collider.gameObject.transform.parent.GetComponent<Chunk>();
                int bx = (int)(Mathf.Round(hitBlock.x) - thisChunk.location.x);
                int by = (int)(Mathf.Round(hitBlock.y) - thisChunk.location.y);
                int bz = (int)(Mathf.Round(hitBlock.z) - thisChunk.location.z);

                var blockNeighbour = GetWorldNeighbour(new Vector3Int(bx, by, bz), Vector3Int.CeilToInt(thisChunk.location));
                thisChunk = chunks[blockNeighbour.Item2];

                //int i = bx + chunkDimensions.x * (by + chunkDimensions.z * bz);
                int i = ToFlat(blockNeighbour.Item1);
                    
                if (Input.GetMouseButtonDown(0))
                {
                    // 教學中為了避免 health 為 -1 的方塊被刪除，因此加了這個判斷，但其實根本沒必要。health 從 NOCRACK(10) 開始往上加，本來就不可能加到 -1
                    if (MeshUtils.blockTypeHealth[(int)thisChunk.chunkData[i]] != -1)
                    {
                        // 第一次敲擊時觸發，一段時間後檢查是否已被敲掉，否則修復自己 health 恢復成 NOCRACK
                        if (thisChunk.healthData[i] == MeshUtils.BlockType.NOCRACK)
                        {
                            StartCoroutine(HealBlock(c: thisChunk, blockIndex: i));
                        }

                        thisChunk.healthData[i]++;

                        if (thisChunk.healthData[i] == MeshUtils.BlockType.NOCRACK + MeshUtils.blockTypeHealth[(int)thisChunk.chunkData[i]])
                        {
                            thisChunk.chunkData[i] = MeshUtils.BlockType.AIR;
                            thisChunk.healthData[i] = MeshUtils.BlockType.NOCRACK;

                            // 上方方塊是否掉落檢查
                            Vector3Int nBlock = FromFlat(i);
                            var neghbourBlock = GetWorldNeighbour(new Vector3Int(nBlock.x, nBlock.y + 1, nBlock.z), Vector3Int.CeilToInt(thisChunk.location));
                            Vector3Int block = neghbourBlock.Item1;
                            int neighboutBlockIndex = ToFlat(block);
                            Chunk neighbourChunk = chunks[neghbourBlock.Item2];
                            StartCoroutine(Drop(neighbourChunk, neighboutBlockIndex));
                        }
                    }
                }
                else
                {
                    thisChunk.chunkData[i] = buildType;
                    thisChunk.healthData[i] = MeshUtils.BlockType.NOCRACK;

                    // 方塊是否掉落檢查
                    StartCoroutine(Drop(thisChunk, i));
                }

                RedrawChunk(thisChunk);
            }
        }
    }

    public static Vector3Int FromFlat(int i)
    {
        return new Vector3Int(i % chunkDimensions.x, (i / chunkDimensions.x) % chunkDimensions.y, i / (chunkDimensions.x * chunkDimensions.y));
    }

    public static int ToFlat(Vector3Int v)
    {
        return v.x + chunkDimensions.x * (v.y + chunkDimensions.z * v.z);
    }

    /// <summary>
    /// 處理 Chunk 邊界對 Block 索引值的處理，當超出當前 Chunk 時，指向下一個 Chunk 並修正 Block 索引值
    /// 應該先返回 chunk index 再返回 updated block index，比較合邏輯。我的優化版已修正。
    /// </summary>
    /// <param name="blockIndex">updated block index</param>
    /// <param name="chunkIndex">chunk index</param>
    /// <returns>(updated block index, chunk index)</returns>
    public Tuple<Vector3Int, Vector3Int> GetWorldNeighbour(Vector3Int blockIndex, Vector3Int chunkIndex)
    {
        Chunk thisChunk = chunks[chunkIndex];
        int bx = blockIndex.x;
        int by = blockIndex.y;
        int bz = blockIndex.z;

        Vector3Int neighbour = chunkIndex;

        if (bx == chunkDimensions.x)
        {
            neighbour = new Vector3Int((int)thisChunk.location.x + chunkDimensions.x, (int)thisChunk.location.y, (int)thisChunk.location.z);
            bx = 0;
        }
        else if (bx == -1)
        {
            neighbour = new Vector3Int((int)thisChunk.location.x - chunkDimensions.x, (int)thisChunk.location.y, (int)thisChunk.location.z);
            bx = chunkDimensions.x - 1;
        }
        else if (by == chunkDimensions.y)
        {
            neighbour = new Vector3Int((int)thisChunk.location.x, (int)thisChunk.location.y + chunkDimensions.y, (int)thisChunk.location.z);
            by = 0;
        }
        else if (by == -1)
        {
            neighbour = new Vector3Int((int)thisChunk.location.x, (int)thisChunk.location.y - chunkDimensions.y, (int)thisChunk.location.z);
            by = chunkDimensions.y - 1;
        }
        else if (bz == chunkDimensions.z)
        {
            neighbour = new Vector3Int((int)thisChunk.location.x, (int)thisChunk.location.y, (int)thisChunk.location.z + chunkDimensions.z);
            bz = 0;
        }
        else if (bz == -1)
        {
            neighbour = new Vector3Int((int)thisChunk.location.x, (int)thisChunk.location.y, (int)thisChunk.location.z - chunkDimensions.z);
            bz = chunkDimensions.z - 1;
        }

        return new Tuple<Vector3Int, Vector3Int>(new Vector3Int(bx, by, bz), neighbour);
    }

    // 一段時間後檢查是否已被敲掉，否則修復自己 health 恢復成 NOCRACK
    public IEnumerator HealBlock(Chunk c, int blockIndex)
    {
        yield return threeSeconds;

        if(c.chunkData[blockIndex] != MeshUtils.BlockType.AIR)
        {
            c.healthData[blockIndex] = MeshUtils.BlockType.NOCRACK;
            RedrawChunk(c);
        }
    }

    // 一段時間後檢查是否已被敲掉，否則修復自己 health 恢復成 NOCRACK
    public IEnumerator Drop(Chunk c, int blockIndex, int spread = 3)
    {
        // 檢查當前方塊是否會掉落
        if(!MeshUtils.canDrop.Contains(c.chunkData[blockIndex]))
        {
            yield break;
        }

        yield return dropDelay;

        while (true)
        {
            // Current block: Sand
            Vector3Int thisBlock = FromFlat(blockIndex);

            // Block below current block
            var neighbourBlock = GetWorldNeighbour(new Vector3Int(thisBlock.x, thisBlock.y - 1, thisBlock.z), Vector3Int.CeilToInt(c.location));

            Vector3Int block = neighbourBlock.Item1;
            int neighbourBlockIndex = ToFlat(block);
            Chunk neighbourChunk = chunks[neighbourBlock.Item2];

            // 檢查下方是否有掉落空間
            if(neighbourChunk != null && neighbourChunk.chunkData[neighbourBlockIndex] == MeshUtils.BlockType.AIR)
            {
                neighbourChunk.chunkData[neighbourBlockIndex] = c.chunkData[blockIndex];
                neighbourChunk.healthData[neighbourBlockIndex] = MeshUtils.BlockType.NOCRACK;

                // 考慮當前方塊的上方一格是否會觸發掉落機制
                var nBlockAbove = GetWorldNeighbour(new Vector3Int(thisBlock.x, thisBlock.y + 1, thisBlock.z), Vector3Int.CeilToInt(c.location));
                Vector3Int blockAbove = nBlockAbove.Item1;
                int nBlockAboveIndex = ToFlat(blockAbove);
                Chunk nChunkAbove = chunks[nBlockAbove.Item2];

                c.chunkData[blockIndex] = MeshUtils.BlockType.AIR;
                c.healthData[blockIndex] = MeshUtils.BlockType.NOCRACK;

                StartCoroutine(Drop(c: nChunkAbove, blockIndex: nBlockAboveIndex));

                yield return dropDelay;
                RedrawChunk(c);

                if(neighbourChunk != c)
                {
                    RedrawChunk(neighbourChunk);
                }

                c = neighbourChunk;
                blockIndex = neighbourBlockIndex;
            }
            else if (MeshUtils.canFlow.Contains(c.chunkData[blockIndex]))
            {
                FlowIntoNeighbour(thisBlock, Vector3Int.CeilToInt(c.location), Vector3Int.forward, spread - 1);
                FlowIntoNeighbour(thisBlock, Vector3Int.CeilToInt(c.location), Vector3Int.back, spread - 1);
                FlowIntoNeighbour(thisBlock, Vector3Int.CeilToInt(c.location), Vector3Int.left, spread - 1);
                FlowIntoNeighbour(thisBlock, Vector3Int.CeilToInt(c.location), Vector3Int.right, spread - 1);
                yield break;
            }
            else
            {
                yield break;
            }
        }
    }

    /// <summary>
    /// 讓水等物件流向四周
    /// </summary>
    /// <param name="blockPosition"></param>
    /// <param name="chunkPosition"></param>
    /// <param name="neighbourDirection"></param>
    /// <param name="spread">流動距離(程度)</param>
    public void FlowIntoNeighbour(Vector3Int blockPosition, Vector3Int chunkPosition, Vector3Int neighbourDirection, int spread)
    {
        spread--;

        if(spread <= 0)
        {
            return;
        }

        Vector3Int neighbourPosition = blockPosition + neighbourDirection;
        var neighbourBlock = GetWorldNeighbour(neighbourPosition, chunkPosition);
        Vector3Int block = neighbourBlock.Item1;
        int neighbourBlockIndex = ToFlat(block);
        Chunk neighbourChunk = chunks[neighbourBlock.Item2];

        if(neighbourChunk == null)
        {
            return;
        }

        if(neighbourChunk.chunkData[neighbourBlockIndex] == MeshUtils.BlockType.AIR)
        {
            neighbourChunk.chunkData[neighbourBlockIndex] = chunks[chunkPosition].chunkData[ToFlat(blockPosition)];
            neighbourChunk.healthData[neighbourBlockIndex] = MeshUtils.BlockType.NOCRACK;
            RedrawChunk(neighbourChunk);
            StartCoroutine(Drop(c: neighbourChunk, blockIndex: neighbourBlockIndex, spread: spread--));
        }
    }

    void RedrawChunk(Chunk c)
    {
        DestroyImmediate(c.GetComponent<MeshFilter>());
        DestroyImmediate(c.GetComponent<MeshRenderer>());
        DestroyImmediate(c.GetComponent<Collider>());
        c.CreateChunk(chunkDimensions, c.location, false);
    }

    public void SetBuildType(int type)
    {
        buildType = (MeshUtils.BlockType)type;
    }

    public void SetBlockType(MeshUtils.BlockType type)
     {
        buildType = type;
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
            Vector3Int position = new Vector3Int(x, y * chunkDimensions.y, z);

            if (!chunkChecker.Contains(position))
            {
                GameObject chunk = Instantiate(chunkPrefab);
                chunk.name = $"Chunk_{position.x}_{position.y}_{position.z}";
                Chunk c = chunk.GetComponent<Chunk>();
                c.CreateChunk(dimensions: chunkDimensions, position: position);
                chunkChecker.Add(position);
                chunks.Add(position, c);
            }

            chunks[position].meshRendererSolid.enabled = meshEnabled;
            chunks[position].meshRendererFluid.enabled = meshEnabled;
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
                chunks[pos].meshRendererSolid.enabled = false;
                chunks[pos].meshRendererFluid.enabled = false;
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

    public void SaveWorld()
    {
        FileSaver.Save(this);
    }
    
    IEnumerator LoadWorldFromFile()
    {
        WorldData wd = FileSaver.Load();

        if (wd == null)
        {
            StartCoroutine(BuildWorld());
            yield break;
        }

        chunkChecker.Clear();

        for (int i = 0; i < wd.chunkCheckerValues.Length; i += 3)
        {
            chunkChecker.Add(new Vector3Int(wd.chunkCheckerValues[i],
                                            wd.chunkCheckerValues[i + 1],
                                            wd.chunkCheckerValues[i + 2]));
        }

        chunkColumns.Clear();

        for (int i = 0; i < wd.chunkColumnsValues.Length; i += 2)
        {
            chunkColumns.Add(new Vector2Int(wd.chunkColumnsValues[i],
                                            wd.chunkColumnsValues[i + 1]));
        }

        //chunks.Clear();
        int index = 0;
        int vIndex = 0;
        loadingBar.maxValue = chunkChecker.Count;

        foreach (Vector3Int chunkPos in chunkChecker)
        {
            GameObject chunk = Instantiate(chunkPrefab);
            chunk.name = $"Chunk_{chunkPos.x}_{chunkPos.y}_{chunkPos.z}";
            Chunk c = chunk.GetComponent<Chunk>();

            int blockCount = chunkDimensions.x * chunkDimensions.y * chunkDimensions.z;
            c.chunkData = new MeshUtils.BlockType[blockCount];
            c.healthData = new MeshUtils.BlockType[blockCount];

            for(int i = 0; i < blockCount; i++)
            {
                c.chunkData[i] = (MeshUtils.BlockType)wd.allChunkData[index];
                c.healthData[i] = MeshUtils.BlockType.NOCRACK;
                index++;
            }

            c.CreateChunk(chunkDimensions, chunkPos, false);
            chunks.Add(chunkPos, c);
            RedrawChunk(c);
            c.meshRendererSolid.enabled = wd.chunkVisibility[vIndex];
            c.meshRendererFluid.enabled = wd.chunkVisibility[vIndex];
            vIndex++;

            loadingBar.value++;
            yield return null;
        }

        fpc.transform.position = new Vector3(wd.fpcX, wd.fpcY, wd.fpcZ);
        Debug.Log($"Load WorldData fpc: ({fpc.transform.position})");
        mCamera.SetActive(false);
        loadingBar.gameObject.SetActive(false);
        fpc.SetActive(true);
        lastBuildPosition = Vector3Int.CeilToInt(fpc.transform.position);

        // 依序執行 buildQueue 當中的 IEnumerator
        StartCoroutine(BuildCoordinator());

        // 將 IEnumerator 添加到 buildQueue 當中
        StartCoroutine(UpdateWorld());
    }
}
