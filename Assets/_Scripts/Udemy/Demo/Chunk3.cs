using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

namespace udemy
{
    public class Chunk3 : MonoBehaviour
    {
        public Material solid_material;
        public Material fluid_material;

        public int WIDTH = 2;
        public int HEIGHT = 2;
        public int DEPTH = 2;
        int n_block;

        public Block4[,,] blocks;

        // �N�T���� blocks �� BlockType �u�����@�Ӱ}�C�A�i�[�֦s���t��
        public BlockType[] block_types;

        // �N�T���� blocks �� CrackState �u�����@�Ӱ}�C�A�i�[�֦s���t��
        public CrackState[] crack_states;

        public Vector3Int location;

        private MeshRenderer solid_mesh_renderer;
        private GameObject solid_mesh_obj = null;

        private MeshRenderer fluid_mesh_renderer;
        private GameObject fluid_mesh_obj = null;

        // For HealBlock
        private WaitForSeconds heal_block_buffer = new WaitForSeconds(3.0f);

        (Vector3Int, BlockType)[] tree_design = new (Vector3Int, BlockType)[] {
            (new Vector3Int(-1,2,-1), BlockType.LEAVES),
            (new Vector3Int(0,2,-1), BlockType.LEAVES),
            (new Vector3Int(0,3,-1), BlockType.LEAVES),
            (new Vector3Int(1,3,-1), BlockType.LEAVES),
            (new Vector3Int(-1,4,-1), BlockType.LEAVES),
            (new Vector3Int(0,4,-1), BlockType.LEAVES),
            (new Vector3Int(0,5,-1), BlockType.LEAVES),
            (new Vector3Int(0,0,0), BlockType.WOOD),
            (new Vector3Int(0,1,0), BlockType.WOOD),
            (new Vector3Int(-1,2,0), BlockType.LEAVES),
            (new Vector3Int(0,2,0), BlockType.WOOD),
            (new Vector3Int(1,2,0), BlockType.LEAVES),
            (new Vector3Int(-1,3,0), BlockType.LEAVES),
            (new Vector3Int(0,3,0), BlockType.WOOD),
            (new Vector3Int(1,3,0), BlockType.LEAVES),
            (new Vector3Int(-1,4,0), BlockType.LEAVES),
            (new Vector3Int(0,4,0), BlockType.WOOD),
            (new Vector3Int(1,4,0), BlockType.LEAVES),
            (new Vector3Int(-1,5,0), BlockType.LEAVES),
            (new Vector3Int(0,5,0), BlockType.LEAVES),
            (new Vector3Int(1,5,0), BlockType.LEAVES),
            (new Vector3Int(0,2,1), BlockType.LEAVES),
            (new Vector3Int(1,2,1), BlockType.LEAVES),
            (new Vector3Int(-1,3,1), BlockType.LEAVES),
            (new Vector3Int(0,3,1), BlockType.LEAVES),
            (new Vector3Int(0,4,1), BlockType.LEAVES),
            (new Vector3Int(1,4,1), BlockType.LEAVES),
            (new Vector3Int(0,5,1), BlockType.LEAVES)
        };

        (Vector3Int, BlockType)[] cactus_design = new (Vector3Int, BlockType)[] {
            (new Vector3Int(0,0,0), BlockType.CACTUS),
            (new Vector3Int(0,1,0), BlockType.CACTUS),
            (new Vector3Int(-2,2,0), BlockType.CACTUS),
            (new Vector3Int(-1,2,0), BlockType.CACTUS),
            (new Vector3Int(0,2,0), BlockType.CACTUS),
            (new Vector3Int(-2,3,0), BlockType.CACTUS),
            (new Vector3Int(0,3,0), BlockType.CACTUS),
            (new Vector3Int(1,3,0), BlockType.CACTUS),
            (new Vector3Int(2,3,0), BlockType.CACTUS),
            (new Vector3Int(-2,4,0), BlockType.CACTUS),
            (new Vector3Int(0,4,0), BlockType.CACTUS),
            (new Vector3Int(2,4,0), BlockType.CACTUS),
            (new Vector3Int(0,5,0), BlockType.CACTUS)
        };

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

