using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace udemy
{
    public class Chunk
    {
        public int width = 2;
        public int height = 2;
        public int depth = 2;

        public Block[,,] blocks;

        // 將三維的 blocks 的 BlockType 攤平成一個陣列，可加快存取速度
        public BlockType[] block_types;

        public Vector3Int location;

        void buildChunk()
        {
            int n_block = width * depth * height;
            block_types = new BlockType[n_block];

            for (int i = 0; i < n_block; i++)
            {
                block_types[i] = BlockType.DIRT;
            }
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

    // ProcessMeshDataJob 用於將多個 Mesh 合併為單一個 Mesh，作用同 MeshUtils.mergeMeshes，但是使用了 Job System 會更有效率
    // BurstCompile 需使用 .NET 4.0 以上
    [BurstCompile]
    public struct ProcessMeshDataJob : IJobParallelFor
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
