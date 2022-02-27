using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace minecraft
{
    public class WorldDemo2 : MonoBehaviour
    {
        public static Vector3Int world_dimesions = new Vector3Int(4, 4, 4);
        public static Vector3Int chunk_dimensions = new Vector3Int(10, 10, 10);
        
        public GameObject chunk_prefab;

        public GameObject main_camera;
        public GameObject fpc;
        public Slider loading_bar;

        public StrataSetting surface_setting;

        Dictionary<Vector3Int, Chunk1> chunks = new Dictionary<Vector3Int, Chunk1>();
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
            StartCoroutine(buildWorld());
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
            float offset = surface_setting.getOffset();
            int ypos = (int)surface_setting.getAltitude(xpos, zpos, offset: offset) + 10;
            fpc.transform.position = new Vector3(xpos, ypos, zpos);
            fpc.SetActive(true);
            last_position = new Vector3Int(xpos, ypos, zpos);
            loading_bar.gameObject.SetActive(false);

            // �Ұ� taskCoordinator�A�̧ǰ���Q����������
            StartCoroutine(taskCoordinator());

            StartCoroutine(updateWorld());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="col_x">ChunkColumn �� X �y��</param>
        /// <param name="col_z">ChunkColumn �� Z �y��</param>
        void buildChunkColumn(int col_x, int col_z)
        {
            // �Y�O�w�إ߹L�� ChunkColumn�A�h�A���Ϩ�e�{�Y�i
            if (chunk_columns.Contains(new Vector2Int(col_x, col_z)))
            {
                displayChunkColumn(col_x, col_z, enabled: true);
            }

            // �Y�S�إ߹L��� (col_x, col_z) �� ChunkColumn�A�h���ͨå[�J�޲z
            else
            {
                GameObject chunk_obj;
                Vector3Int location;
                Chunk1 chunk;

                // �̧ǫإߦP�@�� ChunkColumn �̭��� Chunk
                for (int y = 0; y < world_dimesions.y; y++)
                {
                    // y: Chunk �����ޭ�
                    // y * chunk_dimensions.y: Chunk ����ڮy��(�M���ޭȮt�F chunk_dimensions.y ��)
                    location = new Vector3Int(col_x, y * chunk_dimensions.y, col_z);

                    // ���� Chunk ����
                    chunk_obj = Instantiate(chunk_prefab);
                    chunk_obj.name = $"Chunk_{location.x}_{location.y}_{location.z}";

                    chunk = chunk_obj.GetComponent<Chunk1>();
                    chunk.build(dimensions: chunk_dimensions, location: location);

                    chunks.Add(location, chunk);
                }

                chunk_columns.Add(new Vector2Int(col_x, col_z));
            }            
        }

        /// <summary>
        /// �̧ǰ��� task_queue ��������
        /// Coordinator: ��խ�
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
                    displayChunkColumn(column_position.x, column_position.y, enabled: false);
                }
            }

            yield return null;
        }

        void displayChunkColumn(int col_x, int col_z, bool enabled = true)
        {
            Vector3Int pos;

            for (int y = 0; y < world_dimesions.y; y++)
            {
                // Chunk position
                pos = new Vector3Int(col_x, y * chunk_dimensions.y, col_z);

                if (chunks.ContainsKey(pos))
                {
                    chunks[pos].mesh_renderer.enabled = enabled;
                }
            }
        }
    }
}
