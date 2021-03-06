using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Rendering;

namespace udemy
{
    public class ChunkDemo1 : MonoBehaviour
    {
        public Material atlas;

        public int width = 2;
        public int height = 2;
        public int depth = 2;

        public Block1[,,] blocks;

        // Start is called before the first frame update
        void Start()
        {
            MeshFilter filter = gameObject.AddComponent<MeshFilter>();
            MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.material = atlas;

            blocks = new Block1[width, height, depth];
            int x, y, z;

            int n_mesh = width * height * depth;
            List<Mesh> input_mesh_datas = new List<Mesh>(n_mesh);
            int vertex_index_offset = 0, triangle_index_offset = 0, idx = 0;
            int n_vertex, n_triangle;
            Block1 block;

            ProcessMeshDataJob jobs = new ProcessMeshDataJob();
            jobs.vertex_index_offsets = new NativeArray<int>(n_mesh, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            jobs.triangle_index_offsets = new NativeArray<int>(n_mesh, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            for (z = 0; z < depth; z++)
            {
                for (y = 0; y < height; y++)
                {
                    for (x = 0; x < width; x++)
                    {
                        block = new Block1(BlockType.DIRT, new Vector3Int(x, y, z));
                        blocks[x, y, z] = block;
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
        }
    }
}
