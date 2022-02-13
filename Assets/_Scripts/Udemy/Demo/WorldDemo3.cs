using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace udemy
{
    public class WorldDemo3 : MonoBehaviour
    {
        public static Vector3Int world_dimesions = new Vector3Int(5, 5, 5);
        public static Vector3Int extra_world_dimesions = new Vector3Int(5, 5, 5);
        public static Vector3Int chunk_dimensions = new Vector3Int(10, 10, 10);
        
        public GameObject chunk_prefab;

        public GameObject main_camera;
        public GameObject fpc;
        public Slider loading_bar;

        #region fBM 2D
        public StrataSetting surface_setting;
        public static Strata surface_strata;

        public StrataSetting stone_setting;
        public static Strata stone_strata;

        public StrataSetting diamond_top_setting;
        public static Strata diamond_top_strata;

        public StrataSetting diamond_bottom_setting;
        public static Strata diamond_bottom_strata; 
        #endregion

        // fBM 3D
        public ClusterSetting cave_setting;
        public static Cluster cave_cluster;

        Dictionary<Vector3Int, Chunk2> chunks = new Dictionary<Vector3Int, Chunk2>();
        HashSet<Vector2Int> chunk_columns = new HashSet<Vector2Int>();

        // 為什麼要利用 Queue 來管理這些建造和隱藏 ChunkColumn 的任務？是為了強調任務的順序性，以避免後面的小任務比前面的大任務還要快結束嗎？
        Queue<IEnumerator> task_queue = new Queue<IEnumerator>();

        // 上一個紀錄點，玩家的位置
        [SerializeField] private Vector3Int last_position;

        // 可看到的距離
        int sight_unit = 5;

        // For updateWorld
        WaitForSeconds update_world_buffer = new WaitForSeconds(0.5f);

        void Start() 
        {
            loading_bar.maxValue = world_dimesions.x * world_dimesions.z;

            surface_strata = new Strata(surface_setting);
            stone_strata = new Strata(stone_setting);
            diamond_top_strata = new Strata(diamond_top_setting);
            diamond_bottom_strata = new Strata(diamond_bottom_setting);

            cave_cluster = new Cluster(cave_setting);

            StartCoroutine(buildWorld());
        }

        private void Update()
        {
            // 左鍵(0)：挖掘方塊；右鍵(1)：放置方塊
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                RaycastHit hit;
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

                if (Physics.Raycast(ray, out hit, 10f))
                {
                    Vector3 hit_block;

                    if (Input.GetMouseButtonDown(0))
                    {
                        hit_block = hit.point - hit.normal / 2.0f;
                    }
                    else
                    {
                        hit_block = hit.point + hit.normal / 2.0f;
                    }

                    //Debug.Log($"Block location: {hit_block}");
                    Chunk2 chunk = hit.collider.GetComponent<Chunk2>();
                    //Chunk chunk = hit.collider.gameObject.transform.parent.GetComponent<Chunk>();
                    int bx = (int)(Mathf.Round(hit_block.x) - chunk.location.x);
                    int by = (int)(Mathf.Round(hit_block.y) - chunk.location.y);
                    int bz = (int)(Mathf.Round(hit_block.z) - chunk.location.z);

                    int i = Utils.xyzToFlat(bx, by, bz, chunk_dimensions.x, chunk_dimensions.z);
                    chunk.block_types[i] = BlockType.AIR;

                    redrawChunk(chunk);

                    //var blockNeighbour = GetWorldNeighbour(new Vector3Int(bx, by, bz), Vector3Int.CeilToInt(chunk.location));
                    //chunk = chunks[blockNeighbour.Item2];

                    ////int i = bx + chunkDimensions.x * (by + chunkDimensions.z * bz);
                    //int i = ToFlat(blockNeighbour.Item1);

                    //if (Input.GetMouseButtonDown(0))
                    //{
                    //    // TODO: 教學中為了避免 health 為 -1 的方塊被刪除，因此加了這個判斷，但其實根本沒必要。health 從 NOCRACK(10) 開始往上加，本來就不可能加到 -1
                    //    if (MeshUtils.blockTypeHealth[(int)chunk.chunkData[i]] != -1)
                    //    {
                    //        // 第一次敲擊時觸發，一段時間後檢查是否已被敲掉，否則修復自己 health 恢復成 NOCRACK
                    //        if (chunk.healthData[i] == MeshUtils.BlockType.NOCRACK)
                    //        {
                    //            StartCoroutine(HealBlock(c: chunk, blockIndex: i));
                    //        }

                    //        chunk.healthData[i]++;

                    //        if (chunk.healthData[i] == MeshUtils.BlockType.NOCRACK + MeshUtils.blockTypeHealth[(int)chunk.chunkData[i]])
                    //        {
                    //            chunk.chunkData[i] = MeshUtils.BlockType.AIR;
                    //            chunk.healthData[i] = MeshUtils.BlockType.NOCRACK;

                    //            // 上方方塊是否掉落檢查
                    //            Vector3Int nBlock = FromFlat(i);
                    //            var neghbourBlock = GetWorldNeighbour(new Vector3Int(nBlock.x, nBlock.y + 1, nBlock.z), Vector3Int.CeilToInt(chunk.location));
                    //            Vector3Int block = neghbourBlock.Item1;
                    //            int neighboutBlockIndex = ToFlat(block);
                    //            Chunk neighbourChunk = chunks[neghbourBlock.Item2];
                    //            StartCoroutine(Drop(neighbourChunk, neighboutBlockIndex));
                    //        }
                    //    }
                    //}
                    //else
                    //{
                    //    chunk.chunkData[i] = buildType;
                    //    thisChunk.healthData[i] = MeshUtils.BlockType.NOCRACK;

                    //    // 方塊是否掉落檢查
                    //    StartCoroutine(Drop(thisChunk, i));
                    //}

                    //RedrawChunk(thisChunk);
                }
            }
        }

        IEnumerator buildWorld()
        {
            int x, z, column_z;

            for (z = 0; z < world_dimesions.z; z++)
            {
                column_z = z * chunk_dimensions.z;

                for (x = 0; x < world_dimesions.x; x++)
                {
                    // x, z: Chunk column 的索引值
                    // x * chunk_dimensions.x, column_z: Chunk column 的實際座標(和索引值差了 chunk_dimensions.x 倍)
                    buildChunkColumn(x * chunk_dimensions.x, column_z);
                    loading_bar.value++;
                    yield return null;
                }
            }

            main_camera.SetActive(false);

            // Place the player in the center of map
            int xpos = chunk_dimensions.x * world_dimesions.x / 2;
            int zpos = chunk_dimensions.z * world_dimesions.z / 2;
            int ypos = (int)surface_strata.fBM(xpos, zpos) + 10;

            fpc.transform.position = new Vector3(xpos, ypos, zpos);
            fpc.SetActive(true);
            last_position = new Vector3Int(xpos, ypos, zpos);
            loading_bar.gameObject.SetActive(false);

            // 啟動 taskCoordinator，依序執行被分派的任務
            StartCoroutine(taskCoordinator());

            // NOTE: 暫且關閉此功能，以利開發其他機制
            // 將 IEnumerator 添加到 buildQueue 當中
            //StartCoroutine(updateWorld());

            StartCoroutine(buildExtraWorld());
        }

        IEnumerator buildExtraWorld()
        {
            int z_start = world_dimesions.z;
            int z_end = world_dimesions.z + extra_world_dimesions.z;
            int x_start = world_dimesions.x;
            int x_end = world_dimesions.x + extra_world_dimesions.x;

            for (int z = z_start; z < z_end; z++)
            {
                for (int x = 0; x < x_end; x++)
                {
                    buildChunkColumn(chunk_dimensions.x * x, chunk_dimensions.z * z, visiable: true);
                    yield return null;
                }
            }

            for (int z = 0; z < z_end; z++)
            {
                for (int x = x_start; x < x_end; x++)
                {
                    buildChunkColumn(chunk_dimensions.x * x, chunk_dimensions.z * z, visiable: true);
                    yield return null;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="col_x">ChunkColumn 的 X 座標</param>
        /// <param name="col_z">ChunkColumn 的 Z 座標</param>
        void buildChunkColumn(int col_x, int col_z, bool visiable = true)
        {
            // 若是已建立過的 ChunkColumn，則再次使其呈現即可
            if (chunk_columns.Contains(new Vector2Int(col_x, col_z)))
            {
                displayChunkColumn(col_x, col_z, visiable: visiable);
            }

            // 若沒建立過位於 (col_x, col_z) 的 ChunkColumn，則產生並加入管理
            else
            {
                GameObject chunk_obj;
                Vector3Int location;
                Chunk2 chunk;

                // 依序建立同一個 ChunkColumn 裡面的 Chunk
                for (int y = 0; y < world_dimesions.y; y++)
                {
                    // y: Chunk 的索引值
                    // y * chunk_dimensions.y: Chunk 的實際座標(和索引值差了 chunk_dimensions.y 倍)
                    location = new Vector3Int(col_x, y * chunk_dimensions.y, col_z);

                    // 產生 Chunk 物件
                    chunk_obj = Instantiate(chunk_prefab);
                    chunk_obj.name = $"Chunk_{location.x}_{location.y}_{location.z}";

                    chunk = chunk_obj.GetComponent<Chunk2>();
                    chunk.init(dimensions: chunk_dimensions, location: location);
                    chunk.build();
                    chunk.mesh_renderer.enabled = visiable;

                    chunks.Add(location, chunk);
                }

                chunk_columns.Add(new Vector2Int(col_x, col_z));
            }            
        }

        /// <summary>
        /// 依序執行 task_queue 當中的任務
        /// Coordinator: 協調員
        /// TODO: 任務優先順序應為：1. enable 已建構的 ChunkColumn 2. 生成新的 ChunkColumn 3. 隱藏遠方 ChunkColumn
        /// TODO: 維護鄰近 ChunkColumn 座標列表，若要 enable 或要新生成的 ChunkColumn 又已不在清單中，或許可以跳出 Coroutine 以避免無效作業
        /// </summary>
        /// <returns></returns>
        IEnumerator taskCoordinator()
        {
            while (true)
            {
                while (task_queue.Count > 0)
                {
                    // 依序從 task_queue 當中取出任務來執行
                    yield return StartCoroutine(task_queue.Dequeue());
                }

                yield return null;
            }
        }

        IEnumerator updateWorld()
        {
            int posx, posz;

            while (true)
            {
                // 當玩家移動距離離上一次世界生成點 lastBuildPosition 大過一個 chunk 的距離時
                if ((last_position - fpc.transform.position).magnitude > chunk_dimensions.x)
                {
                    last_position = Vector3Int.CeilToInt(fpc.transform.position);
                    posx = (int)(fpc.transform.position.x / chunk_dimensions.x) * chunk_dimensions.x;
                    posz = (int)(fpc.transform.position.z / chunk_dimensions.z) * chunk_dimensions.z;
                    
                    task_queue.Enqueue(buildColumnRecursive(posx, posz, sight_unit));
                    task_queue.Enqueue(hideColumns(posx, posz));
                }

                yield return update_world_buffer;
            }
        }

        /// <summary>
        /// 以 (chunk_x, chunk_z) 為圓心，向四個方向建立新的 ChunkColumn
        /// </summary>
        /// <param name="col_x">Chunk column 的 X 座標</param>
        /// <param name="col_z">Chunk column 的 Z 座標</param>
        /// <param name="radius">遞迴次數</param>
        /// <returns></returns>
        IEnumerator buildColumnRecursive(int col_x, int col_z, int radius)
        {
            int next_radius = radius - 1, next_col;

            // 若 radius 為 1，則下方遞迴呼叫的 radius 即為 0，將不再執行
            if (next_radius < 0)
            {
                yield break;
            }

            next_col = col_z + chunk_dimensions.z;
            buildChunkColumn(col_x, next_col);

            // Next chunk z position: z + chunkDimensions.z
            task_queue.Enqueue(buildColumnRecursive(col_x, next_col, next_radius));
            yield return null;

            next_col = col_z - chunk_dimensions.z;
            buildChunkColumn(col_x, next_col);
            task_queue.Enqueue(buildColumnRecursive(col_x, next_col, next_radius));
            yield return null;

            next_col = col_x + chunk_dimensions.x;
            buildChunkColumn(next_col, col_z);
            task_queue.Enqueue(buildColumnRecursive(next_col, col_z, next_radius));
            yield return null;

            next_col = col_x - chunk_dimensions.x;
            buildChunkColumn(next_col, col_z);
            task_queue.Enqueue(buildColumnRecursive(next_col, col_z, next_radius));
            yield return null;
        }

        /// <summary>
        /// 隱藏距離玩家位置過遠的 ChunkColumn 而非刪除，因為重新建構會花太多時間與資源
        /// </summary>
        /// <param name="x">玩家位置 X 座標</param>
        /// <param name="z">玩家位置 Z 座標</param>
        /// <returns></returns>
        IEnumerator hideColumns(int x, int z)
        {
            Vector2Int fpc_position = new Vector2Int(x, z);
            float sight_distance = sight_unit * chunk_dimensions.x;

            foreach (Vector2Int column_position in chunk_columns)
            {
                // 若 ChunkColumn 距離玩家過遠
                if ((column_position - fpc_position).magnitude >= sight_distance)
                {
                    // 隱藏 ChunkColumn，因為太遠看不到 
                    // 實際上是 Z 值，但 Vector2Int 本身屬性為 y
                    displayChunkColumn(column_position.x, column_position.y, visiable: false);
                }
            }

            yield return null;
        }

        void displayChunkColumn(int col_x, int col_z, bool visiable = true)
        {
            Vector3Int pos;

            for (int y = 0; y < world_dimesions.y; y++)
            {
                // Chunk position
                pos = new Vector3Int(col_x, y * chunk_dimensions.y, col_z);

                if (chunks.ContainsKey(pos))
                {
                    chunks[pos].mesh_renderer.enabled = visiable;
                }
            }
        }

        void redrawChunk(Chunk2 chunk)
        {
            DestroyImmediate(chunk.GetComponent<MeshFilter>());
            DestroyImmediate(chunk.GetComponent<MeshRenderer>());
            DestroyImmediate(chunk.GetComponent<Collider>());
            chunk.build();
        }
    }
}
