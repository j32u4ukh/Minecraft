using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Rendering;

namespace udemy
{
    public class ChunkDemo2 : MonoBehaviour
    {
        public Material atlas;

        public int width = 2;
        public int height = 2;
        public int depth = 2;

        [Header("Perlin Setting")]
        [SerializeField] int octaves = 8;
        [SerializeField] float scale = 0.001f;
        [SerializeField] float height_scale = 10f;

        Chunk1 chunk;

        // Start is called before the first frame update
        void Start()
        {
            MeshFilter filter = gameObject.AddComponent<MeshFilter>();
            MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.material = atlas;

            chunk = new Chunk1();
            chunk.width = width;
            chunk.height = height;
            chunk.depth = depth;

            chunk.location = Vector3Int.zero;
            chunk.blocks = new Block2[width, height, depth];
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
                        block = new Block2(block_type: chunk.block_types[block_idx], offset: new Vector3Int(x, y, z), chunk);
                        chunk.blocks[x, y, z] = block;

                        if(block.mesh != null)
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

            // Mesh.AllocateWritableMeshData ???t?@???i?g???????????A?M???q?L jobs ?i?????I???@?A
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

            /* ?b???T?????????? Schedule ?M Complete
             * ?@???A?????F?@?? job ???????????A???i?????a?b job ?W???? Schedule?A?b?A???n???????????G???e???n???? Complete?C
             * ?@???}?n???????O?????@???A?????n?????? job?A?P???????|?P???e???b?B???????Ljob?????v???C
             * ?|???????A?p?G?A?b?@?V?????M?U?@?V?}?l???e?????@?q?S?????L job ?b?B?????????A???B?i?H?????@?V???????A?A?i?H?b?@?V???????????????@?? job?A?b?U?@?V?????????????G?C
             * ?????A?p?G?o???????????w?g?Q???L job ?e???F?A???O?b?@?V?????@?j?q???R???Q???????q?A?b?o???????A?? job ?|???????v?C
             * 
             * job ?????@?? Run ???k?A?A?i?H?????????N Schedule ?q?????D?u?{?????????o?? job?C?A?i?H?????????F???????????C
             */
            JobHandle handle = jobs.Schedule(input_mesh_datas.Count, 4);
            Mesh mesh = new Mesh();

            SubMeshDescriptor sm = new SubMeshDescriptor(0, triangle_index_offset, MeshTopology.Triangles);
            sm.firstVertex = 0;
            sm.vertexCount = vertex_index_offset;

            /* ???? JobHandle.Complete ?????s???o?k???v
             * ?b?D?u?{???s?????????e?A?l?????????????v???n?????????????C?u???d JobHandle.IsCompleted ?O???????C
             * ?A???????? JobHandle.Complete ???b?D?u?{?????s???? NaitveContainer ???????????v?C???? Complete ?P???|?M?z?w?????t?????????A?C
             * ???o?????????|?y?????s?n?|?C?o???L?{?]?b?A?C?@?V?????????????W?@?V job ???s job ???Q?????C
             * 
             * ?b?D?u?{?????? Schedule ?M Complete
             * ?A?u???b?D?u?{?????? Schedule ?M Complete ???k?C?p?G?@?? job ???n???????t?@???A???? JobHandle ???B?z???????t?????O?????b job ???????s?? job?C
             * 
             * 
             */
            handle.Complete();

            jobs.output_mesh_data.subMeshCount = 1;
            jobs.output_mesh_data.SetSubMesh(0, sm);

            // ?q?L Mesh.ApplyAndDisposeWritableMeshData ???f?????^ Mesh
            // inputMeshes -> jobs.meshData -> jobs.outputMesh -> outputMeshData -> newMesh
            Mesh.ApplyAndDisposeWritableMeshData(output_mesh_datas, new[] { mesh });

            jobs.input_mesh_datas.Dispose();
            jobs.vertex_index_offsets.Dispose();
            jobs.triangle_index_offsets.Dispose();
            mesh.RecalculateBounds();

            filter.mesh = mesh;
        }

        void buildChunk()
        {
            int n_block = width * depth * height;
            chunk.block_types = new BlockType[n_block];
            Strata surface_strata = new Strata(octaves: octaves, scale: scale, height_scale: height_scale);
            surface_strata.setAltitude(altitude: height - 2);
            Vector3Int xyz;

            for (int i = 0; i < n_block; i++)
            {
                xyz = Utils.flatToVector3Int(i, width, height);

                if (xyz.y > surface_strata.fBM(xyz.x, xyz.z))
                {
                    chunk.block_types[i] = BlockType.AIR;
                }
                else
                {
                    chunk.block_types[i] = BlockType.DIRT;
                }
            }
        }
    }
}
