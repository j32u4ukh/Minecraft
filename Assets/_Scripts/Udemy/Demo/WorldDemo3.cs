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

        // ������n�Q�� Queue �Ӻ޲z�o�ǫسy�M���� ChunkColumn �����ȡH�O���F�j�ե��Ȫ����ǩʡA�H�קK�᭱���p���Ȥ�e�����j�����٭n�ֵ����ܡH
        Queue<IEnumerator> task_queue = new Queue<IEnumerator>();

        // �W�@�Ӭ����I�A���a����m
        [SerializeField] private Vector3Int last_position;

        // �i�ݨ쪺�Z��
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
            // ����(0)�G��������F�k��(1)�G��m���
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
                    //    // TODO: �оǤ����F�קK health �� -1 ������Q�R���A�]���[�F�o�ӧP�_�A�����ڥ��S���n�Chealth �q NOCRACK(10) �}�l���W�[�A���ӴN���i��[�� -1
                    //    if (MeshUtils.blockTypeHealth[(int)chunk.chunkData[i]] != -1)
                    //    {
                    //        // �Ĥ@���V����Ĳ�o�A�@�q�ɶ����ˬd�O�_�w�Q�V���A�_�h�״_�ۤv health ��_�� NOCRACK
                    //        if (chunk.healthData[i] == MeshUtils.BlockType.NOCRACK)
                    //        {
                    //            StartCoroutine(HealBlock(c: chunk, blockIndex: i));
                    //        }

                    //        chunk.healthData[i]++;

                    //        if (chunk.healthData[i] == MeshUtils.BlockType.NOCRACK + MeshUtils.blockTypeHealth[(int)chunk.chunkData[i]])
                    //        {
                    //            chunk.chunkData[i] = MeshUtils.BlockType.AIR;
                    //            chunk.healthData[i] = MeshUtils.BlockType.NOCRACK;

                    //            // �W�����O�_�����ˬd
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

                    //    // ����O�_�����ˬd
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
                    // x, z: Chunk column �����ޭ�
                    // x * chunk_dimensions.x, column_z: Chunk column ����ڮy��(�M���ޭȮt�F chunk_dimensions.x ��)
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

            // �Ұ� taskCoordinator�A�̧ǰ���Q����������
            StartCoroutine(taskCoordinator());

            // NOTE: �ȥB�������\��A�H�Q�}�o��L����
            // �N IEnumerator �K�[�� buildQueue ��
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
        /// <param name="col_x">ChunkColumn �� X �y��</param>
        /// <param name="col_z">ChunkColumn �� Z �y��</param>
        void buildChunkColumn(int col_x, int col_z, bool visiable = true)
        {
            // �Y�O�w�إ߹L�� ChunkColumn�A�h�A���Ϩ�e�{�Y�i
            if (chunk_columns.Contains(new Vector2Int(col_x, col_z)))
            {
                displayChunkColumn(col_x, col_z, visiable: visiable);
            }

            // �Y�S�إ߹L��� (col_x, col_z) �� ChunkColumn�A�h���ͨå[�J�޲z
            else
            {
                GameObject chunk_obj;
                Vector3Int location;
                Chunk2 chunk;

                // �̧ǫإߦP�@�� ChunkColumn �̭��� Chunk
                for (int y = 0; y < world_dimesions.y; y++)
                {
                    // y: Chunk �����ޭ�
                    // y * chunk_dimensions.y: Chunk ����ڮy��(�M���ޭȮt�F chunk_dimensions.y ��)
                    location = new Vector3Int(col_x, y * chunk_dimensions.y, col_z);

                    // ���� Chunk ����
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
            Vector2Int fpc_position = new Vector2Int(x, z);
            float sight_distance = sight_unit * chunk_dimensions.x;

            foreach (Vector2Int column_position in chunk_columns)
            {
                // �Y ChunkColumn �Z�����a�L��
                if ((column_position - fpc_position).magnitude >= sight_distance)
                {
                    // ���� ChunkColumn�A�]���ӻ��ݤ��� 
                    // ��ڤW�O Z �ȡA�� Vector2Int �����ݩʬ� y
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
