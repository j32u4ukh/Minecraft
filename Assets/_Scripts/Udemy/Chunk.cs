using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace udemy
{
    public class Chunk : MonoBehaviour
    {
        public Material atlas;

        public int width = 2;
        public int height = 2;
        public int depth = 2;

        public Block[,,] blocks;

        // 將三維的 blocks 的 BlockType 攤平成一個陣列，可加快存取速度
        public BlockType[] block_types;

        // 將三維的 blocks 的 CrackState 攤平成一個陣列，可加快存取速度
        //public CrackState[] crack_states;

        DefineBlockJob define_block_job;

        public Vector3Int location;

        public MeshRenderer mesh_renderer;

        public void build(Vector3Int dimensions, Vector3Int location)
        {
            MeshFilter filter = gameObject.AddComponent<MeshFilter>();
            mesh_renderer = gameObject.AddComponent<MeshRenderer>();
            mesh_renderer.material = atlas;

            this.location = location;
            width = dimensions.x;
            height = dimensions.y;
            depth = dimensions.z;
            blocks = new Block[width, height, depth];
            initChunk();
            int x, y, z;

            int n_mesh = width * height * depth;
            List<Mesh> input_mesh_datas = new List<Mesh>();
            int vertex_index_offset = 0, triangle_index_offset = 0, idx = 0;
            int n_vertex, n_triangle, block_idx;
            Block block;

            ProcessMeshDataJob job = new ProcessMeshDataJob();
            job.vertex_index_offsets = new NativeArray<int>(n_mesh, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            job.triangle_index_offsets = new NativeArray<int>(n_mesh, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            for (z = 0; z < depth; z++)
            {
                for (y = 0; y < height; y++)
                {
                    for (x = 0; x < width; x++)
                    {
                        //block_idx = x + width * (y + depth * z);
                        block_idx = Utils.xyzToFlat(x, y, z, width, depth);
                        block = new Block(block_type: block_types[block_idx],
                                          offset: new Vector3Int(x, y, z) + location,
                                          chunk: this);
                        blocks[x, y, z] = block;

                        if (block.mesh != null)
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
                }
            }

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
            mesh.RecalculateBounds();

            filter.mesh = mesh;

            MeshCollider collider = gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
        }

        void initChunk()
        {
            int n_block = width * depth * height;
            block_types = new BlockType[n_block];

            NativeArray<BlockType> block_type_array = new NativeArray<BlockType>(block_types, Allocator.Persistent);
            //NativeArray<CrackState> crack_state_array = new NativeArray<CrackState>(crack_states, Allocator.Persistent);

            //var randomArray = new Unity.Mathematics.Random[n_block];
            //var seed = new System.Random();

            //for (int i = 0; i < blockCount; i++)
            //{
            //    randomArray[i] = new Unity.Mathematics.Random((uint)seed.Next());
            //}

            //RandomArray = new NativeArray<Unity.Mathematics.Random>(randomArray, Allocator.Persistent);

            DefineBlockJob job = new DefineBlockJob()
            {
                block_types = block_type_array,
                width = width,
                height = height,
                location = location
            };

            JobHandle handle = job.Schedule(n_block, 64);

            // Schedule 執行完才會執行這一行，若不加 jobHandle.Complete()，則會在背景繼續執行，也執行下方程式碼
            handle.Complete();

            job.block_types.CopyTo(block_types);
            //job.hData.CopyTo(healthData);
            block_type_array.Dispose();
            //healthTypes.Dispose();
            //RandomArray.Dispose();
        }

        public BlockType getBlockType(int index)
        {
            return block_types[index];
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
        //public NativeArray<CrackState> crack_states;
        public int width;
        public int height;
        public Vector3Int location;

        // TODO: 原本每次開起的隨機數都會相同，是因為給 Unity.Mathematics.Random 的 seed 都是 1，因此只須傳入隨機的 seed，並在 Execute(int i) 外部建立 Unity.Mathematics.Random 物件即可
        //public NativeArray<Unity.Mathematics.Random> randoms;

        Vector3Int xyz;
        int surface_height, stone_height, diamond_top_height, diamond_bottom_height;
        int dig_cave;

        public void Execute(int i)
        {
            //int x = i % width + (int)location.x;
            //int y = (i / width) % height + (int)location.y;
            //int z = i / (width * height) + (int)location.z;

            xyz = Utils.flatToVector3Int(i, width, height) + location;

            //var random = randoms[i];

            
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

            int WATER_LINE = 16;

            //crack_states[i] = CrackState.None;

            if (xyz.y == 0)
            {
                block_types[i] = BlockType.BEDROCK;
                return;
            }

            // TODO: 目前的洞穴可能會挖到地表，且因沒有考慮到是否是地表，因而造成地表為泥土而非草地
            //if (dig_cave < cave_setting.boundary)
            //{
            //    block_types[i] = BlockType.AIR;
            //    return;
            //}

            if (xyz.y == surface_height)
            {
                //if (desertBiome < World.biomeSettings.probability)
                //{
                //    block_types[i] = BlockType.SAND;

                //    if (random.NextFloat(1) <= 0.1)
                //    {
                //        block_types[i] = BlockType.CACTUS;
                //    }
                //}
                //else if (plantTree < World.treeSettings.probability)
                //{
                //    block_types[i] = BlockType.FOREST;

                //    if (random.NextFloat(1) <= 0.1)
                //    {
                //        // Execute 當中一次處理一個 Block，因此這裡僅放置樹基，而非直接種一棵樹
                //        block_types[i] = BlockType.WOODBASE;
                //    }
                //}
                //else
                //{
                //    block_types[i] = BlockType.GRASSSIDE;
                //}

                // TODO: temp, delete after testing
                block_types[i] = BlockType.GRASSSIDE;
            }

            //else if ((diamond_bottom_height < xyz.y) && (xyz.y < diamond_top_height) && (random.NextFloat(1) < diamond_top_setting.probability))
            //{
            //    block_types[i] = BlockType.DIAMOND;
            //}

            //else if ((xyz.y < stone_height) && (random.NextFloat(1) < stone_setting.probability))
            //{
            //    block_types[i] = BlockType.STONE;
            //}

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

