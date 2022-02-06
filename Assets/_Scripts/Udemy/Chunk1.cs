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
    public class Chunk1 : MonoBehaviour
    {
        public Material atlas;

        public int width = 2;
        public int height = 2;
        public int depth = 2;

        public Block2[,,] blocks;

        // 將三維的 blocks 的 BlockType 攤平成一個陣列，可加快存取速度
        public BlockType[] block_types;

        public Vector3Int location;

        public StrataSetting surface_setting;
        public StrataSetting stone_setting;
        public StrataSetting diamond_top_setting;
        public StrataSetting diamond_bottom_setting;

        public ClusterSetting cave_setting;

        public void createChunk(Vector3Int dimensions, Vector3Int location)
        {
            MeshFilter filter = gameObject.AddComponent<MeshFilter>();
            MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.material = atlas;

            this.location = location;
            width = dimensions.x;
            height = dimensions.y;
            depth = dimensions.z;
            blocks = new Block2[width, height, depth];
            buildChunk();
            int x, y, z;

            int n_mesh = width * height * depth;
            List<Mesh> input_mesh_datas = new List<Mesh>();
            int vertex_index_offset = 0, triangle_index_offset = 0, idx = 0;
            int n_vertex, n_triangle, block_idx;
            Block2 block;

            ProcessMeshDataJob jobs = new ProcessMeshDataJob();
            jobs.vertex_index_offsets = new NativeArray<int>(n_mesh, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            jobs.triangle_index_offsets = new NativeArray<int>(n_mesh, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            for (z = 0; z < depth; z++)
            {
                for (y = 0; y < height; y++)
                {
                    for (x = 0; x < width; x++)
                    {
                        //block_idx = x + width * (y + depth * z);
                        block_idx = Utils.xyzToFlat(x, y, z, width, depth);
                        block = new Block2(block_type: block_types[block_idx],
                                           offset: new Vector3Int(x, y, z) + location,
                                           chunk: this);
                        blocks[x, y, z] = block;

                        if (block.mesh != null)
                        {
                            input_mesh_datas.Add(block.mesh);

                            jobs.vertex_index_offsets[idx] = vertex_index_offset;
                            jobs.triangle_index_offsets[idx] = triangle_index_offset;

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
            jobs.input_mesh_datas = Mesh.AcquireReadOnlyMeshData(input_mesh_datas);

            // Mesh.AllocateWritableMeshData 分配一個可寫的網格數據，然後通過 jobs 進行頂點操作，
            Mesh.MeshDataArray output_mesh_datas = Mesh.AllocateWritableMeshData(1);

            // inputMeshes -> jobs.meshData -> jobs.outputMesh -> outputMeshData -> newMesh
            jobs.output_mesh_data = output_mesh_datas[0];

            //jobs.output_mesh_data.SetIndexBufferParams(triangle_index_offset, IndexFormat.UInt32);
            jobs.setIndexBufferParams(n_triangle: triangle_index_offset);

            //jobs.output_mesh_data.SetVertexBufferParams(
            //    vertex_index_offset,
            //    new VertexAttributeDescriptor(VertexAttribute.Position, stream: 0),
            //    new VertexAttributeDescriptor(VertexAttribute.Normal, stream: 1),
            //    new VertexAttributeDescriptor(VertexAttribute.TexCoord0, stream: 2),
            //    new VertexAttributeDescriptor(VertexAttribute.TexCoord1, stream: 3));
            jobs.setVertexBufferParams(n_vertex: vertex_index_offset);

            /* 在正確的時間調用 Schedule 和 Complete
             * 一旦你擁有了一個 job 所需的數據，盡可能快地在 job 上調用 Schedule，在你需要它的執行結果之前不要調用 Complete。
             * 一個良好的實踐是調度一個你不需要等待的 job，同時它不會與當前正在運行的其他job產生競爭。
             * 舉例來說，如果你在一幀結束和下一幀開始之前擁有一段沒有其他 job 在運行的時間，並且可以接受一幀的延遲，你可以在一幀結束的時候調度一個 job，在下一幀中使用它的結果。
             * 或者，如果這個轉換時間已經被其他 job 占滿了，但是在一幀中有一大段未充分利用的時段，在這里調度你的 job 會更有效率。
             * 
             * job 擁有一個 Run 方法，你可以用它來替代 Schedule 從而讓主線程立刻執行這個 job。你可以使用它來達到調試目的。
             */
            JobHandle handle = jobs.Schedule(input_mesh_datas.Count, 4);
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

            jobs.output_mesh_data.subMeshCount = 1;
            jobs.output_mesh_data.SetSubMesh(0, sm);

            // 通過 Mesh.ApplyAndDisposeWritableMeshData 接口賦值回 Mesh
            // inputMeshes -> jobs.meshData -> jobs.outputMesh -> outputMeshData -> newMesh
            Mesh.ApplyAndDisposeWritableMeshData(output_mesh_datas, new[] { mesh });

            jobs.input_mesh_datas.Dispose();
            jobs.vertex_index_offsets.Dispose();
            jobs.triangle_index_offsets.Dispose();
            mesh.RecalculateBounds();

            filter.mesh = mesh;

            MeshCollider collider = gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
        }

        void buildChunk()
        {
            int n_block = width * depth * height;
            block_types = new BlockType[n_block];
            float surface_offset = surface_setting.getOffset();
            float stone_offset = stone_setting.getOffset();
            float diamond_top_offset = diamond_top_setting.getOffset();
            float diamond_bottom_offset = diamond_bottom_setting.getOffset();
            Vector3Int xyz;

            int surface_height, stone_height, diamond_top_height, diamond_bottom_height;
            int dig_cave;

            for (int i = 0; i < n_block; i++)
            {
                xyz = Utils.flatToVector3Int(i, width, height) + location;
                surface_height = (int)surface_setting.getAltitude(xyz.x, xyz.z, offset: surface_offset);
                stone_height = (int)stone_setting.getAltitude(xyz.x, xyz.z, offset: stone_offset);
                diamond_top_height = (int)diamond_top_setting.getAltitude(xyz.x, xyz.z, offset: diamond_top_offset);
                diamond_bottom_height = (int)diamond_bottom_setting.getAltitude(xyz.x, xyz.z, offset: diamond_bottom_offset);

                dig_cave = (int)cave_setting.fBM3D(xyz.x, xyz.y, xyz.z);

                if (xyz.y == 0)
                {
                    block_types[i] = BlockType.BEDROCK;
                    continue;
                }

                // TODO: 目前的洞穴可能會挖到地表，且因沒有考慮到是否是地表，因而造成地表為泥土而非草地
                if (dig_cave < cave_setting.boundary)
                {
                    block_types[i] = BlockType.AIR;
                    continue;
                }

                if (xyz.y == surface_height)
                {
                    block_types[i] = BlockType.GRASSSIDE;
                }
                else if ((diamond_bottom_height < xyz.y) && (xyz.y < diamond_top_height) && (Random.Range(0f, 1f) < diamond_top_setting.probability))
                {
                    block_types[i] = BlockType.DIAMOND;
                }
                else if ((xyz.y < stone_height) && (Random.Range(0f, 1f) <= stone_setting.probability))
                {
                    block_types[i] = BlockType.STONE;
                }
                else if(xyz.y < surface_height)
                {
                    block_types[i] = BlockType.DIRT;
                }
                else
                {
                    block_types[i] = BlockType.AIR;
                }

                
            }
        }

        public BlockType getBlockType(int index)
        {
            return block_types[index];
        }
    }
}
