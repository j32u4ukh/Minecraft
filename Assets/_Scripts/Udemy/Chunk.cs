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

        // �N�T���� blocks �� BlockType �u�����@�Ӱ}�C�A�i�[�֦s���t��
        public BlockType[] block_types;

        // �N�T���� blocks �� CrackState �u�����@�Ӱ}�C�A�i�[�֦s���t��
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

            // Mesh.AllocateWritableMeshData ���t�@�ӥi�g������ƾڡA�M��q�L jobs �i�泻�I�ާ@�A
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

            /* �b���T���ɶ��ե� Schedule �M Complete
             * �@���A�֦��F�@�� job �һݪ��ƾڡA�ɥi��֦a�b job �W�ե� Schedule�A�b�A�ݭn�������浲�G���e���n�ե� Complete�C
             * �@�Ө}�n�����O�իפ@�ӧA���ݭn���ݪ� job�A�P�ɥ����|�P��e���b�B�檺��Ljob�����v���C
             * �|�Ҩӻ��A�p�G�A�b�@�V�����M�U�@�V�}�l���e�֦��@�q�S����L job �b�B�檺�ɶ��A�åB�i�H�����@�V������A�A�i�H�b�@�V�������ɭԽիפ@�� job�A�b�U�@�V���ϥΥ������G�C
             * �Ϊ̡A�p�G�o���ഫ�ɶ��w�g�Q��L job �e���F�A���O�b�@�V�����@�j�q���R���Q�Ϊ��ɬq�A�b�o���իקA�� job �|�󦳮Ĳv�C
             * 
             * job �֦��@�� Run ��k�A�A�i�H�Υ��Ӵ��N Schedule �q�����D�u�{�ߨ����o�� job�C�A�i�H�ϥΥ��ӹF��ոեت��C
             */
            JobHandle handle = job.Schedule(input_mesh_datas.Count, 4);
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

            job.output_mesh_data.subMeshCount = 1;
            job.output_mesh_data.SetSubMesh(0, sm);

            // �q�L Mesh.ApplyAndDisposeWritableMeshData ���f��Ȧ^ Mesh
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

            // Schedule ���槹�~�|����o�@��A�Y���[ jobHandle.Complete()�A�h�|�b�I���~�����A�]����U��{���X
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

    /* �Ҧ��� job ���|�O struct�A�îھڻݭn�~�Ӥ��P��interface
     * 
     * �@�Өæ�� job �ϥΤ@�� NativeArray �s��ƾڨӧ@�������ƾڷ��C�æ�� job ���h�Ӯ֤߰���C�C�Ӯ֤ߤW���@�� job�A�C�� job �B�z�@�����u�@�q�C
     * IJobParallelFor ���欰�������� IJob�A���O���P��u����@�� Execute ��k�A���|�b�ƾڷ����C�@���W���� Execute ��k�CExecute ��k�����@�Ӿ�ƫ����ѼơC
     * �o�ӯ��ެO���F�b job ������ާ@��{���X�ݩM�ާ@�ƾڷ��W����Ӥ����C
     * 
     * ��իרæ�� job �ɡA�A�������w�A���� NativeArray �ƾڷ������סC�b���c���P�ɦs�b�h�� NativeArrayUnity �ɡAC# Job System �����D�A�n�ϥέ��@�� NativeArray �@���ƾڷ��C
     * �o�Ӫ��צP�ɷ|�i�� C# Job System ���h�֭� Execute ��k�|�Q����C
     * �b�o�ӳ������A�æ�� job ���ի׷|������C��իרæ�ƥ��ȮɡAC# Job System �|�N�u�@�����h�ӧ妸�A���o�����P���֤ߨӳB�z�C�C�@�ӧ妸���]�t�@������ Execute ��k�C
     * �H�� C# Job System �|�b�C�� CPU �֤ߪ� Unity ��� Job System �W�ի׳̦h�@�� job�A�öǻ����o�� job �@�ǧ妸���u�@�ӧ����C
     * 
     * ��@�ӭ�� job ���e�����F���t�������u�@�妸��A���|�q��L��� job ���������ѧE���u�@�妸�C���C���u������ӭ�� job �ѧE�妸���@�b�A���F�T�O�w�s������(cache locality)�C
     * ���F�u�Ƴo�ӹL�{�A�A�ݭn���w�@�ӨC�妸�ƶq(batch count)�C�o�ӨC�妸�ƶq����F�A�|�ͦ��h�� job �M�u�{���i����Ȥ��o���ɫסC
     * �ϥΤ@�Ӹ��C���C�妸�ƶq�A��p 1�A�|�ϧA�b�u�{�������u�@���t�󥭧��C���|�a�Ӥ@���B�~���}�P�A�ҥH���ɼW�[�C�妸�ƶq�|�O��n����ܡC
     * �q�C�妸�ƶq�� 1 �}�l�A�M��C�C�W�[�o�Ӽƶq����ʯण�A���ɬO�@�ӦX�z�������C
     * 
     * ���n�b job ���}�@���ޤ��s
     * �b job ���}�@���ޤ��s�|���H�m�H�o�C�A�åB�o�� job ����Q�� Unity �� Burst �sĶ���Ӵ��ɩʯ�C
     * Burst �O�@�ӷs����� LLVM ����ݽsĶ���޳N�A���|�ϨƱ����A��[²��C����� C# job �çQ�ΧA���x���S�w�\�ಣ�Ͱ����u�ƪ������X�C
     * �ѦҡGhttps://zhuanlan.zhihu.com/p/58125078
     */

    // DefineBlockJob�G�ھڮ��޻P��m����T�A�M�w Block �������P��m�C�A��� ProcessMeshDataJob �B�z�p��e�{�C
    struct DefineBlockJob : IJobParallelFor
    {
        public NativeArray<BlockType> block_types;
        //public NativeArray<CrackState> crack_states;
        public int width;
        public int height;
        public Vector3Int location;

        // TODO: �쥻�C���}�_���H���Ƴ��|�ۦP�A�O�]���� Unity.Mathematics.Random �� seed ���O 1�A�]���u���ǤJ�H���� seed�A�æb Execute(int i) �~���إ� Unity.Mathematics.Random ����Y�i
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

            // TODO: �ثe���}�ޥi��|����a��A�B�]�S���Ҽ{��O�_�O�a��A�]�ӳy���a���d�g�ӫD��a
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
                //        // Execute ���@���B�z�@�� Block�A�]���o�̶ȩ�m���A�ӫD�����ؤ@�ʾ�
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

            // TODO: ��ڼƭȭn�ھڦa�ΰ��C�Ӱ��վ�
            // TODO: �p��T�O���O�ۤv�@�Ӱ϶��A�ӫD�H�������G�b�a�Ϥ��H�j���n����@�ˡA�ϥ� fBM3D
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

    // ProcessMeshDataJob�G�ھ� Block �������P��m���A�p��һݶK�ϻP��m
    // ProcessMeshDataJob �Ω�N�h�� Mesh �X�֬���@�� Mesh�A�@�ΦP MeshUtils.mergeMeshes�A���O�ϥΤF Job System �|�󦳮Ĳv
    // BurstCompile �ݨϥ� .NET 4.0 �H�W
    [BurstCompile]
    struct ProcessMeshDataJob : IJobParallelFor
    {
        /* �N NativeContainer �аO���uŪ��
         * �O�� job �b�q�{���p�U�֦� NativeContainer ��Ū�g�v���C�b�X�A�� NativeContainer �W�ϥ� [ReadOnly] �ݩʥi�H���ɩʯ�C*/
        [ReadOnly] public Mesh.MeshDataArray input_mesh_datas;

        // ProcessMeshDataJob �N�h�� MeshData �X�֦��@�ӡA�]���o�̬O MeshData �ӫD MeshDataArray
        public Mesh.MeshData output_mesh_data;

        // �֥[�e���U�� MeshData �� vertex �ӼơA�i�D��e vertex �ƾڭn�s�J�ɡA���ޭȪ������q
        public NativeArray<int> vertex_index_offsets;

        // �֥[�e���U�� MeshData �� triangle �ӼơA�i�D��e triangle �ƾڭn�s�J�ɡA���ޭȪ������q
        public NativeArray<int> triangle_index_offsets;

        /// <summary>
        /// Called once per element
        /// </summary>
        /// <param name="index"></param>
        public void Execute(int index)
        {
            Mesh.MeshData data = input_mesh_datas[index];

            // ��e vertex �ƾڭn�s�J�ɡA���ޭȪ������q
            int vertex_index_offset = vertex_index_offsets[index];

            // ��e vertex �ƾڭӼ�
            int n_vertex = data.vertexCount;

            NativeArray<float3> current_vertices = new NativeArray<float3>(n_vertex, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            // �q data �����o Vertices ��J current_vertices�AReinterpret<Vector3> Ū�J Vector3�A�ഫ�� float3
            data.GetVertices(current_vertices.Reinterpret<Vector3>());

            NativeArray<float3> current_normals = new NativeArray<float3>(n_vertex, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            data.GetNormals(current_normals.Reinterpret<Vector3>());

            // uv �������O Vector2�A���b Job System �����ϥ� Vector3
            NativeArray<float3> current_uvs = new NativeArray<float3>(n_vertex, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            data.GetUVs(0, current_uvs.Reinterpret<Vector3>());

            NativeArray<float3> current_uv2s = new NativeArray<float3>(n_vertex, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            data.GetUVs(1, current_uv2s.Reinterpret<Vector3>());

            // �ھ� SetVertexBufferParams �\�񶶧ǡA�̧Ǩ��o 0: Position, 1: Normal, 2: TexCoord0, 3: TexCoord1
            NativeArray<Vector3> vertices = output_mesh_data.GetVertexData<Vector3>(stream: 0);
            NativeArray<Vector3> normals = output_mesh_data.GetVertexData<Vector3>(stream: 1);
            NativeArray<Vector3> uvs = output_mesh_data.GetVertexData<Vector3>(stream: 2);
            NativeArray<Vector3> uv2s = output_mesh_data.GetVertexData<Vector3>(stream: 3);

            /* �Q�� index ���o�U�� MeshData�A�A���O���X vertices, normals, uvs, uv2s�A
             * �N�ƾڦs�J�P�@�� NativeArray<Vector3>�A�Q�� vertex_index �N�U�� MeshData ���ƾ��x�s�쥿�T����m
             * 
             */
            for (int i = 0; i < n_vertex; i++)
            {
                vertices[vertex_index_offset + i] = current_vertices[i];
                normals[vertex_index_offset + i] = current_normals[i];
                uvs[vertex_index_offset + i] = current_uvs[i];
                uv2s[vertex_index_offset + i] = current_uv2s[i];
            }

            /* NativeArray �ϥΫ����I�s Dispose()�A�H�קK�O���鷸�X */
            current_vertices.Dispose();
            current_normals.Dispose();
            current_uvs.Dispose();
            current_uv2s.Dispose();

            // ���o��X�ƾڤ����T���γ��I���ޭ�
            NativeArray<int> triangles = output_mesh_data.GetIndexData<int>();

            // ��e triangle �ƾڭn�s�J�ɡA���ޭȪ������q
            int triangle_index_offset = triangle_index_offsets[index];

            // ��e triangle �ƾڭӼ�
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
        /// �o�̪� stream �����ǡA���M ProcessMeshDataJob.Execute �� GetVertexData �� stream �����ǬۦP
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

