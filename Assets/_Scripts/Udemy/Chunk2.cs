using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

namespace udemy
{
    public class Chunk2 : MonoBehaviour
    {
        public Material atlas;

        public int WIDTH = 2;
        public int HEIGHT = 2;
        public int DEPTH = 2;
        int n_block;

        public Block3[,,] blocks;

        // 將三維的 blocks 的 BlockType 攤平成一個陣列，可加快存取速度
        public BlockType[] block_types;

        // 將三維的 blocks 的 CrackState 攤平成一個陣列，可加快存取速度
        public CrackState[] crack_states;

        public Vector3Int location;

        public MeshRenderer mesh_renderer;

        // For HealBlock
        private WaitForSeconds heal_block_buffer = new WaitForSeconds(3.0f);

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

        public void locate(Vector3Int dimensions, Vector3Int location)
        {
            this.location = location;

            WIDTH = dimensions.x;
            HEIGHT = dimensions.y;
            DEPTH = dimensions.z;
            blocks = new Block3[WIDTH, HEIGHT, DEPTH];
            n_block = WIDTH * HEIGHT * DEPTH;
        }

        public void build()
        {
            // 當 Chunk 下的 Block 發生變化，需要重繪 Chunk 時，MeshFilter 會被刪除，因此每次都需要重新添加
            MeshFilter filter = gameObject.AddComponent<MeshFilter>();

            // 當 Chunk 下的 Block 發生變化，需要重繪 Chunk 時，MeshRenderer 會被刪除，因此每次都需要重新添加
            mesh_renderer = gameObject.AddComponent<MeshRenderer>();
            mesh_renderer.material = atlas;
            
            int x, y, z;
            List<Mesh> input_mesh_datas = new List<Mesh>();
            int vertex_index_offset = 0, triangle_index_offset = 0, idx = 0;
            int n_vertex, n_triangle, block_idx;
            Block3 block;

            ProcessMeshDataJob job = new ProcessMeshDataJob();
            job.vertex_index_offsets = new NativeArray<int>(n_block, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            job.triangle_index_offsets = new NativeArray<int>(n_block, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            for (z = 0; z < DEPTH; z++)
            {
                for (y = 0; y < HEIGHT; y++)
                {
                    for (x = 0; x < WIDTH; x++)
                    {
                        //block_idx = x + width * (y + depth * z);
                        block_idx = Utils.xyzToFlat(x, y, z, WIDTH, DEPTH);
                        block = new Block3(block_type: block_types[block_idx],
                                           crack_state: crack_states[block_idx],
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

            // 當 Chunk 下的 Block 發生變化，需要重繪 Chunk 時，MeshCollider 會被刪除，因此每次都需要重新添加
            MeshCollider collider = gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
        }

        /// <summary>
        /// 當 新增 或 破壞 方塊後，呼叫此函式，以重新繪製 Chunk
        /// </summary>
        public void rebuild()
        {
            DestroyImmediate(gameObject.GetComponent<MeshFilter>());
            DestroyImmediate(mesh_renderer);
            DestroyImmediate(gameObject.GetComponent<Collider>());
            build();
        }

        public void setVisiable(bool visiable)
        {
            mesh_renderer.enabled = visiable;
        }

        public Tuple<Vector3Int, Vector3Int> getChunkBlockLocation(Vector3Int block_position)
        {
            return getChunkBlockLocation(bx: block_position.x, by: block_position.y, bz: block_position.z);
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
        public Tuple<Vector3Int, Vector3Int> getChunkBlockLocation(int bx, int by, int bz)
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

            return new Tuple<Vector3Int, Vector3Int>(chunk_location, new Vector3Int(bx, by, bz));
        }

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
            block_types[index] = block_type;
            crack_states[index] = CrackState.None;
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

            if(block_types[index] != BlockType.AIR)
            {
                crack_states[index] = CrackState.None;
                rebuild();
            }
        }
    }
}
