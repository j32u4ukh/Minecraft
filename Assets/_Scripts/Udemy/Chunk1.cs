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

        // �N�T���� blocks �� BlockType �u�����@�Ӱ}�C�A�i�[�֦s���t��
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

            // Mesh.AllocateWritableMeshData ���t�@�ӥi�g������ƾڡA�M��q�L jobs �i�泻�I�ާ@�A
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

            /* �b���T���ɶ��ե� Schedule �M Complete
             * �@���A�֦��F�@�� job �һݪ��ƾڡA�ɥi��֦a�b job �W�ե� Schedule�A�b�A�ݭn�������浲�G���e���n�ե� Complete�C
             * �@�Ө}�n�����O�իפ@�ӧA���ݭn���ݪ� job�A�P�ɥ����|�P��e���b�B�檺��Ljob�����v���C
             * �|�Ҩӻ��A�p�G�A�b�@�V�����M�U�@�V�}�l���e�֦��@�q�S����L job �b�B�檺�ɶ��A�åB�i�H�����@�V������A�A�i�H�b�@�V�������ɭԽիפ@�� job�A�b�U�@�V���ϥΥ������G�C
             * �Ϊ̡A�p�G�o���ഫ�ɶ��w�g�Q��L job �e���F�A���O�b�@�V�����@�j�q���R���Q�Ϊ��ɬq�A�b�o���իקA�� job �|�󦳮Ĳv�C
             * 
             * job �֦��@�� Run ��k�A�A�i�H�Υ��Ӵ��N Schedule �q�����D�u�{�ߨ����o�� job�C�A�i�H�ϥΥ��ӹF��ոեت��C
             */
            JobHandle handle = jobs.Schedule(input_mesh_datas.Count, 4);
            Mesh mesh = new Mesh();
            mesh.name = $"Chunk_{location.x}_{location.y}_{location.z}";

            SubMeshDescriptor sm = new SubMeshDescriptor(0, triangle_index_offset, MeshTopology.Triangles);
            sm.firstVertex = 0;
            sm.vertexCount = vertex_index_offset;

            /* �ե� JobHandle.Complete �ӭ��s��o�k���v
             * �b�D�u�{���s�ϥμƾګe�A�l�ܼƾڪ��Ҧ��v�ݭn�̿ඵ�������C�u�ˬd JobHandle.IsCompleted �O�������C
             * �A�����ե� JobHandle.Complete �Ӧb�D�u�{�����s��� NaitveContainer �������Ҧ��v�C�ե� Complete �P�ɷ|�M�z�w���ʨt�Τ������A�C
             * ���o�˰����ܷ|�y�����s�n�|�C�o�ӹL�{�]�b�A�C�@�V���իר̿��W�@�V job ���s job �ɳQ���ΡC
             * 
             * �b�D�u�{���ե� Schedule �M Complete
             * �A�u��b�D�u�{���ե� Schedule �M Complete ��k�C�p�G�@�� job �ݭn�̿��t�@�ӡA�ϥ� JobHandle �ӳB�z�̿����t�Ӥ��O���զb job ���ի׷s�� job�C
             * 
             * 
             */
            handle.Complete();

            jobs.output_mesh_data.subMeshCount = 1;
            jobs.output_mesh_data.SetSubMesh(0, sm);

            // �q�L Mesh.ApplyAndDisposeWritableMeshData ���f��Ȧ^ Mesh
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

                // TODO: �ثe���}�ޥi��|����a��A�B�]�S���Ҽ{��O�_�O�a��A�]�ӳy���a���d�g�ӫD��a
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
