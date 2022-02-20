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

        // ������n�Q�� Queue �Ӻ޲z�o�ǫسy�M���� ChunkColumn �����ȡH�O���F�j�ե��Ȫ����ǩʡA�H�קK�᭱���p���Ȥ�e�����j�����٭n�ֵ����ܡH
        Queue<IEnumerator> task_queue = new Queue<IEnumerator>();

        // �W�@�Ӭ����I�A���a����m
        [SerializeField] private Vector3Int last_position;

        // �i�ݨ쪺�Z��
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
            // TODO: ���� Player ���޲z�A�Q�Ψƥ�q�� World ���Ǥ���Q�����A���Ǥ���S�Q�s�W
            // ����(0)�G��������F�k��(1)�G��m���
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

                    // ����(0)�G�������
                    if (Input.GetMouseButtonDown(0))
                    {
                        hit_block = hit.point - hit.normal / 2.0f;
                    }

                    // �k��(1)�G��m���
                    else
                    {
                        hit_block = hit.point + hit.normal / 2.0f;
                    }

                    //Debug.Log($"Block location: {hit_block}");
                    int bx = (int)(Mathf.Round(hit_block.x) - chunk.location.x);
                    int by = (int)(Mathf.Round(hit_block.y) - chunk.location.y);
                    int bz = (int)(Mathf.Round(hit_block.z) - chunk.location.z);
                    int i;

                    // ����(0)�G�������
                    if (Input.GetMouseButtonDown(0))
                    {
                        i = xyzToFlat(bx, by, bz);
                        //Debug.Log($"Block location: ({bx}, {by}, {bz}), block_type: {chunk.getBlockType(i)}");

                        // �֥[�}�a�{��(�Y�}�a�{�׻P����j�׬۷�A�~�|�u���}�a��)
                        if (chunk.crackBlock(index: i))
                        {
                            // �P�_��e����O�_��� Chunk ��ɤW�A�Y�O�A�P����ɪ��O���� Chunk�H
                            HashSet<Vector3Int> neighbour_locations = getNeighboringChunkLocation(chunk, bx, by, bz);

                            // �N�P�Q�}�a�� Block ��ɪ� Chunk ������ø
                            foreach (Vector3Int neighbour_location in neighbour_locations)
                            {
                                rebuildConsiderAround(chunks[neighbour_location]);
                            }

                            // �Ҽ{��e������W��@��O�_�|Ĳ�o��������
                            dropBlockAbove(chunk: chunk, block_position: new Vector3Int(bx, by, bz));
                        }
                    }

                    //// �k��(1)�G��m���
                    //else
                    //{
                    //    // TODO: �Ҽ{ �@�� ���j�p�A���o�� chunk ���@�w�s�b�� chunks ��
                    //    (Vector3Int, Vector3Int) chunk_block_location = chunk.getChunkBlockLocation(bx, by, bz);

                    //    if (chunks.ContainsKey(chunk_block_location.Item1))
                    //    {
                    //        chunk = chunks[chunk_block_location.Item1];
                    //        i = vector3IntToFlat(chunk_block_location.Item2);

                    //        chunk.placeBlock(index: i, block_type: player.getBlockType());

                    //        StartCoroutine(dropBlock(chunk: chunk, block_index: i));
                    //    }
                    //}

                    // �� �s�W �� �}�a �����A���sø�s Chunk
                    rebuildConsiderAround(chunk);
                }
            }

            //if (Input.GetKey(KeyCode.RightControl))
            //{
            //    if (Input.GetKeyDown(KeyCode.RightAlt))
            //    {
            //        saveWorldToFile();
            //    }
            //}
        }

        IEnumerator buildWorld()
        {
            int n_chunk_column = world_dimesions.x * world_dimesions.z;
            int n_chunk = world_dimesions.x * world_dimesions.y * world_dimesions.z;

            // �� Chunk �B�z �H�� �ؾ�A��̳��O�n�M����e�Ҧ� Chunk�A�]���u�@�q�O n_chunk * 2
            loading_bar.maxValue = n_chunk_column + n_chunk * 2;
            int x, z, column_z;

            // ��l�� chunks �H�ΦU�� Chunk ����� ����(block_types) �P ���A(crack_states)
            for (z = 0; z < world_dimesions.z; z++)
            {
                column_z = z * chunk_dimensions.z;

                for (x = 0; x < world_dimesions.x; x++)
                {
                    // x, z: Chunk column �����ޭ�
                    // x * chunk_dimensions.x, column_z: Chunk column ����ڮy��(�M���ޭȮt�F chunk_dimensions.x ��)
                    buildChunkColumn(x * chunk_dimensions.x, column_z);
                    loading_bar.value++;
                    yield return null;
                }
            }

            // TODO: �b�o�̦Ҽ{ Chunk ��ɰ��D�A���� Chunk ��ɪ� Mesh
            yield return buildConsiderAround();

            // �b�o�̩I�s Chunk.buildTrees()�A�𪺫غc�~����k�Ҽ{��� Chunk �����p
            yield return buildVegetations();

            main_camera.SetActive(false);

            // Place the player in the center of map
            int xpos = chunk_dimensions.x * world_dimesions.x / 2;
            int zpos = chunk_dimensions.z * world_dimesions.z / 2;
            int ypos = (int)surface_strata.fBM(xpos, zpos) + 10;

            player.transform.position = new Vector3(xpos, ypos, zpos);
            player.gameObject.SetActive(true);
            last_position = new Vector3Int(xpos, ypos, zpos);
            loading_bar.gameObject.SetActive(false);

            // �Ұ� taskCoordinator�A�̧ǰ���Q����������
            StartCoroutine(taskCoordinator());

            // NOTE: �ȥB�������\��A�H�Q�}�o��L����
            // �N IEnumerator �K�[�� buildQueue ��
            //StartCoroutine(updateWorld());

            StartCoroutine(buildExtraWorld(visiable: false));
        }

        IEnumerator buildExtraWorld(bool visiable = false)
        {
            int z_start = world_dimesions.z;
            int z_end = world_dimesions.z + extra_world_dimesions.z;
            int x_start = world_dimesions.x;
            int x_end = world_dimesions.x + extra_world_dimesions.x;

            /* NOTE: �ثe�I�s buildChunkColumn ��u�O�w�q�F�U�� Block ���������ƾڡA�|����ڲK�[ Mesh�A
             * �Y�b�I�s buildConsiderAround ���e�Q�� Mesh �i��ާ@�A�h�|�o�Ϳ��~ */
            for (int z = z_start; z < z_end; z++)
            {
                for (int x = 0; x < x_start; x++)
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

            yield return buildConsiderAround();

            yield return buildVegetations();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="col_x">ChunkColumn �� X �y��</param>
        /// <param name="col_z">ChunkColumn �� Z �y��</param>
        void buildChunkColumn(int col_x, int col_z, bool visiable = true)
        {
            // �Y�O�w�إ߹L�� ChunkColumn�A�h�A���Ϩ�e�{�Y�i
            if (chunk_columns.Contains(new Vector2Int(col_x, col_z)))
            {
                setChunkColumnVisiable(col_x, col_z, visiable: visiable);
            }

            // �Y�S�إ߹L��� (col_x, col_z) �� ChunkColumn�A�h���ͨå[�J�޲z
            else
            {
                GameObject chunk_obj;
                Chunk chunk;

                foreach(Vector3Int location in iterChunkColumnLocation(col_x, col_z))
                {
                    // ���� Chunk ����
                    chunk_obj = Instantiate(chunk_prefab);
                    chunk_obj.name = $"Chunk_{location.x}_{location.y}_{location.z}";

                    chunk = chunk_obj.GetComponent<Chunk>();
                    chunk.init(dimensions: chunk_dimensions, location: location);

                    chunks.Add(location, chunk);
                }

                chunk_columns.Add(new Vector2Int(col_x, col_z));
            }            
        }

        /// <summary>
        /// �Ҽ{�� Chunk ��ɰ��D��A��ڲK�[ Mesh �� Chunk ��
        /// </summary>
        /// <returns></returns>
        private IEnumerator buildConsiderAround()
        {
            (Chunk up, Chunk down, Chunk left, Chunk right, Chunk forward, Chunk back) neighbors;

            foreach (KeyValuePair<Vector3Int, Chunk> location_chunk in chunks)
            {
                neighbors = getNeighbours(location: location_chunk.Key);

                location_chunk.Value.buildConsiderAround(neighbors.up, neighbors.down, neighbors.left, neighbors.right, neighbors.forward, neighbors.back);
                location_chunk.Value.setVisiable(visiable: true);

                loading_bar.value++;
                yield return null;
            }
        }

        void rebuildConsiderAround(Chunk chunk)
        {
            (Chunk up, Chunk down, Chunk left, Chunk right, Chunk forward, Chunk back) neighbors;
            neighbors = getNeighbours(location: chunk.location);

            chunk.rebuildConsiderAround(neighbors.up, neighbors.down, neighbors.left, neighbors.right, neighbors.forward, neighbors.back);
        }

        private (Chunk up, Chunk down, Chunk left, Chunk right, Chunk forward, Chunk back) getNeighbours(Vector3Int location)
        {
            Chunk up = null, down = null, left = null, right = null, forward = null, back = null;
            Vector3Int u = location + chunk_dimensions * Vector3Int.up,
                       d = location + chunk_dimensions * Vector3Int.down,
                       l = location + chunk_dimensions * Vector3Int.left,
                       r = location + chunk_dimensions * Vector3Int.right,
                       f = location + chunk_dimensions * Vector3Int.forward,
                       b = location + chunk_dimensions * Vector3Int.back;

            if (chunks.ContainsKey(u))
            {
                up = chunks[u];
            }

            if (chunks.ContainsKey(d))
            {
                down = chunks[d];
            }

            if (chunks.ContainsKey(l))
            {
                left = chunks[l];
            }

            if (chunks.ContainsKey(r))
            {
                right = chunks[r];
            }

            if (chunks.ContainsKey(f))
            {
                forward = chunks[f];
            }

            if (chunks.ContainsKey(b))
            {
                back = chunks[b];
            }

            return (up, down, left, right, forward, back);
        }

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
        /// �̧ǰ��� task_queue ��������
        /// Coordinator: ��խ�
        /// TODO: �����u�����������G1. enable �w�غc�� ChunkColumn 2. �ͦ��s�� ChunkColumn 3. ���û��� ChunkColumn
        /// TODO: ���@�F�� ChunkColumn �y�ЦC��A�Y�n enable �έn�s�ͦ��� ChunkColumn �S�w���b�M�椤�A�γ\�i�H���X Coroutine �H�קK�L�ħ@�~
        /// </summary>
        /// <returns></returns>
        IEnumerator taskCoordinator()
        {
            while (true)
            {
                while (task_queue.Count > 0)
                {
                    // �̧Ǳq task_queue �����X���ȨӰ���
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
                // ���a���ʶZ�����W�@���@�ɥͦ��I lastBuildPosition �j�L�@�� chunk ���Z����
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
        /// �H (chunk_x, chunk_z) ����ߡA�V�|�Ӥ�V�إ߷s�� ChunkColumn
        /// </summary>
        /// <param name="col_x">Chunk column �� X �y��</param>
        /// <param name="col_z">Chunk column �� Z �y��</param>
        /// <param name="radius">���j����</param>
        /// <returns></returns>
        IEnumerator buildColumnRecursive(int col_x, int col_z, int radius)
        {
            int next_radius = radius - 1, next_col;

            // �Y radius �� 1�A�h�U�軼�j�I�s�� radius �Y�� 0�A�N���A����
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
        /// ���öZ�����a��m�L���� ChunkColumn �ӫD�R���A�]�����s�غc�|��Ӧh�ɶ��P�귽
        /// </summary>
        /// <param name="x">���a��m X �y��</param>
        /// <param name="z">���a��m Z �y��</param>
        /// <returns></returns>
        IEnumerator hideColumns(int x, int z)
        {
            Vector2Int player_position = new Vector2Int(x, z);
            float sight_distance = sight_unit * chunk_dimensions.x;

            foreach (Vector2Int column_position in chunk_columns)
            {
                // �Y ChunkColumn �Z�����a�L��
                if ((column_position - player_position).magnitude >= sight_distance)
                {
                    // ���� ChunkColumn�A�]���ӻ��ݤ��� 
                    // ��ڤW�O Z �ȡA�� Vector2Int �����ݩʬ� y
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
                block_position = flatToVector3Int(i: block_index);

                // ���o��e������U������m(���i����U�@�� Chunk�A�ϥ� getChunkBlockLocation ���o���T�� Chunk �M Block �����ޭ�)
                location_below = chunk.getChunkBlockLocation(bx: block_position.x, 
                                                             by: block_position.y - 1, 
                                                             bz: block_position.z);

                if (chunks.ContainsKey(location_below.Item1))
                {
                    chunk_below = chunks[location_below.Item1];
                    block_below_index = vector3IntToFlat(v: location_below.Item2);
                }
                else
                {
                    chunk_below = null;
                    block_below_index = -1;
                }

                // NOTE: ���������� Chunk �|���Q�غc�A�i��o�� chunk_below == null
                // �ˬd�U��O�_�������Ŷ�
                if (chunk_below != null && chunk_below.getBlockType(block_below_index).Equals(BlockType.AIR))
                {
                    // ������� chunk_below
                    chunk_below.setBlockType(index: block_below_index, block_type: block_type);
                    chunk_below.setCrackState(index: block_below_index);

                    // ��s��e���
                    chunk.setBlockType(index: block_index, block_type: BlockType.AIR);
                    chunk.setCrackState(index: block_index);

                    // �Ҽ{��e������W��@��O�_�|Ĳ�o��������
                    dropBlockAbove(chunk: chunk, block_position: block_position);

                    yield return falling_buffer;

                    neighbour_locations = new HashSet<Vector3Int>() { chunk.location };

                    // �P�_��e����O�_��� Chunk ��ɤW�A�Y�O�A�P����ɪ��O���� Chunk�H
                    temp_locations = getNeighboringChunkLocation(chunk: chunk, 
                                                                 bx: block_position.x, 
                                                                 by: block_position.y, 
                                                                 bz: block_position.z);

                    // �N�P�Q�}�a�� Block ��ɪ� Chunk ������ø
                    foreach (Vector3Int temp_location in temp_locations)
                    {
                        neighbour_locations.Add(temp_location);
                    }

                    if (chunk_below != chunk)
                    {
                        neighbour_locations.Add(chunk_below.location);

                        // �P�_��e����O�_��� Chunk ��ɤW�A�Y�O�A�P����ɪ��O���� Chunk�H
                        temp_locations = getNeighboringChunkLocation(chunk: chunk,
                                                                     bx: location_below.Item2.x,
                                                                     by: location_below.Item2.y,
                                                                     bz: location_below.Item2.z);

                        // �N�P�Q�}�a�� Block ��ɪ� Chunk ������ø
                        foreach (Vector3Int temp_location in temp_locations)
                        {
                            neighbour_locations.Add(temp_location);
                        }
                    }

                    // �N�P�Q�}�a�� Block ��ɪ� Chunk ������ø
                    foreach (Vector3Int neighbour_location in neighbour_locations)
                    {
                        rebuildConsiderAround(chunks[neighbour_location]);
                    }

                    // ���V���U�᪺���
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

        void spreadBlock(Chunk chunk, Vector3Int block_position, Vector3Int direction, int spread = 2)
        {
            spread--;

            if (spread < 0)
            {
                return;
            }

            (Vector3Int, Vector3Int) location = chunk.getChunkBlockLocation(block_position + direction);

            if (!chunks.ContainsKey(location.Item1))
            {
                return;
            }

            Chunk neighbor_chunk = chunks[location.Item1];
            int block_neighbor_index = vector3IntToFlat(location.Item2);

            if (neighbor_chunk.getBlockType(block_neighbor_index).Equals(BlockType.AIR))
            {
                int block_index = vector3IntToFlat(block_position);
                neighbor_chunk.setBlockType(index: block_neighbor_index, chunk.getBlockType(block_index));
                neighbor_chunk.setCrackState(index: block_neighbor_index);
                //neighbor_chunk.rebuild();
                rebuildConsiderAround(neighbor_chunk);

                StartCoroutine(dropBlock(chunk: neighbor_chunk, block_index: block_neighbor_index, spread: spread));
            }
        }

        /// <summary>
        /// �Ҽ{��e������W��@��O�_�|Ĳ�o��������
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

        /// <summary>
        /// �b�o�̩I�s Chunk.buildTrees()�A�𪺫غc�~����k�Ҽ{��� Chunk �����p
        /// </summary>
        /// <param name="version"></param>
        private IEnumerator buildVegetations()
        {
            Chunk target_chunk;
            IEnumerable<(Vector3Int, BlockType)> vegetations;
            (Vector3Int, Vector3Int) chunk_block_location;
            int t_index;

            foreach (Chunk chunk in chunks.Values)
            {
                vegetations = chunk.iterVegetations();

                // �̧Ǩ��X�𪺳�����m�P���
                foreach ((Vector3Int, BlockType) tree in vegetations)
                {
                    // �Ҽ{�𪺦�m�i��� Chunk�A���o�ե��᪺ Chunk �M Block ���ޭ�
                    chunk_block_location = chunk.getChunkBlockLocationAdvanced(tree.Item1);

                    // �ˬd�ե��᪺ Chunk �O�_�w�إߡA���s�b�h���L
                    if (!chunks.ContainsKey(chunk_block_location.Item1))
                    {
                        continue;
                    }

                    // ���o�ե��᪺ Chunk ���ޭ�
                    target_chunk = chunks[chunk_block_location.Item1];

                    // ���o�ե��᪺ Block ���ޭȪ� flat ����
                    t_index = vector3IntToFlat(chunk_block_location.Item2);

                    // ��ե��᪺ Chunk ��m�𪺳������
                    target_chunk.placeBlock(index: t_index, block_type: tree.Item2);
                }

                loading_bar.value++;
                yield return null;
            }

            foreach (Chunk chunk in chunks.Values)
            {
                rebuildConsiderAround(chunk);
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

        // �ثeŪ���ɮרӫغc�@�ɪ��L�{���A�S���Ϥ����ǬO Extra World�A�]���|�������غc��~�i�J�C��
        IEnumerator loadWorldFromFile()
        {
            yield return null;
            //WorldData wd = WorldRecorder.load();

            //if (wd == null)
            //{
            //    StartCoroutine(buildWorld());
            //    yield break;
            //}

            //chunks.Clear();
            //chunk_columns.Clear();

            //Vector3Int location;
            //GameObject obj;
            //Chunk chunk;

            //// NOTE: �Y�Q��l�Ƨ����a�P���N�i�J�C���Aloading_bar.maxValue ���]�m�|�O�Ӱ��D
            //loading_bar.maxValue = wd.getChunkNumber();
            //var chunk_datas = wd.iterChunkDatas();

            //while (chunk_datas.MoveNext())
            //{
            //    var chunk_data = chunk_datas.Current;
            //    location = chunk_data.Item1;

            //    // NOTE: �Y�Q��l�Ƨ����a�P���N�i�J�C���A�i�H�Q�� location �M���a��m�A�P�_�O�_�ݭn�{�b�N�ظm

            //    obj = Instantiate(chunk_prefab);
            //    obj.name = $"Chunk_{location.x}_{location.y}_{location.z}";
            //    chunk = obj.GetComponent<Chunk>();

            //    chunk.block_types = chunk_data.Item2;
            //    chunk.crack_states = chunk_data.Item3;

            //    chunk.locate(dimensions: chunk_dimensions, location: location);
            //    chunk.build();
            //    chunk.setVisiable(visiable: chunk_data.Item4);

            //    chunk_columns.Add(new Vector2Int(location.x, location.z));
            //    chunks.Add(location, chunk);

            //    loading_bar.value++;
            //    yield return null;
            //}

            //player.transform.position = wd.getPlayerPosition();
            //main_camera.SetActive(false);
            //loading_bar.gameObject.SetActive(false);
            //player.gameObject.SetActive(true);
            //last_position = Vector3Int.CeilToInt(player.transform.position);

            //// NOTE: �Y�Q��l�Ƨ����a�P���N�i�J�C���A�o�̤��e�N�i���i�J�A�o����غc�Ѿl���@��

            //// �̧ǰ��� buildQueue ���� IEnumerator
            //StartCoroutine(taskCoordinator());

            //// �N IEnumerator �K�[�� buildQueue ��
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
        // �C�� chunk_location ���O�� 3 �Ӽƭ� (x, y, z)�A�@�� n_chunk * 3 �Ӽƭ� 
        //public int[] locations;
        public List<int> locations;

        // �C�� Chunk ���O�� chunk_dimensions.x * chunk_dimensions.y * chunk_dimensions.z �Ӽƭ�
        // �@�� n_chunk * chunk_dimensions.x * chunk_dimensions.y * chunk_dimensions.z �Ӽƭ� 
        public List<int> block_types;

        public List<bool> visibility;

        // ���a��m
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
