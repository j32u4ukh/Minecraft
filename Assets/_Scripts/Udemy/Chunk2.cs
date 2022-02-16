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

        // �N�T���� blocks �� BlockType �u�����@�Ӱ}�C�A�i�[�֦s���t��
        public BlockType[] block_types;

        // �N�T���� blocks �� CrackState �u�����@�Ӱ}�C�A�i�[�֦s���t��
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

            // Schedule ���槹�~�|����o�@��A�Y���[ jobHandle.Complete()�A�h�|�b�I���~�����A�]����U��{���X
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
            // �� Chunk �U�� Block �o���ܤơA�ݭn��ø Chunk �ɡAMeshFilter �|�Q�R���A�]���C�����ݭn���s�K�[
            MeshFilter filter = gameObject.AddComponent<MeshFilter>();

            // �� Chunk �U�� Block �o���ܤơA�ݭn��ø Chunk �ɡAMeshRenderer �|�Q�R���A�]���C�����ݭn���s�K�[
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

            // �� Chunk �U�� Block �o���ܤơA�ݭn��ø Chunk �ɡAMeshCollider �|�Q�R���A�]���C�����ݭn���s�K�[
            MeshCollider collider = gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
        }

        /// <summary>
        /// �� �s�W �� �}�a �����A�I�s���禡�A�H���sø�s Chunk
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

        /// <summary>
        /// ���I��������� Chunk�A�M�ؼФ������ Chunk ���P�ɡA�����m���y�з|�o�ͯ��ޭȶW�X�C
        /// �B�z Chunk ��ɹ� Block ���ޭȪ��B�z�A��W�X��e Chunk �ɡA���V�U�@�� Chunk �íץ� Block ���ޭȡC
        /// NOTE: �o�̥��Ҽ{�� �@�� ���j�p
        /// </summary>
        /// <param name="bx">�ؼФ����m�� X �y��</param>
        /// <param name="by">�ؼФ����m�� Y �y��</param>
        /// <param name="bz">�ؼФ����m�� Z �y��</param>
        /// <returns>(�w�ե� chunk ��m, �w�ե� block ���ޭ�)</returns>
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
        /// �V���Y������ɡA�֥[�}�a�{��
        /// �Y�}�a�{�׻P����j�׬۷�A�~�|�u���}�a��
        /// </summary>
        /// <param name="index">������ޭ�</param>
        /// <returns>�Ӥ���O�_�Q�}�a</returns>
        public bool crackBlock(int index)
        {
            // �Y�L�k�}�a�A������^
            if (!isCrackable(index))
            {
                return false;
            }

            // �Ĥ@���V����Ĳ�o�A�@�q�ɶ����ˬd�O�_�w�Q�V���A�_�h�״_�ۤv crack_state ��_�� CrackState.None
            if (crack_states[index].Equals(CrackState.None))
            {
                StartCoroutine(healBlock(index));
            }

            // �֥[�}�a�{��
            crack_states[index]++;

            // �Y �}�a�{�� �P ����j�� �۷�
            if (isCracked(index))
            {
                // ��گ}�a�Ӥ��
                block_types[index] = BlockType.AIR;
                crack_states[index] = CrackState.None;

                return true;
            }

            return false;
        }

        /// <summary>
        /// �}�a�{��(CrackState) �P ����j��(Strenth) �۷�A�~��u���}�a��
        /// </summary>
        /// <param name="index">������ޭ�</param>
        /// <returns>�O�_�Q�}�a�F</returns>
        public bool isCracked(int index)
        {
            return crack_states[index].Equals((CrackState)MeshUtils.getStrenth(block_types[index]));
        }

        /// <summary>
        /// �Y���򩥵�����������A�j�׳]�m�� -1�A��ܵL�k�}�a�C
        /// ��L���h�L����C
        /// </summary>
        /// <param name="index"></param>
        /// <returns>�Ӥ���O�_�i�H�}�a</returns>
        public bool isCrackable(int index)
        {
            return MeshUtils.getStrenth(block_types[index]) != -1;
        }

        // �@�q�ɶ����ˬd�O�_�w�Q�V���A�_�h�״_�ۤv health ��_�� NOCRACK
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