            DefineBlockJob2 job = new DefineBlockJob2()
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
            blocks = new Block4[WIDTH, HEIGHT, DEPTH];
            n_block = WIDTH * HEIGHT * DEPTH;
        }

        public void build()
        {
            buildMesh(obj: ref solid_mesh_obj, mesh_type: "Solid");
            buildMesh(obj: ref fluid_mesh_obj, mesh_type: "Fluid");
        }

        private void buildMesh(ref GameObject obj, string mesh_type = "Solid")
        {
            MeshFilter mesh_filter;

            if (obj == null)
            {
                obj = new GameObject(mesh_type);
                obj.transform.parent = transform;

                // �� Chunk �U�� Block �o���ܤơA�ݭn��ø Chunk �ɳo�� Component �|�Q�R���A�]���C�����ݭn���s�K�[
                mesh_filter = obj.AddComponent<MeshFilter>();

                switch (mesh_type)
                {
                    case "Solid":
                        solid_mesh_renderer = obj.AddComponent<MeshRenderer>();
                        solid_mesh_renderer.material = solid_material;
                        break;

                    case "Fluid":
                        fluid_mesh_renderer = obj.AddComponent<MeshRenderer>();
                        fluid_mesh_renderer.material = fluid_material;

                        obj.AddComponent<UVScroller>();
                        break;
                }
            }
            else
            {
                mesh_filter = obj.GetComponent<MeshFilter>();
                DestroyImmediate(obj.GetComponent<Collider>());
            }

            int x, y, z;
            List<Mesh> input_mesh_datas = new List<Mesh>();
            int vertex_index_offset = 0, triangle_index_offset = 0, idx = 0;
            int n_vertex, n_triangle, block_idx;
            Block4 block;
            bool condition0, condition1;

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
                        block = new Block4(block_type: block_types[block_idx],
                                           crack_state: crack_states[block_idx],
                                           offset: new Vector3Int(x, y, z) + location,
                                           chunk: this);
                        blocks[x, y, z] = block;

                        condition0 = block.mesh != null;
                        condition1 = false;

                        // �Ϥ��o�̬O Solid �٬O Fluid 
                        switch (mesh_type)
                        {
                            case "Solid":
                                condition1 = !MeshUtils.canSpread(block_types[block_idx]);
                                break;

                            case "Fluid":
                                condition1 = MeshUtils.canSpread(block_types[block_idx]);
                                break;
                        }

                        if (condition0 && condition1)
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
             * �@�Ө}�n�����O�իפ@�ӧA���ݭn���ݪ� job�A�P�ɥ����|�P���e���b�B�檺��Ljob�����v���C
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

            // ��s mesh_filter �� mesh
            mesh_filter.mesh = mesh;

            // �� Chunk �U�� Block �o���ܤơA�ݭn��ø Chunk �ɡAMeshCollider �|�Q�R���A�]���C�����ݭn���s�K�[
            MeshCollider collider = obj.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;

