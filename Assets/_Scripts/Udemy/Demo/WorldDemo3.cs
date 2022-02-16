using System;
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
        public bool load_file = false;
        public GameObject chunk_prefab;

        public GameObject main_camera;
        public Player player;
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

        public Dictionary<Vector3Int, Chunk2> chunks = new Dictionary<Vector3Int, Chunk2>();
        HashSet<Vector2Int> chunk_columns = new HashSet<Vector2Int>();

        // 為什麼要利用 Queue 來管理這些建造和隱藏 ChunkColumn 的任務？是為了強調任務的順序性，以避免後面的小任務比前面的大任務還要快結束嗎？
        Queue<IEnumerator> task_queue = new Queue<IEnumerator>();

        // 上一個紀錄點，玩家的位置
        [SerializeField] private Vector3Int last_position;

        // 可看到的距離
        int sight_unit = 5;

        // For updateWorld
        WaitForSeconds update_world_buffer = new WaitForSeconds(0.5f);

        // For (falling block)
        WaitForSeconds falling_buffer = new WaitForSeconds(0.1f);

        void Start()
        {
            surface_strata = new Strata(surface_setting);
            stone_strata = new Strata(stone_setting);
            diamond_top_strata = new Strata(diamond_top_setting);
            diamond_bottom_strata = new Strata(diamond_bottom_setting);

            cave_cluster = new Cluster(cave_setting);

            if (load_file)
            {
                StartCoroutine(loadWorldFromFile());
            }
            else
            {
                StartCoroutine(buildWorld());
            }
        }

        private void Update()
        {
            // TODO: 移到 Player 當中管理，利用事件通知 World 哪些方塊被移除，哪些方塊又被新增
            // 左鍵(0)：挖掘方塊；右鍵(1)：放置方塊
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                RaycastHit hit;
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

                if (Physics.Raycast(ray, out hit, 10f))
                {
                    Chunk2 chunk = hit.collider.GetComponent<Chunk2>();
                    //Chunk chunk = hit.collider.gameObject.transform.parent.GetComponent<Chunk>();
                    Vector3 hit_block;

                    // 左鍵(0)：挖掘方塊
                    if (Input.GetMouseButtonDown(0))
                    {
                        hit_block = hit.point - hit.normal / 2.0f;
                    }

                    // 右鍵(1)：放置方塊
                    else
                    {
                        hit_block = hit.point + hit.normal / 2.0f;
                    }

                    // Debug.Log($"Block location: {hit_block}");
                    int bx = (int)(Mathf.Round(hit_block.x) - chunk.location.x);
                    int by = (int)(Mathf.Round(hit_block.y) - chunk.location.y);
                    int bz = (int)(Mathf.Round(hit_block.z) - chunk.location.z);
                    int i;

                    // 左鍵(0)：挖掘方塊
                    if (Input.GetMouseButtonDown(0))
                    {
                        i = Utils.xyzToFlat(bx, by, bz, chunk_dimensions.x, chunk_dimensions.z);

                        // 累加破壞程度(若破壞程度與方塊強度相當，才會真的破壞掉)
                        if(chunk.crackBlock(index: i))
                        {
                            //Tuple<Vector3Int, Vector3Int> chunk_block_location = chunk.getChunkBlockLocation(bx, by, bz);

                            // 考慮當前方塊的上方一格是否會觸發掉落機制
                            dropBlockAbove(chunk: chunk, block_position: new Vector3Int(bx, by, bz));
                        }
                    }

                    // 右鍵(1)：放置方塊
                    else
                    {
                        // TODO: 考慮 世界 的大小，取得的 chunk 不一定存在於 chunks 當中
                        Tuple<Vector3Int, Vector3Int> chunk_block_location = chunk.getChunkBlockLocation(bx, by, bz);

                        if (chunks.ContainsKey(chunk_block_location.Item1))
                        {
                            chunk = chunks[chunk_block_location.Item1];
                            i = Utils.vector3IntToFlat(chunk_block_location.Item2, chunk_dimensions.x, chunk_dimensions.z);

                            chunk.placeBlock(index: i, block_type: player.getBlockType());

                            StartCoroutine(dropBlock(chunk: chunk, block_index: i));
                        }
                    }

                    // 當 新增 或 破壞 方塊後，重新繪製 Chunk
                    chunk.rebuild();

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

            if (Input.GetKey(KeyCode.RightControl))
            {
                if (Input.GetKeyDown(KeyCode.RightAlt))
                {
                    saveWorldToFile();
                }
            }
        }

        IEnumerator buildWorld()
        {
            loading_bar.maxValue = world_dimesions.x * world_dimesions.z;
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

            player.transform.position = new Vector3(xpos, ypos, zpos);
            player.gameObject.SetActive(true);
            last_position = new Vector3Int(xpos, ypos, zpos);
            loading_bar.gameObject.SetActive(false);

            // 啟動 taskCoordinator，依序執行被分派的任務
            StartCoroutine(taskCoordinator());

            // NOTE: 暫且關閉此功能，以利開發其他機制
            // 將 IEnumerator 添加到 buildQueue 當中
            //StartCoroutine(updateWorld());

            StartCoroutine(buildExtraWorld());
        }

        IEnumerator buildExtraWorld(bool visiable = false)
        {
            int z_start = world_dimesions.z;
            int z_end = world_dimesions.z + extra_world_dimesions.z;
            int x_start = world_dimesions.x;
            int x_end = world_dimesions.x + extra_world_dimesions.x;

            for (int z = z_start; z < z_end; z++)
            {
                for (int x = 0; x < x_end; x++)
                {
                    buildChunkColumn(chunk_dimensions.x * x, chunk_dimensions.z * z, visiable: visiable);
                    yield return null;
                }
            }

            for (int z = 0; z < z_end; z++)
            {
                for (int x = x_start; x < x_end; x++)
                {
                    buildChunkColumn(chunk_dimensions.x * x, chunk_dimensions.z * z, visiable: visiable);
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
                setChunkColumnVisiable(col_x, col_z, visiable: visiable);
            }

            // 若沒建立過位於 (col_x, col_z) 的 ChunkColumn，則產生並加入管理
            else
            {
                GameObject chunk_obj;
                Chunk2 chunk;

                foreach(Vector3Int location in iterChunkColumnLocation(col_x, col_z))
                {
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
                if ((last_position - player.transform.position).magnitude > chunk_dimensions.x)
                {
                    last_position = Vector3Int.CeilToInt(player.transform.position);
                    posx = (int)(player.transform.position.x / chunk_dimensions.x) * chunk_dimensions.x;
                    posz = (int)(player.transform.position.z / chunk_dimensions.z) * chunk_dimensions.z;
                    
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
            Vector2Int player_position = new Vector2Int(x, z);
            float sight_distance = sight_unit * chunk_dimensions.x;

            foreach (Vector2Int column_position in chunk_columns)
            {
                // 若 ChunkColumn 距離玩家過遠
                if ((column_position - player_position).magnitude >= sight_distance)
                {
                    // 隱藏 ChunkColumn，因為太遠看不到 
                    // 實際上是 Z 值，但 Vector2Int 本身屬性為 y
                    setChunkColumnVisiable(column_position.x, column_position.y, visiable: false);
                }
            }

            yield return null;
        }

        void setChunkColumnVisiable(int col_x, int col_z, bool visiable = true)
        {
            IEnumerable<Chunk2> chunk_column = iterChunkColumn(col_x, col_z);

            foreach(Chunk2 chunk in chunk_column)
            {
                chunk.setVisiable(visiable);
            }
        }

        IEnumerator dropBlock(Chunk2 chunk, int block_index, int spread = 2)
        {
            BlockType block_type = chunk.getBlockType(block_index);

            if (!MeshUtils.canDrop(block_type: block_type))
            {
                yield break;
            }

            yield return falling_buffer;

            Vector3Int block_position;
            Tuple<Vector3Int, Vector3Int> location_below;
            Chunk2 chunk_below;
            int block_below_index;

            while (true)
            {
                block_position = Utils.flatToVector3Int(i: block_index, 
                                                        width: chunk_dimensions.x, 
                                                        height: chunk_dimensions.y);

                // 取得當前方塊的下方方塊位置(有可能跨到下一個 Chunk，使用 getChunkBlockLocation 取得正確的 Chunk 和 Block 的索引值)
                location_below = chunk.getChunkBlockLocation(bx: block_position.x, 
                                                             by: block_position.y - 1, 
                                                             bz: block_position.z);

                if (chunks.ContainsKey(location_below.Item1))
                {
                    chunk_below = chunks[location_below.Item1];
                    block_below_index = Utils.vector3IntToFlat(v: location_below.Item2,
                                                           width: chunk_dimensions.x,
                                                           depth: chunk_dimensions.z);
                }
                else
                {
                    chunk_below = null;
                    block_below_index = -1;
                }

                // NOTE: 當方塊掉往的 Chunk 尚未被建構，可能發生 chunk_below == null
                // 檢查下方是否有掉落空間
                if (chunk_below != null && chunk_below.getBlockType(block_below_index).Equals(BlockType.AIR))
                {
                    // 更新下方方塊
                    chunk_below.setBlockType(index: block_below_index, block_type: block_type);
                    chunk_below.setCrackState(index: block_below_index);

                    // 更新當前方塊
                    chunk.setBlockType(index: block_index, block_type: BlockType.AIR);
                    chunk.setCrackState(index: block_index);

                    // 考慮當前方塊的上方一格是否會觸發掉落機制
                    dropBlockAbove(chunk: chunk, block_position: block_position);

                    yield return falling_buffer;

                    chunk.rebuild();

                    if(chunk_below != chunk)
                    {
                        chunk_below.rebuild();
                    }

                    // 指向落下後的方塊
                    block_index = block_below_index;
                    chunk = chunk_below;
                }
                else if (MeshUtils.canSpread(block_type: block_type))
                {
                    spreadBlock(chunk, block_position, Vector3Int.forward, spread);
                    spreadBlock(chunk, block_position, Vector3Int.back, spread);
                    spreadBlock(chunk, block_position, Vector3Int.left, spread);
                    spreadBlock(chunk, block_position, Vector3Int.right, spread);
                    yield break;
                }
                else
                {
                    yield break;
                }
            }
        }

        void spreadBlock(Chunk2 chunk, Vector3Int block_position, Vector3Int direction, int spread = 2)
        {
            spread--;

            if (spread < 0)
            {
                return;
            }

            Tuple<Vector3Int, Vector3Int> location = chunk.getChunkBlockLocation(block_position + direction);

            if (!chunks.ContainsKey(location.Item1))
            {
                return;
            }

            Chunk2 neighbor_chunk = chunks[location.Item1];
            int block_neighbor_index = Utils.vector3IntToFlat(location.Item2, width: chunk_dimensions.x, depth: chunk_dimensions.z);

            if (neighbor_chunk.getBlockType(block_neighbor_index).Equals(BlockType.AIR))
            {
                int block_index = Utils.vector3IntToFlat(block_position, width: chunk_dimensions.x, depth: chunk_dimensions.z);
                neighbor_chunk.setBlockType(index: block_neighbor_index, chunk.getBlockType(block_index));
                neighbor_chunk.setCrackState(index: block_neighbor_index);
                neighbor_chunk.rebuild();

                StartCoroutine(dropBlock(chunk: neighbor_chunk, block_index: block_neighbor_index, spread: spread));
            }
        }

        /// <summary>
        /// 考慮當前方塊的上方一格是否會觸發掉落機制
        /// </summary>
        /// <param name="chunk"></param>
        /// <param name="block_position"></param>
        void dropBlockAbove(Chunk2 chunk, Vector3Int block_position)
        {
            Tuple<Vector3Int, Vector3Int>  location_above = chunk.getChunkBlockLocation(bx: block_position.x,
                                                                                        by: block_position.y + 1,
                                                                                        bz: block_position.z);

            if (chunks.ContainsKey(location_above.Item1))
            {
                Chunk2 chunk_above = chunks[location_above.Item1];
                int block_above_index = Utils.vector3IntToFlat(v: location_above.Item2,
                                                               width: chunk_dimensions.x,
                                                               depth: chunk_dimensions.z);

                StartCoroutine(dropBlock(chunk_above, block_above_index));
            }
        }

        IEnumerable<Chunk2> iterChunkColumn(int col_x, int col_z)
        {
            IEnumerable<Vector3Int> locations = iterChunkColumnLocation(col_x, col_z);

            foreach (Vector3Int location in locations)
            {
                if (chunks.ContainsKey(location))
                {
                    yield return chunks[location];
                }
            }
        }

        IEnumerable<Vector3Int> iterChunkColumnLocation(int col_x, int col_z)
        {
            for (int y = 0; y < world_dimesions.y; y++)
            {
                // Chunk position
                yield return new Vector3Int(col_x, y * chunk_dimensions.y, col_z);
            }
        }

        void saveWorldToFile()
        {
            WorldData1 wd = new WorldData1();

            foreach (KeyValuePair<Vector3Int, Chunk2> item in chunks)
            {
                wd.setLocation(location: item.Key);
                wd.setVisibility(visible: item.Value.mesh_renderer.enabled);
                wd.setBlockTypes(block_types: item.Value.block_types);
            }

            wd.setPlayerPosition(position: player.transform.position);

            WorldRecorder1.save(wd);
        }

        // 目前讀取檔案來建構世界的過程中，沒有區分哪些是 Extra World，因此會全部都建構後才進入遊戲
        IEnumerator loadWorldFromFile()
        {
            WorldData1 wd = WorldRecorder1.load();

            if (wd == null)
            {
                StartCoroutine(buildWorld());
                yield break;
            }

            chunks.Clear();
            chunk_columns.Clear();

            Vector3Int location;
            GameObject obj;
            Chunk2 chunk;

            // NOTE: 若想初始化完玩家周圍後就進入遊戲，loading_bar.maxValue 的設置會是個問題
            loading_bar.maxValue = wd.getChunkNumber();
            var chunk_datas = wd.iterChunkDatas();

            while (chunk_datas.MoveNext())
            {
                var chunk_data = chunk_datas.Current;
                location = chunk_data.Item1;

                // NOTE: 若想初始化完玩家周圍後就進入遊戲，可以利用 location 和玩家位置，判斷是否需要現在就建置

                obj = Instantiate(chunk_prefab);
                obj.name = $"Chunk_{location.x}_{location.y}_{location.z}";
                chunk = obj.GetComponent<Chunk2>();

                chunk.block_types = chunk_data.Item2;
                chunk.crack_states = chunk_data.Item3;

                chunk.locate(dimensions: chunk_dimensions, location: location);
                chunk.build();
                chunk.mesh_renderer.enabled = chunk_data.Item4;

                chunk_columns.Add(new Vector2Int(location.x, location.z));
                chunks.Add(location, chunk);

                loading_bar.value++;
                yield return null;
            }

            player.transform.position = wd.getPlayerPosition();
            main_camera.SetActive(false);
            loading_bar.gameObject.SetActive(false);
            player.gameObject.SetActive(true);
            last_position = Vector3Int.CeilToInt(player.transform.position);

            // NOTE: 若想初始化完玩家周圍後就進入遊戲，這裡之前就可先進入，這之後建構剩餘的世界

            // 依序執行 buildQueue 當中的 IEnumerator
            StartCoroutine(taskCoordinator());

            // 將 IEnumerator 添加到 buildQueue 當中
            StartCoroutine(updateWorld());
        }
    }

    [Serializable]
    public class WorldData1
    {
        // 每個 chunk_location 分別有 3 個數值 (x, y, z)，共有 n_chunk * 3 個數值 
        //public int[] locations;
        public List<int> locations;

        // 每個 Chunk 分別有 chunk_dimensions.x * chunk_dimensions.y * chunk_dimensions.z 個數值
        // 共有 n_chunk * chunk_dimensions.x * chunk_dimensions.y * chunk_dimensions.z 個數值 
        public List<int> block_types;

        public List<bool> visibility;

        // 玩家位置
        public int player_x;
        public int player_y;
        public int player_z;

        public WorldData1()
        {
            locations = new List<int>();
            block_types = new List<int>();
            visibility = new List<bool>();
        }

        public int getChunkNumber()
        {
            return locations.Count / 3;
        }

        public void setLocation(Vector3Int location)
        {
            locations.Add(location.x);
            locations.Add(location.y);
            locations.Add(location.z);
        }

        public void setVisibility(bool visible)
        {
            visibility.Add(visible);
        }

        public void setBlockTypes(BlockType[] block_types)
        {
            foreach(BlockType block_type in block_types)
            {
                this.block_types.Add((int)block_type);
            }
        }

        public IEnumerator<Tuple<Vector3Int, BlockType[], CrackState[], bool>> iterChunkDatas()
        {
            int b, c, n_chunk = getChunkNumber(), n_block = WorldDemo3.chunk_dimensions.x * WorldDemo3.chunk_dimensions.y * WorldDemo3.chunk_dimensions.z;
            int loaction_index = 0, index = 0;
            Vector3Int location;
            BlockType[] block_types;
            CrackState[] crack_states;

            for (c = 0; c < n_chunk; c++)
            {
                location = new Vector3Int(locations[loaction_index],
                                          locations[loaction_index + 1],
                                          locations[loaction_index + 2]);
                loaction_index += 3;

                block_types = new BlockType[n_block];
                crack_states = new CrackState[n_block];

                for (b = 0; b < n_block; b++)
                {
                    block_types[b] = (BlockType)this.block_types[index];
                    crack_states[b] = CrackState.None;
                    index++;
                }

                yield return Tuple.Create(location, block_types, crack_states, visibility[c]);
            }
        }

        public void setPlayerPosition(Vector3 position)
        {
            player_x = (int)position.x;
            player_y = (int)position.y;
            player_z = (int)position.z;
        }

        public Vector3 getPlayerPosition()
        {
            return new Vector3(player_x, player_y, player_z);
        }
    }
}
