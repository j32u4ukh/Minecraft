using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace minecraft
{
    public class Chunk : MonoBehaviour
    {
        public Material solid_material;
        public Material fluid_material;

        public int WIDTH = 2;
        public int HEIGHT = 2;
        public int DEPTH = 2;

        // n_block = WIDTH * HEIGHT * DEPTH
        int n_block;

        public Block[,,] blocks;

        // 將三維的 blocks 的 BlockType 攤平成一個陣列，可加快存取速度
        public BlockType[] block_types;

        // 將三維的 blocks 的 CrackState 攤平成一個陣列，可加快存取速度
        public CrackState[] crack_states;

        public Vector3Int location;

        private MeshRenderer solid_mesh_renderer;
        private GameObject solid_mesh_obj = null;

        private MeshRenderer fluid_mesh_renderer;
        private GameObject fluid_mesh_obj = null;

        #region 管理六邊鄰居的資訊，不再每次有需求都要問一次鄰居有誰
        private bool met_neighbors = false;
        private (Chunk up, Chunk down, Chunk left, Chunk right, Chunk forward, Chunk back) neighbors;
        #endregion

        // For HealBlock
        private WaitForSeconds heal_block_buffer = new WaitForSeconds(3.0f);

        (Vector3Int, BlockType)[] tree_design = new (Vector3Int, BlockType)[] {
            (new Vector3Int(-1,2,-1), BlockType.LEAVES),
            (new Vector3Int(0,2,-1), BlockType.LEAVES),
            (new Vector3Int(0,3,-1), BlockType.LEAVES),
            (new Vector3Int(1,3,-1), BlockType.LEAVES),
            (new Vector3Int(-1,4,-1), BlockType.LEAVES),
            (new Vector3Int(0,4,-1), BlockType.LEAVES),
            (new Vector3Int(0,5,-1), BlockType.LEAVES),
            (new Vector3Int(0,0,0), BlockType.WOOD),
            (new Vector3Int(0,1,0), BlockType.WOOD),
            (new Vector3Int(-1,2,0), BlockType.LEAVES),
            (new Vector3Int(0,2,0), BlockType.WOOD),
            (new Vector3Int(1,2,0), BlockType.LEAVES),
            (new Vector3Int(-1,3,0), BlockType.LEAVES),
            (new Vector3Int(0,3,0), BlockType.WOOD),
            (new Vector3Int(1,3,0), BlockType.LEAVES),
            (new Vector3Int(-1,4,0), BlockType.LEAVES),
            (new Vector3Int(0,4,0), BlockType.WOOD),
            (new Vector3Int(1,4,0), BlockType.LEAVES),
            (new Vector3Int(-1,5,0), BlockType.LEAVES),
            (new Vector3Int(0,5,0), BlockType.LEAVES),
            (new Vector3Int(1,5,0), BlockType.LEAVES),
            (new Vector3Int(0,2,1), BlockType.LEAVES),
            (new Vector3Int(1,2,1), BlockType.LEAVES),
            (new Vector3Int(-1,3,1), BlockType.LEAVES),
            (new Vector3Int(0,3,1), BlockType.LEAVES),
            (new Vector3Int(0,4,1), BlockType.LEAVES),
            (new Vector3Int(1,4,1), BlockType.LEAVES),
            (new Vector3Int(0,5,1), BlockType.LEAVES)
        };

        (Vector3Int, BlockType)[] cactus_design = new (Vector3Int, BlockType)[] {
            (new Vector3Int(0,0,0), BlockType.CACTUS),
            (new Vector3Int(0,1,0), BlockType.CACTUS),
            (new Vector3Int(-2,2,0), BlockType.CACTUS),
            (new Vector3Int(-1,2,0), BlockType.CACTUS),
            (new Vector3Int(0,2,0), BlockType.CACTUS),
            (new Vector3Int(-2,3,0), BlockType.CACTUS),
            (new Vector3Int(0,3,0), BlockType.CACTUS),
            (new Vector3Int(1,3,0), BlockType.CACTUS),
            (new Vector3Int(2,3,0), BlockType.CACTUS),
            (new Vector3Int(-2,4,0), BlockType.CACTUS),
            (new Vector3Int(0,4,0), BlockType.CACTUS),
            (new Vector3Int(2,4,0), BlockType.CACTUS),
            (new Vector3Int(0,5,0), BlockType.CACTUS)
        };

        #region Chunk 初始化
        /// <summary>
        /// 決定 block_types 和 crack_states 的內容，尚未實際建構 Block
        /// </summary>
        /// <param name="dimensions"></param>
        /// <param name="location"></param>
        public void init(Vector3Int dimensions, Vector3Int location)
        {
            locate(dimensions, location);

            block_types = new BlockType[n_block];
            crack_states = new CrackState[n_block];

            NativeArray<BlockType> block_type_array = new NativeArray<BlockType>(block_types, Allocator.Persistent);
            NativeArray<CrackState> crack_state_array = new NativeArray<CrackState>(crack_states, Allocator.Persistent);

            Unity.Mathematics.Random[] randoms = new Unity.Mathematics.Random[n_block];
            System.Random seed = new System.Random();

            for (int i = 0; i < n_block; i++)
            {
                randoms[i] = new Unity.Mathematics.Random((uint)seed.Next());
            }

            NativeArray<Unity.Mathematics.Random> random_array = new NativeArray<Unity.Mathematics.Random>(randoms, Allocator.Persistent);

            DefineBlockJob job = new DefineBlockJob()
            {
                block_types = block_type_array,
                crack_states = crack_state_array,
                randoms = random_array,

                width = WIDTH,
                height = HEIGHT,
                location = location
            };

            JobHandle handle = job.Schedule(n_block, 64);

            // Schedule 執行完才會執行這一行，若不加 jobHandle.Complete()，則會在背景繼續執行，也執行下方程式碼
            handle.Complete();

            job.block_types.CopyTo(block_types);
            job.crack_states.CopyTo(crack_states);

            block_type_array.Dispose();
            crack_state_array.Dispose();
            random_array.Dispose();
        }

        /// <summary>
        /// 尺寸參數初始化
        /// </summary>
        /// <param name="dimensions">長寬高尺寸</param>
        /// <param name="location">世界座標</param>
        public void locate(Vector3Int dimensions, Vector3Int location)
        {
            this.location = location;

            WIDTH = dimensions.x;
            HEIGHT = dimensions.y;
            DEPTH = dimensions.z;
            blocks = new Block[WIDTH, HEIGHT, DEPTH];
            n_block = WIDTH * HEIGHT * DEPTH;
        }
        #endregion

        #region 建構 Chunk Mesh (事先設置六面鄰居，省略每次詢問鄰居有誰的流程)
        public void build()
        {
            buildMesh(obj: ref solid_mesh_obj, mesh_type: "Solid");
            buildMesh(obj: ref fluid_mesh_obj, mesh_type: "Fluid");
        }

        private void buildMesh(ref GameObject obj, string mesh_type = "Solid")
        {
            //if (!met_neighbors)
            //{
            //    return;
            //}

            // TODO: 改為全域變數，避免重複 GetComponent
            MeshFilter mesh_filter;

            if (obj == null)
            {
                obj = new GameObject(mesh_type);
                obj.transform.parent = transform;

                // 當 Chunk 下的 Block 發生變化，需要重繪 Chunk 時這些 Component 會被刪除，因此每次都需要重新添加
                mesh_filter = obj.AddComponent<MeshFilter>();

                switch (mesh_type)
                {
                    case "Solid":
                        solid_mesh_renderer = obj.AddComponent<MeshRenderer>();
                        solid_mesh_renderer.material = solid_material;
                        break;

                    case "Fluid":
                        fluid_mesh_renderer = obj.AddComponent<MeshRenderer>();
                        fluid_mesh_renderer.material = fluid_material;

                        obj.AddComponent<UVScroller>();
                        obj.layer = LayerMask.NameToLayer("Water");
                        break;
                }
            }
            else
            {
                mesh_filter = obj.GetComponent<MeshFilter>();

                // 避免後面又重複添加 Collider
                DestroyImmediate(obj.GetComponent<Collider>());
            }

            List<Mesh> input_mesh_datas = new List<Mesh>();
            int vertex_index_offset = 0, triangle_index_offset = 0, idx = 0;
            int n_vertex, n_triangle, block_idx;
            Block block;
            bool condition0, condition1;

            ProcessMeshDataJob job = new ProcessMeshDataJob();
            job.vertex_index_offsets = new NativeArray<int>(n_block, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            job.triangle_index_offsets = new NativeArray<int>(n_block, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            Vector3Int xyz, offset;
            BlockType block_type;
            CrackState crack_state;

            for (block_idx = 0; block_idx < n_block; block_idx++)
            {
                xyz = Utils.flatToVector3Int(i: block_idx, width: WIDTH, height: HEIGHT);
                offset = xyz + location;
                block_type = block_types[block_idx];
                crack_state = crack_states[block_idx];

                block = new Block(block_type: block_type,
                                  crack_state: crack_state,
                                  offset: offset,
                                  chunk: this,
                                  up: neighbors.up,
                                  down: neighbors.down,
                                  left: neighbors.left,
                                  right: neighbors.right,
                                  forward: neighbors.forward,
                                  back: neighbors.back);

                blocks[xyz.x, xyz.y, xyz.z] = block;

                condition0 = block.mesh != null;
                condition1 = false;

                // 區分這裡是 Solid 還是 Fluid 
                switch (mesh_type)
                {
                    case "Solid":
                        condition1 = !MeshUtils.canSpread(block_type);
                        break;

                    case "Fluid":
                        condition1 = MeshUtils.canSpread(block_type);
                        break;
                }

                if (condition0 && condition1)
                {
                    input_mesh_datas.Add(block.mesh);

                    job.vertex_index_offsets[idx] = vertex_index_offset;
                    job.triangle_index_offsets[idx] = triangle_index_offset;

                    n_vertex = block.mesh.vertexCount;
                    n_triangle = (int)block.mesh.GetIndexCount(0);

                    vertex_index_offset += n_vertex;
                    triangle_index_offset += n_triangle;
                    idx++;
                }
            }

            #region Job
            // input_mesh_datas -> jobs.meshData -> jobs.outputMesh -> outputMeshData -> newMesh
            job.input_mesh_datas = Mesh.AcquireReadOnlyMeshData(input_mesh_datas);

            // Mesh.AllocateWritableMeshData 分配一個可寫的網格數據，然後通過 jobs 進行頂點操作，
            Mesh.MeshDataArray output_mesh_datas = Mesh.AllocateWritableMeshData(1);

            // inputMeshes -> jobs.meshData -> jobs.outputMesh -> outputMeshData -> newMesh
            job.output_mesh_data = output_mesh_datas[0];

            //jobs.output_mesh_data.SetIndexBufferParams(triangle_index_offset, IndexFormat.UInt32);
            job.setIndexBufferParams(n_triangle: triangle_index_offset);

            //jobs.output_mesh_data.SetVertexBufferParams(
            //    vertex_index_offset,
            //    new VertexAttributeDescriptor(VertexAttribute.Position, stream: 0),
            //    new VertexAttributeDescriptor(VertexAttribute.Normal, stream: 1),
            //    new VertexAttributeDescriptor(VertexAttribute.TexCoord0, stream: 2),
            //    new VertexAttributeDescriptor(VertexAttribute.TexCoord1, stream: 3));
            job.setVertexBufferParams(n_vertex: vertex_index_offset);

            /* 在正確的時間調用 Schedule 和 Complete
             * 一旦你擁有了一個 job 所需的數據，盡可能快地在 job 上調用 Schedule，在你需要它的執行結果之前不要調用 Complete。
             * 一個良好的實踐是調度一個你不需要等待的 job，同時它不會與當前正在運行的其他job產生競爭。
             * 舉例來說，如果你在一幀結束和下一幀開始之前擁有一段沒有其他 job 在運行的時間，並且可以接受一幀的延遲，你可以在一幀結束的時候調度一個 job，在下一幀中使用它的結果。
             * 或者，如果這個轉換時間已經被其他 job 占滿了，但是在一幀中有一大段未充分利用的時段，在這里調度你的 job 會更有效率。
             * 
             * job 擁有一個 Run 方法，你可以用它來替代 Schedule 從而讓主線程立刻執行這個 job。你可以使用它來達到調試目的。
             */
            JobHandle handle = job.Schedule(input_mesh_datas.Count, 4);
            Mesh mesh = new Mesh();
            mesh.name = $"Chunk_{location.x}_{location.y}_{location.z}";

            SubMeshDescriptor sm = new SubMeshDescriptor(0, triangle_index_offset, MeshTopology.Triangles);
            sm.firstVertex = 0;
            sm.vertexCount = vertex_index_offset;

            /* 調用 JobHandle.Complete 來重新獲得歸屬權
             * 在主線程重新使用數據前，追蹤數據的所有權需要依賴項都完成。只檢查 JobHandle.IsCompleted 是不夠的。
             * 你必須調用 JobHandle.Complete 來在主線程中重新獲取 NaitveContainer 類型的所有權。調用 Complete 同時會清理安全性系統中的狀態。
             * 不這樣做的話會造成內存泄漏。這個過程也在你每一幀都調度依賴於上一幀 job 的新 job 時被采用。
             * 
             * 在主線程中調用 Schedule 和 Complete
             * 你只能在主線程中調用 Schedule 和 Complete 方法。如果一個 job 需要依賴於另一個，使用 JobHandle 來處理依賴關系而不是嘗試在 job 中調度新的 job。 */
            handle.Complete();

            job.output_mesh_data.subMeshCount = 1;
            job.output_mesh_data.SetSubMesh(0, sm);

            // 通過 Mesh.ApplyAndDisposeWritableMeshData 接口賦值回 Mesh
            // inputMeshes -> jobs.meshData -> jobs.outputMesh -> outputMeshData -> newMesh
            Mesh.ApplyAndDisposeWritableMeshData(output_mesh_datas, new[] { mesh });

            job.input_mesh_datas.Dispose();
            job.vertex_index_offsets.Dispose();
            job.triangle_index_offsets.Dispose();
            #endregion

            mesh.RecalculateBounds();

            // 更新 mesh_filter 的 mesh
            mesh_filter.mesh = mesh;

            // 當 Chunk 下的 Block 發生變化，需要重繪 Chunk 時，MeshCollider 會被刪除，因此每次都需要重新添加
            MeshCollider collider = obj.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
        }

        /// <summary>
        /// 當 新增 或 破壞 方塊後，呼叫此函式，以重新繪製 Chunk
        /// </summary>
        public void rebuild()
        {
            DestroyImmediate(GetComponent<MeshFilter>());
            DestroyImmediate(GetComponent<MeshRenderer>());
            DestroyImmediate(GetComponent<Collider>());
            build();
        }
        #endregion

        #region 建構 Chunk Mesh (每次執行都需傳入六面鄰居)
        public void buildConsiderAround(Chunk up, Chunk down, Chunk left, Chunk right, Chunk forward, Chunk back)
        {
            buildMeshConsiderAround(up, down, left, right, forward, back, ref solid_mesh_obj, mesh_type: "Solid");
            buildMeshConsiderAround(up, down, left, right, forward, back, ref fluid_mesh_obj, mesh_type: "Fluid");
        }

        private void buildMeshConsiderAround(Chunk up, Chunk down, Chunk left, Chunk right, Chunk forward, Chunk back, ref GameObject obj, string mesh_type = "Solid")
        {
            // TODO: 改為全域變數，避免重複 GetComponent
            MeshFilter mesh_filter;

            if (obj == null)
            {
                obj = new GameObject(mesh_type);
                obj.transform.parent = transform;

                // 當 Chunk 下的 Block 發生變化，需要重繪 Chunk 時這些 Component 會被刪除，因此每次都需要重新添加
                mesh_filter = obj.AddComponent<MeshFilter>();

                switch (mesh_type)
                {
                    case "Solid":
                        solid_mesh_renderer = obj.AddComponent<MeshRenderer>();
                        solid_mesh_renderer.material = solid_material;
                        break;

                    case "Fluid":
                        fluid_mesh_renderer = obj.AddComponent<MeshRenderer>();
                        fluid_mesh_renderer.material = fluid_material;

                        obj.AddComponent<UVScroller>();
                        obj.layer = LayerMask.NameToLayer("Water");
                        break;
                }
            }
            else
            {
                mesh_filter = obj.GetComponent<MeshFilter>();

                // 避免後面又重複添加 Collider
                DestroyImmediate(obj.GetComponent<Collider>());
            }

            List<Mesh> input_mesh_datas = new List<Mesh>();
            int vertex_index_offset = 0, triangle_index_offset = 0, idx = 0;
            int n_vertex, n_triangle, block_idx;
            Block block;
            bool condition0, condition1;

            ProcessMeshDataJob job = new ProcessMeshDataJob();
            job.vertex_index_offsets = new NativeArray<int>(n_block, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            job.triangle_index_offsets = new NativeArray<int>(n_block, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            Vector3Int xyz, offset;
            BlockType block_type;
            CrackState crack_state;

            for (block_idx = 0; block_idx < n_block; block_idx++)
            {
                xyz = Utils.flatToVector3Int(i: block_idx, width: WIDTH, height: HEIGHT);
                offset = xyz + location;
                block_type = block_types[block_idx];
                crack_state = crack_states[block_idx];

                // Block(block_type, crack_state, offset, chunk, up, down, left, right, forward, back)
                block = new Block(block_type: block_type,
                                  crack_state: crack_state,
                                  offset: offset,
                                  chunk: this,
                                  up: up,
                                  down: down,
                                  left: left,
                                  right: right,
                                  forward: forward,
                                  back: back);

                blocks[xyz.x, xyz.y, xyz.z] = block;

                condition0 = block.mesh != null;
                condition1 = false;

                // 區分這裡是 Solid 還是 Fluid 
                switch (mesh_type)
                {
                    case "Solid":
                        condition1 = !MeshUtils.canSpread(block_type);
                        break;

                    case "Fluid":
                        condition1 = MeshUtils.canSpread(block_type);
                        break;
                }

                if (condition0 && condition1)
                {
                    input_mesh_datas.Add(block.mesh);

                    job.vertex_index_offsets[idx] = vertex_index_offset;
                    job.triangle_index_offsets[idx] = triangle_index_offset;

                    n_vertex = block.mesh.vertexCount;
                    n_triangle = (int)block.mesh.GetIndexCount(0);

                    vertex_index_offset += n_vertex;
                    triangle_index_offset += n_triangle;
                    idx++;
                }
            }

            #region Job
            // input_mesh_datas -> jobs.meshData -> jobs.outputMesh -> outputMeshData -> newMesh
            job.input_mesh_datas = Mesh.AcquireReadOnlyMeshData(input_mesh_datas);

            // Mesh.AllocateWritableMeshData 分配一個可寫的網格數據，然後通過 jobs 進行頂點操作，
            Mesh.MeshDataArray output_mesh_datas = Mesh.AllocateWritableMeshData(1);

            // inputMeshes -> jobs.meshData -> jobs.outputMesh -> outputMeshData -> newMesh
            job.output_mesh_data = output_mesh_datas[0];

            //jobs.output_mesh_data.SetIndexBufferParams(triangle_index_offset, IndexFormat.UInt32);
            job.setIndexBufferParams(n_triangle: triangle_index_offset);

            //jobs.output_mesh_data.SetVertexBufferParams(
            //    vertex_index_offset,
            //    new VertexAttributeDescriptor(VertexAttribute.Position, stream: 0),
            //    new VertexAttributeDescriptor(VertexAttribute.Normal, stream: 1),
            //    new VertexAttributeDescriptor(VertexAttribute.TexCoord0, stream: 2),
            //    new VertexAttributeDescriptor(VertexAttribute.TexCoord1, stream: 3));
            job.setVertexBufferParams(n_vertex: vertex_index_offset);

            /* 在正確的時間調用 Schedule 和 Complete
             * 一旦你擁有了一個 job 所需的數據，盡可能快地在 job 上調用 Schedule，在你需要它的執行結果之前不要調用 Complete。
             * 一個良好的實踐是調度一個你不需要等待的 job，同時它不會與當前正在運行的其他job產生競爭。
             * 舉例來說，如果你在一幀結束和下一幀開始之前擁有一段沒有其他 job 在運行的時間，並且可以接受一幀的延遲，你可以在一幀結束的時候調度一個 job，在下一幀中使用它的結果。
             * 或者，如果這個轉換時間已經被其他 job 占滿了，但是在一幀中有一大段未充分利用的時段，在這里調度你的 job 會更有效率。
             * 
             * job 擁有一個 Run 方法，你可以用它來替代 Schedule 從而讓主線程立刻執行這個 job。你可以使用它來達到調試目的。
             */
            JobHandle handle = job.Schedule(input_mesh_datas.Count, 4);
            Mesh mesh = new Mesh();
            mesh.name = $"Chunk_{location.x}_{location.y}_{location.z}";

            SubMeshDescriptor sm = new SubMeshDescriptor(0, triangle_index_offset, MeshTopology.Triangles);
            sm.firstVertex = 0;
            sm.vertexCount = vertex_index_offset;

            /* 調用 JobHandle.Complete 來重新獲得歸屬權
             * 在主線程重新使用數據前，追蹤數據的所有權需要依賴項都完成。只檢查 JobHandle.IsCompleted 是不夠的。
             * 你必須調用 JobHandle.Complete 來在主線程中重新獲取 NaitveContainer 類型的所有權。調用 Complete 同時會清理安全性系統中的狀態。
             * 不這樣做的話會造成內存泄漏。這個過程也在你每一幀都調度依賴於上一幀 job 的新 job 時被采用。
             * 
             * 在主線程中調用 Schedule 和 Complete
             * 你只能在主線程中調用 Schedule 和 Complete 方法。如果一個 job 需要依賴於另一個，使用 JobHandle 來處理依賴關系而不是嘗試在 job 中調度新的 job。
             * 
             * 
             */
            handle.Complete();

            job.output_mesh_data.subMeshCount = 1;
            job.output_mesh_data.SetSubMesh(0, sm);

            // 通過 Mesh.ApplyAndDisposeWritableMeshData 接口賦值回 Mesh
            // inputMeshes -> jobs.meshData -> jobs.outputMesh -> outputMeshData -> newMesh
            Mesh.ApplyAndDisposeWritableMeshData(output_mesh_datas, new[] { mesh });

            job.input_mesh_datas.Dispose();
            job.vertex_index_offsets.Dispose();
            job.triangle_index_offsets.Dispose();
            #endregion

            mesh.RecalculateBounds();

            // 更新 mesh_filter 的 mesh
            mesh_filter.mesh = mesh;

            // 當 Chunk 下的 Block 發生變化，需要重繪 Chunk 時，MeshCollider 會被刪除，因此每次都需要重新添加
            MeshCollider collider = obj.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
        }

        /// <summary>
        /// 當 新增 或 破壞 方塊後，呼叫此函式，以重新繪製 Chunk
        /// </summary>
        public void rebuildConsiderAround(Chunk up, Chunk down, Chunk left, Chunk right, Chunk forward, Chunk back)
        {
            DestroyImmediate(GetComponent<MeshFilter>());
            DestroyImmediate(GetComponent<MeshRenderer>());
            DestroyImmediate(GetComponent<Collider>());
            buildConsiderAround(up, down, left, right, forward, back);
        } 
        #endregion

        #region Chunk & Block
        public (Vector3Int, Vector3Int) getChunkBlockLocation(Vector3Int block_position)
        {
            return getChunkBlockLocation(bx: block_position.x, by: block_position.y, bz: block_position.z);
        }

        public (Vector3Int, Vector3Int) getChunkBlockLocationAdvanced(Vector3Int block_position)
        {
            return getChunkBlockLocationAdvanced(bx: block_position.x, by: block_position.y, bz: block_position.z);
        }

        /// <summary>
        /// 當點擊方塊所屬 Chunk，和目標方塊所屬 Chunk 不同時，方塊位置的座標會發生索引值超出。
        /// 處理 Chunk 邊界對 Block 索引值的處理，當超出當前 Chunk 時，指向下一個 Chunk 並修正 Block 索引值。
        /// NOTE: 這裡未考慮到 世界 的大小
        /// </summary>
        /// <param name="bx">目標方塊位置的 X 座標</param>
        /// <param name="by">目標方塊位置的 Y 座標</param>
        /// <param name="bz">目標方塊位置的 Z 座標</param>
        /// <returns>(已校正 chunk 位置, 已校正 block 索引值)</returns>
        public (Vector3Int, Vector3Int) getChunkBlockLocation(int bx, int by, int bz)
        {
            Vector3Int chunk_location = new Vector3Int(location.x, location.y, location.z);

            if (bx == WIDTH)
            {
                chunk_location.x += WIDTH;
                bx = 0;
            }
            else if (bx == -1)
            {
                chunk_location.x -= WIDTH;
                bx = WIDTH - 1;
            }
            else if (by == HEIGHT)
            {
                chunk_location.y += HEIGHT;
                by = 0;
            }
            else if (by == -1)
            {
                chunk_location.y -= HEIGHT;
                by = HEIGHT - 1;
            }
            else if (bz == DEPTH)
            {
                chunk_location.z += DEPTH;
                bz = 0;
            }
            else if (bz == -1)
            {
                chunk_location.z -= DEPTH;
                bz = DEPTH - 1;
            }

            return (chunk_location, new Vector3Int(bx, by, bz));
        }

        /// <summary>
        /// getChunkBlockLocation 只考慮放置方塊的情形，因此一次只會有一個方向超出 Chunk 的邊界
        /// 而 getChunkBlockLocationAdvanced 使用於程式添加環境，一次會有多個方向超出 Chunk 的邊界，XYZ 三個方向都須考慮
        /// 目前僅考慮超出一個 Chunk 的情況，尚未考慮跨兩個或以上 Chunk 的情況
        /// </summary>
        /// <param name="bx">目標方塊位置的 X 座標</param>
        /// <param name="by">目標方塊位置的 Y 座標</param>
        /// <param name="bz">目標方塊位置的 Z 座標</param>
        /// <returns>(已校正 chunk 位置, 已校正 block 索引值)</returns>
        public (Vector3Int, Vector3Int) getChunkBlockLocationAdvanced(int bx, int by, int bz)
        {
            Vector3Int chunk_location = new Vector3Int(location.x, location.y, location.z);

            if (bx >= WIDTH)
            {
                chunk_location.x += WIDTH;
                bx -= WIDTH;
            }
            else if (bx <= -1)
            {
                chunk_location.x -= WIDTH;
                bx += WIDTH;
            }

            if (by >= HEIGHT)
            {
                chunk_location.y += HEIGHT;
                by -= HEIGHT;
            }
            else if (by <= -1)
            {
                chunk_location.y -= HEIGHT;
                by += HEIGHT;
            }

            if (bz >= DEPTH)
            {
                chunk_location.z += DEPTH;
                bz -= DEPTH;
            }
            else if (bz <= -1)
            {
                chunk_location.z -= DEPTH;
                bz += DEPTH;
            }

            return (chunk_location, new Vector3Int(bx, by, bz));
        } 
        #endregion

        #region Block
        public void setBlockType(int index, BlockType block_type)
        {
            block_types[index] = block_type;
        }

        public BlockType getBlockType(int index)
        {
            return block_types[index];
        }

        public void setCrackState(int index, CrackState crack_state = CrackState.None)
        {
            crack_states[index] = crack_state;
        }

        public CrackState getCrackState(int index)
        {
            return crack_states[index];
        }

        public void placeBlock(int index, BlockType block_type)
        {
            try
            {
                block_types[index] = block_type;
                crack_states[index] = CrackState.None;
            }
            catch (IndexOutOfRangeException)
            {
                Vector3Int v = flatToVector3Int(index);
                Debug.LogError($"{index}/{block_types.Length}; {v}/({WIDTH}, {HEIGHT}, {DEPTH})");
            }
        }

        /// <summary>
        /// 敲擊某塊方塊時，累加破壞程度
        /// 若破壞程度與方塊強度相當，才會真的破壞掉
        /// </summary>
        /// <param name="index">方塊索引值</param>
        /// <returns>該方塊是否被破壞</returns>
        public bool crackBlock(int index)
        {
            // 若無法破壞，直接返回
            if (!isCrackable(index))
            {
                return false;
            }

            // 第一次敲擊時觸發，一段時間後檢查是否已被敲掉，否則修復自己 crack_state 恢復成 CrackState.None
            if (crack_states[index].Equals(CrackState.None))
            {
                StartCoroutine(healBlock(index));
            }

            // 累加破壞程度
            crack_states[index]++;

            // 若 破壞程度 與 方塊強度 相當
            if (isCracked(index))
            {
                // 實際破壞該方塊
                block_types[index] = BlockType.AIR;
                crack_states[index] = CrackState.None;

                return true;
            }

            return false;
        }

        /// <summary>
        /// 破壞程度(CrackState) 與 方塊強度(Strenth) 相當，才能真的破壞掉
        /// </summary>
        /// <param name="index">方塊索引值</param>
        /// <returns>是否被破壞了</returns>
        public bool isCracked(int index)
        {
            return crack_states[index].Equals((CrackState)MeshUtils.getStrenth(block_types[index]));
        }

        /// <summary>
        /// 若為基岩等類型的方塊，強度設置為 -1，表示無法破壞。
        /// 其他的則無限制。
        /// </summary>
        /// <param name="index"></param>
        /// <returns>該方塊是否可以破壞</returns>
        public bool isCrackable(int index)
        {
            return MeshUtils.getStrenth(block_types[index]) != -1;
        }

        // 一段時間後檢查是否已被敲掉，否則修復自己 health 恢復成 NOCRACK
        public IEnumerator healBlock(int index)
        {
            yield return heal_block_buffer;

            if (block_types[index] != BlockType.AIR)
            {
                crack_states[index] = CrackState.None;
                rebuild();
            }
        }

        public Vector3Int flatToVector3Int(int i)
        {
            return Utils.flatToVector3Int(i, width: WIDTH, height: HEIGHT);
        } 
        #endregion

        #region 管理六邊鄰居的資訊
        public bool hasMetNeighbors()
        {
            return met_neighbors;
        }

        /// <summary>
        /// 設置六面鄰居，通常會先檢查是否設置過，若設置過就會省略此步驟。
        /// 若之後 Chunk 會動態生成，那就需要確保六面鄰居都存在，才不繼續更新鄰居
        /// </summary>
        /// <param name="neighbors"></param>
        public void setNeighbors((Chunk up, Chunk down, Chunk left, Chunk right, Chunk forward, Chunk back) neighbors)
        {
            if(neighbors.up && neighbors.down && neighbors.left && neighbors.right && neighbors.forward && neighbors.back)
            {
                met_neighbors = true;
            }

            this.neighbors = neighbors;
        }
        #endregion

        public IEnumerable<(Vector3Int, BlockType)> iterVegetations()
        {
            int n_block = block_types.Length;
            Vector3Int block_pos, base_pos;

            for (int i = 0; i < n_block; i++)
            {
                if (block_types[i] == BlockType.WOODBASE)
                {
                    base_pos = flatToVector3Int(i);

                    foreach ((Vector3Int, BlockType) tree in tree_design)
                    {
                        block_pos = base_pos + tree.Item1;
                        yield return (block_pos, tree.Item2);
                    }
                }
                else if (block_types[i] == BlockType.CACTUSBASE)
                {
                    base_pos = flatToVector3Int(i);

                    foreach ((Vector3Int, BlockType) cactus in cactus_design)
                    {
                        block_pos = base_pos + cactus.Item1;
                        yield return (block_pos, cactus.Item2);
                    }
                }
            }
        }

        public void setVisiable(bool visiable)
        {
            solid_mesh_renderer.enabled = visiable;
            fluid_mesh_renderer.enabled = visiable;
        }

        /// <summary>
        /// mesh_renderer_solid 和 mesh_renderer_fluid 是否可見的狀態相同，因此返回任一個的狀態即可
        /// </summary>
        /// <returns></returns>
        public bool isVisiable()
        {
            return solid_mesh_renderer.enabled;
        }


        [Obsolete("提供在 Chunk 中建立 Block 時，協助判斷各面 Mesh 是否需要添加")]
        (bool is_inside, int nx, int ny, int nz) getNeighbourInfo(int bx, int by, int bz, BlockSide side)
        {
            bool is_inside = true;

            switch (side)
            {
                case BlockSide.Right:
                    bx += 1;

                    if (bx >= WIDTH)
                    {
                        is_inside = false;
                        bx = 0;
                    }
                    break;

                case BlockSide.Left:
                    bx -= 1;
                    is_inside = bx >= 0;

                    if (bx < 0)
                    {
                        is_inside = false;
                        bx = WIDTH - 1;
                    }
                    break;

                case BlockSide.Top:
                    by += 1;

                    if (by >= HEIGHT)
                    {
                        is_inside = false;
                        by = 0;
                    }
                    break;

                case BlockSide.Bottom:
                    by -= 1;

                    if (by < 0)
                    {
                        is_inside = false;
                        by = HEIGHT - 1;
                    }
                    break;

                case BlockSide.Front:
                    bz += 1;

                    if (bz >= DEPTH)
                    {
                        is_inside = false;
                        bz = 0;
                    }
                    break;

                case BlockSide.Back:
                    bz -= 1;

                    if (bz < 0)
                    {
                        is_inside = false;
                        bz = DEPTH - 1;
                    }
                    break;
            }

            return (is_inside, bx, by, bz);
        }

        [Obsolete("提供在 Chunk 中建立 Block 時，協助判斷各面 Mesh 是否需要添加")]
        bool hasInsideNeighbour(int x, int y, int z, BlockType block_type)
        {
            int block_idx = Utils.xyzToFlat(x, y, z, width: WIDTH, depth: DEPTH);

            if (getBlockType(block_idx).Equals(block_type))
            {
                return true;
            }

            if (getBlockType(block_idx).Equals(BlockType.AIR) || getBlockType(block_idx).Equals(BlockType.WATER))
            {
                return false;
            }

            return true;
        }

        [Obsolete("提供在 Chunk 中建立 Block 時，協助判斷各面 Mesh 是否需要添加")]
        bool hasNeighbour(Chunk chunk, int x, int y, int z, BlockType block_type)
        {
            int block_idx = Utils.xyzToFlat(x, y, z, width: WIDTH, depth: DEPTH);

            if (chunk.getBlockType(block_idx).Equals(block_type))
            {
                return true;
            }

            if (chunk.getBlockType(block_idx).Equals(BlockType.AIR) || chunk.getBlockType(block_idx).Equals(BlockType.WATER))
            {
                return false;
            }

            return true;
        }
    }

    /* 所有的 job 都會是 struct，並根據需要繼承不同的interface
     * 
     * 一個並行化 job 使用一個 NativeArray 存放數據來作為它的數據源。並行化 job 橫跨多個核心執行。每個核心上有一個 job，每個 job 處理一部分工作量。
     * IJobParallelFor 的行為很類似於 IJob，但是不同於只執行一個 Execute 方法，它會在數據源的每一項上執行 Execute 方法。Execute 方法中有一個整數型的參數。
     * 這個索引是為了在 job 的具體操作實現中訪問和操作數據源上的單個元素。
     * 
     * 當調度並行化 job 時，你必須指定你分割 NativeArray 數據源的長度。在結構中同時存在多個 NativeArrayUnity 時，C# Job System 不知道你要使用哪一個 NativeArray 作為數據源。
     * 這個長度同時會告知 C# Job System 有多少個 Execute 方法會被執行。
     * 在這個場景中，並行化 job 的調度會更複雜。當調度並行化任務時，C# Job System 會將工作分成多個批次，分發給不同的核心來處理。每一個批次都包含一部分的 Execute 方法。
     * 隨後 C# Job System 會在每個 CPU 核心的 Unity 原生 Job System 上調度最多一個 job，並傳遞給這個 job 一些批次的工作來完成。
     * 
     * 當一個原生 job 提前完成了分配給它的工作批次後，它會從其他原生 job 那里獲取其剩余的工作批次。它每次只獲取那個原生 job 剩余批次的一半，為了確保緩存局部性(cache locality)。
     * 為了優化這個過程，你需要指定一個每批次數量(batch count)。這個每批次數量控制了你會生成多少 job 和線程中進行任務分發的粒度。
     * 使用一個較低的每批次數量，比如 1，會使你在線程之間的工作分配更平均。它會帶來一些額外的開銷，所以有時增加每批次數量會是更好的選擇。
     * 從每批次數量為 1 開始，然後慢慢增加這個數量直到性能不再提升是一個合理的策略。
     * 
     * 不要在 job 中開辟托管內存
     * 在 job 中開辟托管內存會難以置信得慢，並且這個 job 不能利用 Unity 的 Burst 編譯器來提升性能。
     * Burst 是一個新的基於 LLVM 的後端編譯器技術，它會使事情對於你更加簡單。它獲取 C# job 並利用你平台的特定功能產生高度優化的機器碼。
     * 參考：https://zhuanlan.zhihu.com/p/58125078
     */

    // DefineBlockJob：根據海拔與位置等資訊，決定 Block 的類型與位置。再交由 ProcessMeshDataJob 處理如何呈現。
    struct DefineBlockJob : IJobParallelFor
    {
        public NativeArray<BlockType> block_types;
        public NativeArray<CrackState> crack_states;

        // TODO: 原本每次開起的隨機數都會相同，是因為給 Unity.Mathematics.Random 的 seed 都是 1，因此只須傳入隨機的 seed，並在 Execute(int i) 外部建立 Unity.Mathematics.Random 物件即可
        public NativeArray<Unity.Mathematics.Random> randoms;

        public int width;
        public int height;
        public Vector3Int location;

        Vector3Int xyz;
        Unity.Mathematics.Random random;
        int surface_height, stone_height, diamond_top_height, diamond_bottom_height;
        int dig_cave, plant_tree, dessert_biome;

        public void Execute(int i)
        {
            xyz = Utils.flatToVector3Int(i, width, height) + location;
            random = randoms[i];

            surface_height = (int)Strata.fBM(x: xyz.x, z: xyz.z,
                                             octaves: World.surface_strata.octaves,
                                             scale: World.surface_strata.scale,
                                             height_scale: World.surface_strata.height_scale,
                                             height_offset: World.surface_strata.height_offset);

            stone_height = (int)Strata.fBM(x: xyz.x, z: xyz.z,
                                           octaves: World.stone_strata.octaves,
                                           scale: World.stone_strata.scale,
                                           height_scale: World.stone_strata.height_scale,
                                           height_offset: World.stone_strata.height_offset);

            diamond_top_height = (int)Strata.fBM(x: xyz.x, z: xyz.z,
                                                 octaves: World.diamond_top_strata.octaves,
                                                 scale: World.diamond_top_strata.scale,
                                                 height_scale: World.diamond_top_strata.height_scale,
                                                 height_offset: World.diamond_top_strata.height_offset);

            diamond_bottom_height = (int)Strata.fBM(x: xyz.x, z: xyz.z,
                                                    octaves: World.diamond_bottom_strata.octaves,
                                                    scale: World.diamond_bottom_strata.scale,
                                                    height_scale: World.diamond_bottom_strata.height_scale,
                                                    height_offset: World.diamond_bottom_strata.height_offset);

            dig_cave = (int)Cluster.fBM3D(x: xyz.x, y: xyz.y, z: xyz.z,
                                          octaves: World.cave_cluster.octaves,
                                          scale: World.cave_cluster.scale,
                                          height_scale: World.cave_cluster.height_scale,
                                          height_offset: World.cave_cluster.height_offset);


            plant_tree = (int)Cluster.fBM3D(x: xyz.x, y: xyz.y, z: xyz.z,
                                            octaves: World.tree_cluster.octaves,
                                            scale: World.tree_cluster.scale,
                                            height_scale: World.tree_cluster.height_scale,
                                            height_offset: World.tree_cluster.height_offset);


            dessert_biome = (int)Cluster.fBM3D(x: xyz.x, y: xyz.y, z: xyz.z,
                                               octaves: World.biome_cluster.octaves,
                                               scale: World.biome_cluster.scale,
                                               height_scale: World.biome_cluster.height_scale,
                                               height_offset: World.biome_cluster.height_offset);


            /* 目前在水線下的高度放了一個 Post-process Volume，定義攝影機進入該區域的效果(看起來藍藍的、能見度很低)，
             * 並利用 WaterManager 在水平方向追蹤玩家。即，不管從哪裡下降到水線以下的區域都會看起來像在水中，
             * 即便現在不在水中。 這個做法必須改掉，因為它不但水線以下都附加該效果，水線的定位方式(要自己算形成的世界有多高)也不是很理想 */
            int WATER_LINE = 20;

            crack_states[i] = CrackState.None;

            if (xyz.y == 0)
            {
                block_types[i] = BlockType.BEDROCK;
                return;
            }

            // TODO: 目前的洞穴可能會挖到地表，且因沒有考慮到是否是地表，因而造成地表為泥土而非草地
            if (dig_cave < World.cave_cluster.boundary)
            {
                block_types[i] = BlockType.AIR;
                return;
            }

            // NOTE: 地表種植植物時，會先設置一個特殊 BlockType 標註，實際種植時也會將該特殊 BlockType 覆蓋，可避免重複認定需要種樹
            if (xyz.y == surface_height && xyz.y >= WATER_LINE)
            {
                if (dessert_biome < World.biome_cluster.boundary)
                {
                    block_types[i] = BlockType.SAND;

                    if (random.NextFloat(1) <= 0.01f)
                    {
                        block_types[i] = BlockType.CACTUSBASE;
                    }
                }
                else if (plant_tree < World.tree_cluster.boundary)
                {
                    block_types[i] = BlockType.FOREST;

                    // TODO: 樹出現的密度(機率)應由外部設置
                    if (random.NextFloat(1) <= 0.05f)
                    {
                        // Execute 當中一次處理一個 Block，因此這裡僅放置樹基，而非直接種一棵樹
                        block_types[i] = BlockType.WOODBASE;
                    }
                }
                else
                {
                    block_types[i] = BlockType.GRASSSIDE;
                }
            }

            else if ((diamond_bottom_height < xyz.y) && (xyz.y < diamond_top_height) && (random.NextFloat(1) <= World.diamond_top_strata.probability))
            {
                block_types[i] = BlockType.DIAMOND;
            }

            else if ((xyz.y < stone_height) && (random.NextFloat(1) <= World.stone_strata.probability))
            {
                block_types[i] = BlockType.STONE;
            }

            else if (xyz.y < surface_height)
            {
                block_types[i] = BlockType.DIRT;
            }

            // TODO: 實際數值要根據地形高低來做調整
            // TODO: 如何確保水是自己一個區塊，而非隨機的散佈在地圖中？大概要像樹一樣，使用 fBM3D
            else if (xyz.y < WATER_LINE)
            {
                block_types[i] = BlockType.WATER;
            }

            else
            {
                block_types[i] = BlockType.AIR;
            }
        }
    }


    // ProcessMeshDataJob：根據 Block 的類型與位置等，計算所需貼圖與位置
    // ProcessMeshDataJob 用於將多個 Mesh 合併為單一個 Mesh，作用同 MeshUtils.mergeMeshes，但是使用了 Job System 會更有效率
    // BurstCompile 需使用 .NET 4.0 以上
    [BurstCompile]
    struct ProcessMeshDataJob : IJobParallelFor
    {
        /* 將 NativeContainer 標記為只讀的
         * 記住 job 在默認情況下擁有 NativeContainer 的讀寫權限。在合適的 NativeContainer 上使用 [ReadOnly] 屬性可以提升性能。*/
        [ReadOnly] public Mesh.MeshDataArray input_mesh_datas;

        // ProcessMeshDataJob 將多個 MeshData 合併成一個，因此這裡是 MeshData 而非 MeshDataArray
        public Mesh.MeshData output_mesh_data;

        // 累加前面各個 MeshData 的 vertex 個數，告訴當前 vertex 數據要存入時，索引值的偏移量
        public NativeArray<int> vertex_index_offsets;

        // 累加前面各個 MeshData 的 triangle 個數，告訴當前 triangle 數據要存入時，索引值的偏移量
        public NativeArray<int> triangle_index_offsets;

        /// <summary>
        /// Called once per element
        /// </summary>
        /// <param name="index"></param>
        public void Execute(int index)
        {
            Mesh.MeshData data = input_mesh_datas[index];

            // 當前 vertex 數據要存入時，索引值的偏移量
            int vertex_index_offset = vertex_index_offsets[index];

            // 當前 vertex 數據個數
            int n_vertex = data.vertexCount;

            NativeArray<float3> current_vertices = new NativeArray<float3>(n_vertex, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            // 從 data 中取得 Vertices 放入 current_vertices，Reinterpret<Vector3> 讀入 Vector3，轉換成 float3
            data.GetVertices(current_vertices.Reinterpret<Vector3>());

            NativeArray<float3> current_normals = new NativeArray<float3>(n_vertex, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            data.GetNormals(current_normals.Reinterpret<Vector3>());

            // uv 本身雖是 Vector2，但在 Job System 中應使用 Vector3
            NativeArray<float3> current_uvs = new NativeArray<float3>(n_vertex, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            data.GetUVs(0, current_uvs.Reinterpret<Vector3>());

            NativeArray<float3> current_uv2s = new NativeArray<float3>(n_vertex, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            data.GetUVs(1, current_uv2s.Reinterpret<Vector3>());

            // 根據 SetVertexBufferParams 擺放順序，依序取得 0: Position, 1: Normal, 2: TexCoord0, 3: TexCoord1
            NativeArray<Vector3> vertices = output_mesh_data.GetVertexData<Vector3>(stream: 0);
            NativeArray<Vector3> normals = output_mesh_data.GetVertexData<Vector3>(stream: 1);
            NativeArray<Vector3> uvs = output_mesh_data.GetVertexData<Vector3>(stream: 2);
            NativeArray<Vector3> uv2s = output_mesh_data.GetVertexData<Vector3>(stream: 3);

            /* 利用 index 取得各個 MeshData，再分別取出 vertices, normals, uvs, uv2s，
             * 將數據存入同一個 NativeArray<Vector3>，利用 vertex_index 將各個 MeshData 的數據儲存到正確的位置
             * 
             */
            for (int i = 0; i < n_vertex; i++)
            {
                vertices[vertex_index_offset + i] = current_vertices[i];
                normals[vertex_index_offset + i] = current_normals[i];
                uvs[vertex_index_offset + i] = current_uvs[i];
                uv2s[vertex_index_offset + i] = current_uv2s[i];
            }

            /* NativeArray 使用後應呼叫 Dispose()，以避免記憶體溢出 */
            current_vertices.Dispose();
            current_normals.Dispose();
            current_uvs.Dispose();
            current_uv2s.Dispose();

            // 取得輸出數據中的三角形頂點索引值
            NativeArray<int> triangles = output_mesh_data.GetIndexData<int>();

            // 當前 triangle 數據要存入時，索引值的偏移量
            int triangle_index_offset = triangle_index_offsets[index];

            // 當前 triangle 數據個數
            int n_triangle = data.GetSubMesh(0).indexCount;

            int idx;

            // Android
            if (data.indexFormat == IndexFormat.UInt16)
            {
                NativeArray<ushort> indexs = data.GetIndexData<ushort>();

                for (int i = 0; i < n_triangle; ++i)
                {
                    idx = indexs[i];
                    triangles[i + triangle_index_offset] = vertex_index_offset + idx;
                }
            }

            // IndexFormat.UInt32: PC
            else
            {
                NativeArray<int> indexs = data.GetIndexData<int>();

                for (int i = 0; i < n_triangle; ++i)
                {
                    idx = indexs[i];
                    triangles[i + triangle_index_offset] = vertex_index_offset + idx;
                }
            }
        }

        /// <summary>
        /// 這裡的 stream 的順序，應和 ProcessMeshDataJob.Execute 當中 GetVertexData 的 stream 的順序相同
        /// 0: Position, 1: Normal, 2: TexCoord0, 3: TexCoord1
        /// </summary>
        /// <param name="n_vertex"></param>
        public void setVertexBufferParams(int n_vertex)
        {
            output_mesh_data.SetVertexBufferParams(
                n_vertex,
                new VertexAttributeDescriptor(VertexAttribute.Position, stream: 0),
                new VertexAttributeDescriptor(VertexAttribute.Normal, stream: 1),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, stream: 2),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord1, stream: 3));
        }

        public void setIndexBufferParams(int n_triangle)
        {
            output_mesh_data.SetIndexBufferParams(n_triangle, IndexFormat.UInt32);
        }
    }

}