            switch (mesh_type)
            {
                case "Solid":
                    break;
                case "Fluid":
                    //obj.layer = 4;
                    obj.layer = LayerMask.NameToLayer("Water");
                    break;
            }
        }

        /// <summary>
        /// �� �s�W �� �}�a �����A�I�s���禡�A�H���sø�s Chunk
        /// </summary>
        public void rebuild()
        {
            DestroyImmediate(GetComponent<MeshFilter>());
            DestroyImmediate(GetComponent<MeshRenderer>());
            DestroyImmediate(GetComponent<Collider>());
            build();
        }

        public IEnumerable<(Vector3Int, BlockType)> iterTrees3()
        {
            int n_block = block_types.Length;
            Vector3Int block_pos, base_pos;

            for (int i = 0; i < n_block; i++)
            {
                if (block_types[i] == BlockType.WOODBASE)
                {
                    base_pos = WorldDemo4.flatToVector3Int(i);

                    foreach ((Vector3Int, BlockType) tree in tree_design)
                    {
                        block_pos = base_pos + tree.Item1;
                        yield return (block_pos, tree.Item2);
                    }
                }
                else if (block_types[i] == BlockType.CACTUSBASE)
                {
                    base_pos = WorldDemo4.flatToVector3Int(i);

                    foreach ((Vector3Int, BlockType) cactus in cactus_design)
                    {
                        block_pos = base_pos + cactus.Item1;
                        yield return (block_pos, cactus.Item2);
                    }
                }
            }
        }

        public IEnumerable<(Vector3Int, BlockType)> iterTrees2()
        {
            int n_block = block_types.Length;
            Vector3Int block_pos, base_pos;

            for (int i = 0; i < n_block; i++)
            {
                if (block_types[i] == BlockType.WOODBASE)
                {
                    base_pos = WorldDemo4.flatToVector3Int(i);

                    foreach ((Vector3Int, BlockType) tree in tree_design)
                    {
                        block_pos = base_pos + tree.Item1;
                        yield return (block_pos, tree.Item2);
                    }
                }
            }
        }

        public IEnumerable<(Vector3Int, BlockType)> iterTrees1()
        {
            int n_block = block_types.Length;
            Vector3Int block_pos;

            for (int i = 0; i < n_block; i++)
            {
                if (block_types[i] == BlockType.WOODBASE)
                {
                    block_pos = WorldDemo4.flatToVector3Int(i) + Vector3Int.up;
                    yield return (block_pos, BlockType.WOOD);

                    block_pos += Vector3Int.up;
                    yield return (block_pos, BlockType.LEAVES);
                }
            }
        }

        public void setVisiable(bool visiable)
        {
            solid_mesh_renderer.enabled = visiable;
            fluid_mesh_renderer.enabled = visiable;
        }

        /// <summary>
        /// mesh_renderer_solid �M mesh_renderer_fluid �O�_�i�������A�ۦP�A�]����^���@�Ӫ����A�Y�i
        /// </summary>
        /// <returns></returns>
        public bool isVisiable()
        {
            return solid_mesh_renderer.enabled;
        }

        public (Vector3Int, Vector3Int) getChunkBlockLocation(Vector3Int block_position)
        {
            return getChunkBlockLocation(bx: block_position.x, by: block_position.y, bz: block_position.z);
        }

        public (Vector3Int, Vector3Int) getChunkBlockLocationAdvanced(Vector3Int block_position)
        {
            return getChunkBlockLocationAdvanced(bx: block_position.x, by: block_position.y, bz: block_position.z);
        }

        /// <summary>
        /// ���I��������� Chunk�A�M�ؼФ������ Chunk ���P�ɡA�����m���y�з|�o�ͯ��ޭȶW�X�C
        /// �B�z Chunk ��ɹ� Block ���ޭȪ��B�z�A���W�X���e Chunk �ɡA���V�U�@�� Chunk �íץ� Block ���ޭȡC
        /// NOTE: �o�̥��Ҽ{�� �@�� ���j�p
        /// </summary>
        /// <param name="bx">�ؼФ����m�� X �y��</param>
        /// <param name="by">�ؼФ����m�� Y �y��</param>
        /// <param name="bz">�ؼФ����m�� Z �y��</param>
        /// <returns>(�w�ե� chunk ��m, �w�ե� block ���ޭ�)</returns>
        public (Vector3Int, Vector3Int) getChunkBlockLocation(int bx, int by, int bz)
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

            return (chunk_location, new Vector3Int(bx, by, bz));
        }

        /// <summary>
        /// getChunkBlockLocation �u�Ҽ{��m��������ΡA�]���@���u�|���@�Ӥ�V�W�X Chunk �����
        /// �� getChunkBlockLocationAdvanced �ϥΩ�{���K�[���ҡA�@���|���h�Ӥ�V�W�X Chunk ����ɡAXYZ �T�Ӥ�V�����Ҽ{
        /// �ثe�ȦҼ{�W�X�@�� Chunk �����p�A�|���Ҽ{���өΥH�W Chunk �����p
        /// </summary>
        /// <param name="bx">�ؼФ����m�� X �y��</param>
        /// <param name="by">�ؼФ����m�� Y �y��</param>
        /// <param name="bz">�ؼФ����m�� Z �y��</param>
        /// <returns>(�w�ե� chunk ��m, �w�ե� block ���ޭ�)</returns>
        public (Vector3Int, Vector3Int) getChunkBlockLocationAdvanced(int bx, int by, int bz)
        {
            Vector3Int chunk_location = new Vector3Int(location.x, location.y, location.z);

            if (bx >= WIDTH)
            {
                chunk_location.x += WIDTH;
                bx -= WIDTH;
            }
            else if (bx <= -1)
            {
                chunk_location.x -= WIDTH;
                bx += WIDTH;
            }
            
            if (by >= HEIGHT)
            {
                chunk_location.y += HEIGHT;
                by -= HEIGHT;
            }
            else if (by <= -1)
            {
                chunk_location.y -= HEIGHT;
                by += HEIGHT;
            }
            
            if (bz >= DEPTH)
            {
                chunk_location.z += DEPTH;
                bz -= DEPTH;
            }
            else if (bz <= -1)
            {
                chunk_location.z -= DEPTH;
                bz += DEPTH;
            }

            return (chunk_location, new Vector3Int(bx, by, bz));
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
            try
            {
                block_types[index] = block_type;
                crack_states[index] = CrackState.None;
            }
            catch (IndexOutOfRangeException)
            {
                Vector3Int v = WorldDemo4.flatToVector3Int(index);
                Debug.LogError($"{index}/{block_types.Length}; {v}/({WIDTH}, {HEIGHT}, {DEPTH})");
            }
        }

        /// <summary>
        /// �V���Y������ɡA�֥[�}�a�{��
        /// �Y�}�a�{�׻P����j�׬۷��A�~�|�u���}�a��
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
        /// �}�a�{��(CrackState) �P ����j��(Strenth) �۷��A�~��u���}�a��
        /// </summary>
        /// <param name="index">������ޭ�</param>
        /// <returns>�O�_�Q�}�a�F</returns>
        public bool isCracked(int index)
        {
            return crack_states[index].Equals((CrackState)MeshUtils.getStrenth(block_types[index]));
        }

        /// <summary>
        /// �Y���򩥵�����������A�j�׳]�m�� -1�A���ܵL�k�}�a�C
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

    // DefineBlockJob�G�ھڮ��޻P��m����T�A�M�w Block �������P��m�C�A��� ProcessMeshDataJob �B�z�p��e�{�C
    struct DefineBlockJob2 : IJobParallelFor
    {
        public NativeArray<BlockType> block_types;
        public NativeArray<CrackState> crack_states;
        public NativeArray<Unity.Mathematics.Random> randoms;

        public int width;
        public int height;
        public Vector3Int location;

        Vector3Int xyz;
        Unity.Mathematics.Random random;
        int surface_height, stone_height, diamond_top_height, diamond_bottom_height;
        int dig_cave, plant_tree, dessert_biome;

        public void Execute(int i)
        {
            xyz = Utils.flatToVector3Int(i, width, height) + location;
            random = randoms[i];

            surface_height = (int)Strata.fBM(x: xyz.x, z: xyz.z,
                                             octaves: WorldDemo4.surface_strata.octaves,
                                             scale: WorldDemo4.surface_strata.scale,
                                             height_scale: WorldDemo4.surface_strata.height_scale,
                                             height_offset: WorldDemo4.surface_strata.height_offset);

            stone_height = (int)Strata.fBM(x: xyz.x, z: xyz.z,
                                           octaves: WorldDemo4.stone_strata.octaves,
                                           scale: WorldDemo4.stone_strata.scale,
                                           height_scale: WorldDemo4.stone_strata.height_scale,
                                           height_offset: WorldDemo4.stone_strata.height_offset);

            diamond_top_height = (int)Strata.fBM(x: xyz.x, z: xyz.z,
                                                 octaves: WorldDemo4.diamond_top_strata.octaves,
                                                 scale: WorldDemo4.diamond_top_strata.scale,
                                                 height_scale: WorldDemo4.diamond_top_strata.height_scale,
                                                 height_offset: WorldDemo4.diamond_top_strata.height_offset);

            diamond_bottom_height = (int)Strata.fBM(x: xyz.x, z: xyz.z,
                                                    octaves: WorldDemo4.diamond_bottom_strata.octaves,
                                                    scale: WorldDemo4.diamond_bottom_strata.scale,
                                                    height_scale: WorldDemo4.diamond_bottom_strata.height_scale,
                                                    height_offset: WorldDemo4.diamond_bottom_strata.height_offset);

            dig_cave = (int)Cluster.fBM3D(x: xyz.x, y: xyz.y, z: xyz.z,
                                          octaves: WorldDemo4.cave_cluster.octaves,
                                          scale: WorldDemo4.cave_cluster.scale,
                                          height_scale: WorldDemo4.cave_cluster.height_scale,
                                          height_offset: WorldDemo4.cave_cluster.height_offset);


            plant_tree = (int)Cluster.fBM3D(x: xyz.x, y: xyz.y, z: xyz.z,
                                            octaves: WorldDemo4.tree_cluster.octaves,
                                            scale: WorldDemo4.tree_cluster.scale,
                                            height_scale: WorldDemo4.tree_cluster.height_scale,
                                            height_offset: WorldDemo4.tree_cluster.height_offset);


            dessert_biome = (int)Cluster.fBM3D(x: xyz.x, y: xyz.y, z: xyz.z,
                                               octaves: WorldDemo4.biome_cluster.octaves,
                                               scale: WorldDemo4.biome_cluster.scale,
                                               height_scale: WorldDemo4.biome_cluster.height_scale,
                                               height_offset: WorldDemo4.biome_cluster.height_offset);


            /* �ثe�b���u�U�����ש�F�@�� Post-process Volume�A�w�q��v���i�J�Ӱϰ쪺�ĪG(�ݰ_�����Ū��B�ਣ�׫ܧC)�A
             * �çQ�� WaterManager �b������V�l�ܪ��a�C�Y�A���ޱq���̤U������u�H�U���ϰ쳣�|�ݰ_�ӹ��b�����A
             * �Y�K�{�b���b�����C �o�Ӱ��k�����ﱼ�A�]�����������u�H�U�����[�ӮĪG�A���u���w��覡(�n�ۤv��Φ����@�ɦ��h��)�]���O�ܲz�Q */
            int WATER_LINE = 20;

            crack_states[i] = CrackState.None;

            if (xyz.y == 0)
            {
                block_types[i] = BlockType.BEDROCK;
                return;
            }

            if (dig_cave < WorldDemo4.cave_cluster.boundary)
            {
                block_types[i] = BlockType.AIR;
                return;
            }

            if (xyz.y == surface_height && xyz.y >= WATER_LINE)
            {
                if (dessert_biome < WorldDemo4.biome_cluster.boundary)
                {
                    block_types[i] = BlockType.SAND;

                    if (random.NextFloat(1) <= 0.05f)
                    {
                        block_types[i] = BlockType.CACTUSBASE;
                    }
                }
                else if (plant_tree < WorldDemo4.tree_cluster.boundary)
                {
                    block_types[i] = BlockType.FOREST;

                    if (random.NextFloat(1) <= 0.05f)
                    {
                        // Execute �����@���B�z�@�� Block�A�]���o�̶ȩ�m���A�ӫD�����ؤ@�ʾ�
                        block_types[i] = BlockType.WOODBASE;
                    }
                }
                else
                {
                    block_types[i] = BlockType.GRASSSIDE;
                }
            }

            else if ((diamond_bottom_height < xyz.y) && (xyz.y < diamond_top_height) && (random.NextFloat(1) <= WorldDemo4.diamond_top_strata.probability))
            {
                block_types[i] = BlockType.DIAMOND;
            }

            else if ((xyz.y < stone_height) && (random.NextFloat(1) <= WorldDemo4.stone_strata.probability))
            {
                block_types[i] = BlockType.STONE;
            }

            else if (xyz.y < surface_height)
            {
                block_types[i] = BlockType.DIRT;
            }

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

}