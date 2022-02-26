using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace udemy
{
    public class World : MonoBehaviour
    {
        public static Vector3Int world_dimesions = new Vector3Int(5, 5, 5);
        public static Vector3Int extra_world_dimesions = new Vector3Int(5, 5, 5);
        public static Vector3Int chunk_dimensions = new Vector3Int(10, 10, 10);

        private static Vector3Int[] directions = new Vector3Int[] {
            Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right, Vector3Int.forward, Vector3Int.back
        };

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

        #region fBM 3D
        public ClusterSetting cave_setting;
        public static Cluster cave_cluster;

        public ClusterSetting tree_setting;
        public static Cluster tree_cluster;

        public ClusterSetting biome_setting;
        public static Cluster biome_cluster; 
        #endregion

        public Dictionary<Vector3Int, Chunk> chunks = new Dictionary<Vector3Int, Chunk>();
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
            tree_cluster = new Cluster(tree_setting);
            biome_cluster = new Cluster(biome_setting);

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
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

                if (Physics.Raycast(ray, out RaycastHit hit, 10f))
                {
                    if (!hit.collider.gameObject.transform.parent.TryGetComponent(out Chunk chunk))
                    {
                        Debug.Log($"No Chunk, parent: {hit.collider.gameObject.transform.parent.gameObject.name}," +
                                  $"target: {hit.collider.gameObject.name}");
                        return;
                    }

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

                    //Debug.Log($"Block location: {hit_block}");
                    int bx = (int)(Mathf.Round(hit_block.x) - chunk.location.x);
                    int by = (int)(Mathf.Round(hit_block.y) - chunk.location.y);
                    int bz = (int)(Mathf.Round(hit_block.z) - chunk.location.z);
                    int i;

                    // 左鍵(0)：挖掘方塊
                    if (Input.GetMouseButtonDown(0))
                    {
                        i = xyzToFlat(bx, by, bz);
                        //Debug.Log($"Block location: ({bx}, {by}, {bz}), block_type: {chunk.getBlockType(i)}");

                        // 累加破壞程度(若破壞程度與方塊強度相當，才會真的破壞掉)
                        if (chunk.crackBlock(index: i))
                        {
                            // 判斷當前方塊是否位於 Chunk 邊界上，若是，與之交界的是哪個 Chunk？
                            HashSet<Vector3Int> neighbour_locations = getNeighboringChunkLocation(chunk, bx, by, bz);

                            // 將與被破壞的 Block 交界的 Chunk 全部重繪
                            rebuild(neighbour_locations);

                            // 考慮當前方塊的上方一格是否會觸發掉落機制
                            dropBlockAbove(chunk: chunk, block_position: new Vector3Int(bx, by, bz));
                        }
                    }

                    // 右鍵(1)：放置方塊
                    else
                    {
                        // TODO: 考慮 世界 的大小，取得的 chunk 不一定存在於 chunks 當中
                        (Vector3Int, Vector3Int) chunk_block_location = chunk.getChunkBlockLocation(bx, by, bz);

                        if (chunks.ContainsKey(chunk_block_location.Item1))
                        {
                            chunk = chunks[chunk_block_location.Item1];
                            i = vector3IntToFlat(chunk_block_location.Item2);

                            chunk.placeBlock(index: i, block_type: player.getBlockType());

                            StartCoroutine(dropBlock(chunk: chunk, block_index: i));
                        }
                    }

                    // 當 新增 或 破壞 方塊後，重新繪製 Chunk
                    rebuild(chunk);
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
            int n_chunk_column = world_dimesions.x * world_dimesions.z;
            int n_chunk = world_dimesions.x * world_dimesions.y * world_dimesions.z;

            // 跨 Chunk 建置 以及 種樹，兩者都是要遍歷當前所有 Chunk，因此工作量是 n_chunk * 2
            loading_bar.maxValue = n_chunk_column + n_chunk * 2;
            int x, z, column_z;

            // 初始化 chunks 以及各個 Chunk 的方塊 類型(block_types) 與 狀態(crack_states)
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

            // 跨 Chunk 建置
            yield return buildInitialization(locations: new HashSet<Vector3Int>(chunks.Keys));

            // 跨 Chunk 種樹
            yield return plantVegetations();

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

            StartCoroutine(buildExtraWorld(visiable: false));
        }

        IEnumerator buildExtraWorld(bool visiable = false)
        {
            int z_start = world_dimesions.z;
            int z_end = world_dimesions.z + extra_world_dimesions.z;
            int x_start = world_dimesions.x;
            int x_end = world_dimesions.x + extra_world_dimesions.x;
            int x, z;
            HashSet<Vector3Int> chunk_locations = new HashSet<Vector3Int>();
            HashSet<Vector3Int> locations, neighbors;

            /* NOTE: 目前呼叫 buildChunkColumn 後只是定義了各個 Block 的類型等數據，尚未實際添加 Mesh，
             * 若在呼叫 build 之前想對 Mesh 進行操作，則會發生錯誤 */
            for (z = z_start; z < z_end; z++)
            {
                for (x = 0; x < x_start; x++)
                {
                    locations = buildChunkColumn(chunk_dimensions.x * x, chunk_dimensions.z * z);

                    foreach (Vector3Int location in locations)
                    {
                        neighbors = getNeighbourLocations(location: location);
                        Utils.combineHashSet(ref chunk_locations, neighbors);
                    }

                    Utils.combineHashSet(ref chunk_locations, locations);
                    yield return null;
                }
            }

            for (z = 0; z < z_end; z++)
            {
                for (x = x_start; x < x_end; x++)
                {
                    locations = buildChunkColumn(chunk_dimensions.x * x, chunk_dimensions.z * z);

                    foreach (Vector3Int location in locations)
                    {
                        neighbors = getNeighbourLocations(location: location);
                        Utils.combineHashSet(ref chunk_locations, neighbors);
                    }

                    Utils.combineHashSet(ref chunk_locations, locations);
                    yield return null;
                }
            }

            yield return build(locations: chunk_locations, visiable: visiable);

            yield return plantVegetations(locations: chunk_locations);
        }

        /// <summary>
        /// 建立一柱 Chunk
        /// </summary>
        /// <param name="col_x">ChunkColumn 的 X 座標</param>
        /// <param name="col_z">ChunkColumn 的 Z 座標</param>
        /// <param name="visiable"></param>
        /// <returns>被建立的 Chunk 們的位置</returns>
        HashSet<Vector3Int> buildChunkColumn(int col_x, int col_z)
        {
            HashSet<Vector3Int> chunk_locations = new HashSet<Vector3Int>();

            // 若是已建立過的 ChunkColumn，則再次使其呈現即可
            if (chunk_columns.Contains(new Vector2Int(col_x, col_z)))
            {
                setChunkColumnVisiable(col_x, col_z, visiable: true);
            }

            // 若沒建立過位於 (col_x, col_z) 的 ChunkColumn，則產生並加入管理
            else
            {
                GameObject chunk_obj;
                Chunk chunk;

                foreach(Vector3Int location in iterChunkColumnLocation(col_x, col_z))
                {
                    // 產生 Chunk 物件
                    chunk_obj = Instantiate(chunk_prefab);
                    chunk_obj.name = $"Chunk_{location.x}_{location.y}_{location.z}";

                    chunk = chunk_obj.GetComponent<Chunk>();
                    chunk.init(dimensions: chunk_dimensions, location: location);
                    chunk_locations.Add(location);
                    chunks.Add(location, chunk);
                }

                chunk_columns.Add(new Vector2Int(col_x, col_z));
            }

            return chunk_locations;
        }

        /// <summary>
        /// 建置多柱 Chunk，只在世界初始化時使用，考慮 loading_bar 的進度
        /// </summary>
        /// <returns></returns>
        private IEnumerator buildInitialization(HashSet<Vector3Int> locations, bool visiable = true)
        {
            IEnumerator iter = build(locations, visiable);

            while (iter.MoveNext())
            {
                loading_bar.value++;
                yield return iter.Current;
            }
        }

        /// <summary>
        /// 世界初始化之後，都使用這個，只建置指定位置的 Chunk，而非全部重新建置，節省所需資源
        /// </summary>
        /// <param name="locations"></param>
        /// <returns></returns>
        private IEnumerator build(HashSet<Vector3Int> locations, bool visiable = true)
        {
            Chunk chunk;

            foreach(Vector3Int location in locations)
            {
                chunk = chunks[location];

                // 若 Chunk 尚未設置過六面鄰居
                if (!chunk.hasMetNeighbors())
                {
                    // Chunk 設置過六面鄰居(利用當前 Chunk 的位置取得鄰居 Chunk)
                    chunk.setNeighbors(neighbors: getNeighbours(location: location));
                }

                chunk.build();
                chunk.setVisiable(visiable: visiable);
                yield return null;
            }
        }

        /// <summary>
        /// 種植植物，由於要考慮到跨 Chunk 的情況，因此必須在 build 之後執行。
        /// 種植過後，該植物就在 Chunk 的管理下了，即便需要重新繪製 Chunk，也不需要再呼叫此函式
        /// </summary>
        /// <param name="version"></param>
        private IEnumerator plantVegetations()
        {
            HashSet<Vector3Int> locations = new HashSet<Vector3Int>(chunks.Keys);
            IEnumerator iter = plantVegetations(locations: locations);

            while (iter.MoveNext())
            {
                loading_bar.value++;
                yield return iter.Current;
            }
        }

        /// <summary>
        /// 在這裡呼叫 Chunk.buildTrees()，樹的建構才有辦法考慮到跨 Chunk 的情況
        /// </summary>
        /// <param name="version"></param>
        private IEnumerator plantVegetations(HashSet<Vector3Int> locations)
        {
            Chunk chunk, target_chunk;
            IEnumerable<(Vector3Int, BlockType)> vegetations;
            (Vector3Int, Vector3Int) chunk_block_location;
            HashSet<Vector3Int> rebuild_locations = new HashSet<Vector3Int>();
            Vector3Int target_location;
            int t_index;

            foreach (Vector3Int location in locations)
            {
                chunk = chunks[location];
                vegetations = chunk.iterVegetations();
                rebuild_locations.Clear();

                // 依序取出樹的部分位置與方塊
                foreach ((Vector3Int, BlockType) vegetation in vegetations)
                {
                    // 考慮樹的位置可能跨 Chunk，取得校正後的 Chunk 和 Block 索引值
                    chunk_block_location = chunk.getChunkBlockLocationAdvanced(vegetation.Item1);
                    target_location = chunk_block_location.Item1;

                    // 檢查校正後的 Chunk 是否已建立，不存在則跳過
                    if (!chunks.ContainsKey(target_location))
                    {
                        continue;
                    }

                    rebuild_locations.Add(target_location);

                    // 取得校正後的 Chunk 索引值
                    target_chunk = chunks[target_location];

                    // 取得校正後的 Block 索引值的 flat 版本
                    t_index = vector3IntToFlat(chunk_block_location.Item2);

                    // 於校正後的 Chunk 放置樹的部分方塊
                    target_chunk.placeBlock(index: t_index, block_type: vegetation.Item2);
                }

                rebuild(rebuild_locations);
                yield return null;
            }
        }

        private void rebuild(HashSet<Vector3Int> locations)
        {
            foreach (Vector3Int location in locations)
            {
                rebuild(chunk: chunks[location]);
            }
        }

        /// <summary>
        /// 當 新增 或 破壞 方塊後，呼叫此函式，以重新繪製 Chunk
        /// </summary>
        /// <param name="chunk"></param>
        private void rebuild(Chunk chunk)
        {
            if (!chunk.hasMetNeighbors())
            {
                chunk.setNeighbors(neighbors: getNeighbours(location: chunk.location));
            }

            chunk.rebuild();
        }

        /// <summary>
        /// 取得當前 Chunk 的六個面所銜接的 Chunk，若尚未建立則返回 null
        /// </summary>
        /// <param name="location">當前 Chunk 的位置</param>
        /// <returns> 當前 Chunk 的六個面所銜接的 Chunk </returns>
        private (Chunk up, Chunk down, Chunk left, Chunk right, Chunk forward, Chunk back) getNeighbours(Vector3Int location)
        {            
            Chunk[] neighbors = new Chunk[6];
            Vector3Int neighbor;

            for (int i = 0; i < 6; i++)
            {
                neighbor = location + chunk_dimensions * directions[i];

                if (chunks.ContainsKey(neighbor))
                {
                    neighbors[i] = chunks[neighbor];
                }
                else
                {
                    neighbors[i] = null;
                }
            }

            return (neighbors[0], neighbors[1], neighbors[2], neighbors[3], neighbors[4], neighbors[5]);
        }

        private HashSet<Vector3Int> getNeighbourLocations(Vector3Int location)
        {
            HashSet<Vector3Int> neighbors = new HashSet<Vector3Int>();
            Vector3Int neighbor;

            for(int i = 0; i < 6; i++)
            {
                neighbor = location + chunk_dimensions * directions[i];

                if (chunks.ContainsKey(neighbor))
                {
                    neighbors.Add(neighbor);
                }
            }

            return neighbors;
        }

        /// <summary>
        /// 若當前方塊位於 Chunk 的邊界，則取得與當前 Chunk 所銜接的鄰居 Chunk
        /// </summary>
        /// <param name="chunk"></param>
        /// <param name="bx"></param>
        /// <param name="by"></param>
        /// <param name="bz"></param>
        /// <returns></returns>
        private HashSet<Vector3Int> getNeighboringChunkLocation(Chunk chunk, int bx, int by, int bz)
        {
            HashSet<Vector3Int> neighbours = new HashSet<Vector3Int>();
            Vector3Int location;

            if(bx + 1 >= chunk_dimensions.x)
            {
                location = chunk.location + chunk_dimensions.x * Vector3Int.right;

                if (chunks.ContainsKey(location))
                {
                    neighbours.Add(location);
                }
            }

            if (bx - 1 < 0)
            {
                location = chunk.location + chunk_dimensions.x * Vector3Int.left;

                if (chunks.ContainsKey(location))
                {
                    neighbours.Add(location);
                }
            }

            if(by + 1 >= chunk_dimensions.y)
            {
                location = chunk.location + chunk_dimensions.y * Vector3Int.up;

                if (chunks.ContainsKey(location))
                {
                    neighbours.Add(location);
                }
            }

            if (by - 1 < 0)
            {
                location = chunk.location + chunk_dimensions.y * Vector3Int.down;

                if (chunks.ContainsKey(location))
                {
                    neighbours.Add(location);
                }
            }

            if (bz + 1 >= chunk_dimensions.z)
            {
                location = chunk.location + chunk_dimensions.z * Vector3Int.forward;

                if (chunks.ContainsKey(location))
                {
                    neighbours.Add(location);
                }
            }

            if (bz - 1 < 0)
            {
                location = chunk.location + chunk_dimensions.z * Vector3Int.back;

                if (chunks.ContainsKey(location))
                {
                    neighbours.Add(location);
                }
            }

            return neighbours;
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

            HashSet<Vector3Int> chunk_locations = new HashSet<Vector3Int>();
            HashSet<Vector3Int> locations, neighbors;

            next_col = col_z + chunk_dimensions.z;
            locations = buildChunkColumn(col_x, next_col);

            foreach (Vector3Int location in locations)
            {
                neighbors = getNeighbourLocations(location: location);
                Utils.combineHashSet(ref chunk_locations, neighbors);
            }

            // Next chunk z position: z + chunkDimensions.z
            task_queue.Enqueue(buildColumnRecursive(col_x, next_col, next_radius));
            yield return null;

            next_col = col_z - chunk_dimensions.z;
            locations = buildChunkColumn(col_x, next_col);

            foreach (Vector3Int location in locations)
            {
                neighbors = getNeighbourLocations(location: location);
                Utils.combineHashSet(ref chunk_locations, neighbors);
            }

            task_queue.Enqueue(buildColumnRecursive(col_x, next_col, next_radius));
            yield return null;

            next_col = col_x + chunk_dimensions.x;
            locations = buildChunkColumn(next_col, col_z);

            foreach (Vector3Int location in locations)
            {
                neighbors = getNeighbourLocations(location: location);
                Utils.combineHashSet(ref chunk_locations, neighbors);
            }

            task_queue.Enqueue(buildColumnRecursive(next_col, col_z, next_radius));
            yield return null;

            next_col = col_x - chunk_dimensions.x;
            locations = buildChunkColumn(next_col, col_z);

            foreach (Vector3Int location in locations)
            {
                neighbors = getNeighbourLocations(location: location);
                Utils.combineHashSet(ref chunk_locations, neighbors);
            }

            task_queue.Enqueue(buildColumnRecursive(next_col, col_z, next_radius));

            yield return build(locations: chunk_locations, visiable: true);
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
            IEnumerable<Chunk> chunk_column = iterChunkColumn(col_x, col_z);

            foreach(Chunk chunk in chunk_column)
            {
                chunk.setVisiable(visiable);
            }
        }

        IEnumerator dropBlock(Chunk chunk, int block_index, int spread = 2)
        {
            BlockType block_type = chunk.getBlockType(block_index);

            if (!MeshUtils.canDrop(block_type: block_type))
            {
                yield break;
            }

            yield return falling_buffer;

            Vector3Int block_position;
            (Vector3Int, Vector3Int) location_below;
            Chunk chunk_below;
            int block_below_index;
            HashSet<Vector3Int> neighbour_locations, temp_locations;

            while (true)
            {
                // 取得當前方塊的三維座標(當前 Chunk 的座標系)
                block_position = flatToVector3Int(i: block_index);

                // 取得當前方塊的下方方塊位置(有可能跨到下一個 Chunk，使用 getChunkBlockLocation 取得正確的 Chunk 和 Block 的索引值)
                location_below = chunk.getChunkBlockLocation(bx: block_position.x, 
                                                             by: block_position.y - 1, 
                                                             bz: block_position.z);

                // 若存在下方方塊所屬 Chunk
                if (chunks.ContainsKey(location_below.Item1))
                {
                    // 當前方塊的下方方塊所屬 Chunk
                    chunk_below = chunks[location_below.Item1];

                    // 取得下方方塊的三維座標(下方方塊所屬 Chunk 的座標系)
                    block_below_index = vector3IntToFlat(v: location_below.Item2);
                }

                // 若下方方塊所屬 Chunk 不存在
                else
                {
                    chunk_below = null;
                    block_below_index = -1;
                }

                // NOTE: 當方塊掉往的 Chunk 尚未被建構，可能發生 chunk_below == null
                // 檢查下方是否有掉落空間
                if (chunk_below != null && chunk_below.getBlockType(block_below_index).Equals(BlockType.AIR))
                {
                    // 方塊落往 chunk_below
                    chunk_below.setBlockType(index: block_below_index, block_type: block_type);
                    chunk_below.setCrackState(index: block_below_index);

                    // 更新當前方塊
                    chunk.setBlockType(index: block_index, block_type: BlockType.AIR);
                    chunk.setCrackState(index: block_index);

                    // 考慮當前方塊的上方一格是否會觸發掉落機制
                    dropBlockAbove(chunk: chunk, block_position: block_position);

                    yield return falling_buffer;

                    // TODO: 方塊掉落的過程中，需要更新的 Chunk 實在太多，破壞得多的話，fps 從 200 多掉到只剩 80 幾，應該要加入 task_queue 來執行或是想辦法只更新一小部分
                    neighbour_locations = new HashSet<Vector3Int>() { chunk.location };

                    // 判斷當前方塊是否位於 Chunk 邊界上，若是，與之交界的是哪個 Chunk？
                    temp_locations = getNeighboringChunkLocation(chunk: chunk, 
                                                                 bx: block_position.x, 
                                                                 by: block_position.y, 
                                                                 bz: block_position.z);

                    // 將與被破壞的 Block 交界的 Chunk 全部重繪
                    Utils.combineHashSet(ref neighbour_locations, temp_locations);

                    if (chunk_below != chunk)
                    {
                        neighbour_locations.Add(chunk_below.location);

                        // 判斷當前方塊是否位於 Chunk 邊界上，若是，與之交界的是哪個 Chunk？
                        temp_locations = getNeighboringChunkLocation(chunk: chunk,
                                                                     bx: location_below.Item2.x,
                                                                     by: location_below.Item2.y,
                                                                     bz: location_below.Item2.z);

                        // 將與被破壞的 Block 交界的 Chunk 全部重繪
                        Utils.combineHashSet(ref neighbour_locations, temp_locations);
                    }

                    // 將與被破壞的 Block 交界的 Chunk 全部重繪
                    rebuild(locations: neighbour_locations);

                    // 指向落下後的方塊
                    block_index = block_below_index;
                    chunk = chunk_below;
                }

                // 下方無掉落空間，考慮是否會向四周溢出
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

        private void spreadBlock(Chunk chunk, Vector3Int block_position, Vector3Int direction, int spread = 2)
        {
            spread--;

            if (spread < 0)
            {
                return;
            }

            // 取得溢出方向的方塊位置(所屬 Chunk 位置 & 方塊座標)
            (Vector3Int, Vector3Int) location = chunk.getChunkBlockLocation(block_position + direction);

            // 若該方塊尚未被建立
            if (!chunks.ContainsKey(location.Item1))
            {
                return;
            }

            // 取得溢出方向的方塊所屬 Chunk
            Chunk neighbor_chunk = chunks[location.Item1];

            // 取得溢出方向的方塊索引值
            int block_neighbor_index = vector3IntToFlat(location.Item2);

            // 若該方向有空間可溢出(方塊類型為 BlockType.AIR)
            if (neighbor_chunk.getBlockType(block_neighbor_index).Equals(BlockType.AIR))
            {
                // 取得當前方塊索引值
                int block_index = vector3IntToFlat(block_position);

                // 更新溢出方向的方塊類型
                neighbor_chunk.setBlockType(index: block_neighbor_index, chunk.getBlockType(block_index));

                // 更新溢出方向的方塊狀態
                neighbor_chunk.setCrackState(index: block_neighbor_index);

                // 重新繪製溢出方向的方塊所屬 Chunk
                rebuild(neighbor_chunk);

                // 繼續檢查是否可以繼續往下掉
                StartCoroutine(dropBlock(chunk: neighbor_chunk, block_index: block_neighbor_index, spread: spread));
            }
        }

        /// <summary>
        /// 考慮當前方塊的上方一格是否會觸發掉落機制
        /// </summary>
        /// <param name="chunk"></param>
        /// <param name="block_position"></param>
        void dropBlockAbove(Chunk chunk, Vector3Int block_position)
        {
            (Vector3Int, Vector3Int) location_above = chunk.getChunkBlockLocation(bx: block_position.x,
                                                                                  by: block_position.y + 1,
                                                                                  bz: block_position.z);

            if (chunks.ContainsKey(location_above.Item1))
            {
                Chunk chunk_above = chunks[location_above.Item1];
                int block_above_index = vector3IntToFlat(v: location_above.Item2);

                StartCoroutine(dropBlock(chunk_above, block_above_index));
            }
        }

        IEnumerable<Chunk> iterChunkColumn(int col_x, int col_z)
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
            WorldData wd = new WorldData();

            foreach (KeyValuePair<Vector3Int, Chunk> item in chunks)
            {
                wd.setLocation(location: item.Key);
                wd.setVisibility(visible: item.Value.isVisiable());
                wd.setBlockTypes(block_types: item.Value.block_types);
            }

            wd.setPlayerPosition(position: player.transform.position);

            WorldRecorder.save(wd);
        }

        // 目前讀取檔案來建構世界的過程中，沒有區分哪些是 Extra World，因此會全部都建構後才進入遊戲
        IEnumerator loadWorldFromFile()
        {
            WorldData wd = WorldRecorder.load();

            if (wd == null)
            {
                StartCoroutine(buildWorld());
                yield break;
            }

            chunks.Clear();
            chunk_columns.Clear();

            Vector3Int location;
            GameObject obj;
            Chunk chunk;

            // NOTE: 若想初始化完玩家周圍後就進入遊戲，loading_bar.maxValue 的設置會是個問題
            loading_bar.maxValue = wd.getChunkNumber();
            var chunk_datas = wd.iterChunkDatas();

            HashSet<Vector3Int> visiables = new HashSet<Vector3Int>();
            HashSet<Vector3Int> invisiables = new HashSet<Vector3Int>();

            while (chunk_datas.MoveNext())
            {
                var chunk_data = chunk_datas.Current;
                location = chunk_data.Item1;

                // NOTE: 若想初始化完玩家周圍後就進入遊戲，可以利用 location 和玩家位置，判斷是否需要現在就建置

                obj = Instantiate(chunk_prefab);
                obj.name = $"Chunk_{location.x}_{location.y}_{location.z}";
                chunk = obj.GetComponent<Chunk>();

                chunk.block_types = chunk_data.Item2;
                chunk.crack_states = chunk_data.Item3;

                chunk.locate(dimensions: chunk_dimensions, location: location);

                if (chunk_data.Item4)
                {
                    visiables.Add(location);
                }
                else
                {
                    invisiables.Add(location);
                }

                chunk_columns.Add(new Vector2Int(location.x, location.z));
                chunks.Add(location, chunk);

                yield return null;
            }

            yield return buildInitialization(visiables, visiable: true);

            player.transform.position = wd.getPlayerPosition();
            main_camera.SetActive(false);
            loading_bar.gameObject.SetActive(false);
            player.gameObject.SetActive(true);
            last_position = Vector3Int.CeilToInt(player.transform.position);

            // NOTE: 若想初始化完玩家周圍後就進入遊戲，這裡之前就可先進入，這之後建構剩餘的世界
            StartCoroutine(buildInitialization(invisiables, visiable: false));

            // 依序執行 buildQueue 當中的 IEnumerator
            StartCoroutine(taskCoordinator());

            //// 將 IEnumerator 添加到 buildQueue 當中
            //StartCoroutine(updateWorld());
        }

        public static Vector3Int flatToVector3Int(int i)
        {
            return Utils.flatToVector3Int(i, width: chunk_dimensions.x, height: chunk_dimensions.y);
        }

        public static int xyzToFlat(int x, int y, int z)
        {
            return Utils.xyzToFlat(x, y, z, chunk_dimensions.x, chunk_dimensions.z);
        }

        public static int vector3IntToFlat(Vector3Int v)
        {
            return Utils.vector3IntToFlat(v: v, width: chunk_dimensions.x, depth: chunk_dimensions.z);
        }
    }

    [Serializable]
    public class WorldData
    {
        // 每個 chunk_location 分別有 3 個數值 (x, y, z)，共有 n_chunk * 3 個數值 
        public List<int> locations;

        // 每個 Chunk 分別有 chunk_dimensions.x * chunk_dimensions.y * chunk_dimensions.z 個數值
        // 共有 n_chunk * chunk_dimensions.x * chunk_dimensions.y * chunk_dimensions.z 個數值 
        public List<int> block_types;

        public List<bool> visibility;

        // 玩家位置
        public int player_x;
        public int player_y;
        public int player_z;

        public WorldData()
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
            int b, c, n_chunk = getChunkNumber(), n_block = World.chunk_dimensions.x * World.chunk_dimensions.y * World.chunk_dimensions.z;
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
