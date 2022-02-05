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

        // �N�T���� blocks �� BlockType �u�����@�Ӱ}�C�A�i�[�֦s���t��
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

    // ProcessMeshDataJob �Ω�N�h�� Mesh �X�֬���@�� Mesh�A�@�ΦP MeshUtils.mergeMeshes�A���O�ϥΤF Job System �|�󦳮Ĳv
    // BurstCompile �ݨϥ� .NET 4.0 �H�W
    [BurstCompile]
    public struct ProcessMeshDataJob : IJobParallelFor
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
