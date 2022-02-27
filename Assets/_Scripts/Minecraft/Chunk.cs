using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace minecraft
{
    public class Chunk : MonoBehaviour
    {
        public Material solid_material;
        public Material fluid_material;

        public int WIDTH = 2;
        public int HEIGHT = 2;
        public int DEPTH = 2;

        // n_block = WIDTH * HEIGHT * DEPTH
        int n_block;

        public Block[,,] blocks;

        // �N�T���� blocks �� BlockType �u�����@�Ӱ}�C�A�i�[�֦s���t��
        public BlockType[] block_types;

        // �N�T���� blocks �� CrackState �u�����@�Ӱ}�C�A�i�[�֦s���t��
        public CrackState[] crack_states;

        public Vector3Int location;

        private MeshRenderer solid_mesh_renderer;
        private GameObject solid_mesh_obj = null;

        private MeshRenderer fluid_mesh_renderer;
        private GameObject fluid_mesh_obj = null;

        #region �޲z����F�~����T�A���A�C�����ݨD���n�ݤ@���F�~����
        private bool met_neighbors = false;
        private (Chunk up, Chunk down, Chunk left, Chunk right, Chunk forward, Chunk back) neighbors;
        #endregion

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

        #region Chunk ��l��
        /// <summary>
        /// �M�w block_types �M crack_states �����e�A�|����ګغc Block
        /// </summary>
        /// <param name="dimensions"></param>
        /// <param name="location"></param>
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

        /// <summary>
        /// �ؤo�Ѽƪ�l��
        /// </summary>
        /// <param name="dimensions">���e���ؤo</param>
        /// <param name="location">�@�ɮy��</param>
        public void locate(Vector3Int dimensions, Vector3Int location)
        {
            this.location = location;

            WIDTH = dimensions.x;
            HEIGHT = dimensions.y;
            DEPTH = dimensions.z;
            blocks = new Block[WIDTH, HEIGHT, DEPTH];
            n_block = WIDTH * HEIGHT * DEPTH;
        }
        #endregion

        #region �غc Chunk Mesh (�ƥ��]�m�����F�~�A�ٲ��C���߰ݾF�~���֪��y�{)
        public void build()
        {
            buildMesh(obj: ref solid_mesh_obj, mesh_type: "Solid");
            buildMesh(obj: ref fluid_mesh_obj, mesh_type: "Fluid");
        }

        private void buildMesh(ref GameObject obj, string mesh_type = "Solid")
        {
            //if (!met_neighbors)
            //{
            //    return;
            //}

            // TODO: �אּ�����ܼơA�קK���� GetComponent
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
                        obj.layer = LayerMask.NameToLayer("Water");
                        break;
                }
            }
            else
            {
                mesh_filter = obj.GetComponent<MeshFilter>();

                // �קK�᭱�S���ƲK�[ Collider
                DestroyImmediate(obj.GetComponent<Collider>());
            }

            List<Mesh> input_mesh_datas = new List<Mesh>();
            int vertex_index_offset = 0, triangle_index_offset = 0, idx = 0;
            int n_vertex, n_triangle, block_idx;
            Block block;
            bool condition0, condition1;

            ProcessMeshDataJob job = new ProcessMeshDataJob();
            job.vertex_index_offsets = new NativeArray<int>(n_block, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            job.triangle_index_offsets = new NativeArray<int>(n_block, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            Vector3Int xyz, offset;
            BlockType block_type;
            CrackState crack_state;

            for (block_idx = 0; block_idx < n_block; block_idx++)
            {
                xyz = Utils.flatToVector3Int(i: block_idx, width: WIDTH, height: HEIGHT);
                offset = xyz + location;
                block_type = block_types[block_idx];
                crack_state = crack_states[block_idx];

                block = new Block(block_type: block_type,
                                  crack_state: crack_state,
                                  offset: offset,
                                  chunk: this,
                                  up: neighbors.up,
                                  down: neighbors.down,
                                  left: neighbors.left,
                                  right: neighbors.right,
                                  forward: neighbors.forward,
                                  back: neighbors.back);

                blocks[xyz.x, xyz.y, xyz.z] = block;

                condition0 = block.mesh != null;
                condition1 = false;

                // �Ϥ��o�̬O Solid �٬O Fluid 
                switch (mesh_type)
                {
                    case "Solid":
                        condition1 = !MeshUtils.canSpread(block_type);
                        break;

                    case "Fluid":
                        condition1 = MeshUtils.canSpread(block_type);
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

            #region Job
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
             * �A�u��b�D�u�{���ե� Schedule �M Complete ��k�C�p�G�@�� job �ݭn�̿��t�@�ӡA�ϥ� JobHandle �ӳB�z�̿����t�Ӥ��O���զb job ���ի׷s�� job�C */
            handle.Complete();

            job.output_mesh_data.subMeshCount = 1;
            job.output_mesh_data.SetSubMesh(0, sm);

            // �q�L Mesh.ApplyAndDisposeWritableMeshData ���f��Ȧ^ Mesh
            // inputMeshes -> jobs.meshData -> jobs.outputMesh -> outputMeshData -> newMesh
            Mesh.ApplyAndDisposeWritableMeshData(output_mesh_datas, new[] { mesh });

            job.input_mesh_datas.Dispose();
            job.vertex_index_offsets.Dispose();
            job.triangle_index_offsets.Dispose();
            #endregion

            mesh.RecalculateBounds();

            // ��s mesh_filter �� mesh
            mesh_filter.mesh = mesh;

            // �� Chunk �U�� Block �o���ܤơA�ݭn��ø Chunk �ɡAMeshCollider �|�Q�R���A�]���C�����ݭn���s�K�[
            MeshCollider collider = obj.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
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
        #endregion

        #region �غc Chunk Mesh (�C�����泣�ݶǤJ�����F�~)
        public void buildConsiderAround(Chunk up, Chunk down, Chunk left, Chunk right, Chunk forward, Chunk back)
        {
            buildMeshConsiderAround(up, down, left, right, forward, back, ref solid_mesh_obj, mesh_type: "Solid");
            buildMeshConsiderAround(up, down, left, right, forward, back, ref fluid_mesh_obj, mesh_type: "Fluid");
        }

        private void buildMeshConsiderAround(Chunk up, Chunk down, Chunk left, Chunk right, Chunk forward, Chunk back, ref GameObject obj, string mesh_type = "Solid")
        {
            // TODO: �אּ�����ܼơA�קK���� GetComponent
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
                        obj.layer = LayerMask.NameToLayer("Water");
                        break;
                }
            }
            else
            {
                mesh_filter = obj.GetComponent<MeshFilter>();

                // �קK�᭱�S���ƲK�[ Collider
                DestroyImmediate(obj.GetComponent<Collider>());
            }

            List<Mesh> input_mesh_datas = new List<Mesh>();
            int vertex_index_offset = 0, triangle_index_offset = 0, idx = 0;
            int n_vertex, n_triangle, block_idx;
            Block block;
            bool condition0, condition1;

            ProcessMeshDataJob job = new ProcessMeshDataJob();
            job.vertex_index_offsets = new NativeArray<int>(n_block, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            job.triangle_index_offsets = new NativeArray<int>(n_block, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            Vector3Int xyz, offset;
            BlockType block_type;
            CrackState crack_state;

            for (block_idx = 0; block_idx < n_block; block_idx++)
            {
                xyz = Utils.flatToVector3Int(i: block_idx, width: WIDTH, height: HEIGHT);
                offset = xyz + location;
                block_type = block_types[block_idx];
                crack_state = crack_states[block_idx];

                // Block(block_type, crack_state, offset, chunk, up, down, left, right, forward, back)
                block = new Block(block_type: block_type,
                                  crack_state: crack_state,
                                  offset: offset,
                                  chunk: this,
                                  up: up,
                                  down: down,
                                  left: left,
                                  right: right,
                                  forward: forward,
                                  back: back);

                blocks[xyz.x, xyz.y, xyz.z] = block;

                condition0 = block.mesh != null;
                condition1 = false;

                // �Ϥ��o�̬O Solid �٬O Fluid 
                switch (mesh_type)
                {
                    case "Solid":
                        condition1 = !MeshUtils.canSpread(block_type);
                        break;

                    case "Fluid":
                        condition1 = MeshUtils.canSpread(block_type);
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

            #region Job
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
            #endregion

            mesh.RecalculateBounds();

            // ��s mesh_filter �� mesh
            mesh_filter.mesh = mesh;

            // �� Chunk �U�� Block �o���ܤơA�ݭn��ø Chunk �ɡAMeshCollider �|�Q�R���A�]���C�����ݭn���s�K�[
            MeshCollider collider = obj.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
        }

        /// <summary>
        /// �� �s�W �� �}�a �����A�I�s���禡�A�H���sø�s Chunk
        /// </summary>
        public void rebuildConsiderAround(Chunk up, Chunk down, Chunk left, Chunk right, Chunk forward, Chunk back)
        {
            DestroyImmediate(GetComponent<MeshFilter>());
            DestroyImmediate(GetComponent<MeshRenderer>());
            DestroyImmediate(GetComponent<Collider>());
            buildConsiderAround(up, down, left, right, forward, back);
        } 
        #endregion

        #region Chunk & Block
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
        /// �B�z Chunk ��ɹ� Block ���ޭȪ��B�z�A��W�X��e Chunk �ɡA���V�U�@�� Chunk �íץ� Block ���ޭȡC
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
        #endregion

        #region Block
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
                Vector3Int v = flatToVector3Int(index);
                Debug.LogError($"{index}/{block_types.Length}; {v}/({WIDTH}, {HEIGHT}, {DEPTH})");
            }
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

            if (block_types[index] != BlockType.AIR)
            {
                crack_states[index] = CrackState.None;
                rebuild();
            }
        }

        public Vector3Int flatToVector3Int(int i)
        {
            return Utils.flatToVector3Int(i, width: WIDTH, height: HEIGHT);
        } 
        #endregion

        #region �޲z����F�~����T
        public bool hasMetNeighbors()
        {
            return met_neighbors;
        }

        /// <summary>
        /// �]�m�����F�~�A�q�`�|���ˬd�O�_�]�m�L�A�Y�]�m�L�N�|�ٲ����B�J�C
        /// �Y���� Chunk �|�ʺA�ͦ��A���N�ݭn�T�O�����F�~���s�b�A�~���~���s�F�~
        /// </summary>
        /// <param name="neighbors"></param>
        public void setNeighbors((Chunk up, Chunk down, Chunk left, Chunk right, Chunk forward, Chunk back) neighbors)
        {
            if(neighbors.up && neighbors.down && neighbors.left && neighbors.right && neighbors.forward && neighbors.back)
            {
                met_neighbors = true;
            }

            this.neighbors = neighbors;
        }
        #endregion

        public IEnumerable<(Vector3Int, BlockType)> iterVegetations()
        {
            int n_block = block_types.Length;
            Vector3Int block_pos, base_pos;

            for (int i = 0; i < n_block; i++)
            {
                if (block_types[i] == BlockType.WOODBASE)
                {
                    base_pos = flatToVector3Int(i);

                    foreach ((Vector3Int, BlockType) tree in tree_design)
                    {
                        block_pos = base_pos + tree.Item1;
                        yield return (block_pos, tree.Item2);
                    }
                }
                else if (block_types[i] == BlockType.CACTUSBASE)
                {
                    base_pos = flatToVector3Int(i);

                    foreach ((Vector3Int, BlockType) cactus in cactus_design)
                    {
                        block_pos = base_pos + cactus.Item1;
                        yield return (block_pos, cactus.Item2);
                    }
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


        [Obsolete("���Ѧb Chunk ���إ� Block �ɡA��U�P�_�U�� Mesh �O�_�ݭn�K�[")]
        (bool is_inside, int nx, int ny, int nz) getNeighbourInfo(int bx, int by, int bz, BlockSide side)
        {
            bool is_inside = true;

            switch (side)
            {
                case BlockSide.Right:
                    bx += 1;

                    if (bx >= WIDTH)
                    {
                        is_inside = false;
                        bx = 0;
                    }
                    break;

                case BlockSide.Left:
                    bx -= 1;
                    is_inside = bx >= 0;

                    if (bx < 0)
                    {
                        is_inside = false;
                        bx = WIDTH - 1;
                    }
                    break;

                case BlockSide.Top:
                    by += 1;

                    if (by >= HEIGHT)
                    {
                        is_inside = false;
                        by = 0;
                    }
                    break;

                case BlockSide.Bottom:
                    by -= 1;

                    if (by < 0)
                    {
                        is_inside = false;
                        by = HEIGHT - 1;
                    }
                    break;

                case BlockSide.Front:
                    bz += 1;

                    if (bz >= DEPTH)
                    {
                        is_inside = false;
                        bz = 0;
                    }
                    break;

                case BlockSide.Back:
                    bz -= 1;

                    if (bz < 0)
                    {
                        is_inside = false;
                        bz = DEPTH - 1;
                    }
                    break;
            }

            return (is_inside, bx, by, bz);
        }

        [Obsolete("���Ѧb Chunk ���إ� Block �ɡA��U�P�_�U�� Mesh �O�_�ݭn�K�[")]
        bool hasInsideNeighbour(int x, int y, int z, BlockType block_type)
        {
            int block_idx = Utils.xyzToFlat(x, y, z, width: WIDTH, depth: DEPTH);

            if (getBlockType(block_idx).Equals(block_type))
            {
                return true;
            }

            if (getBlockType(block_idx).Equals(BlockType.AIR) || getBlockType(block_idx).Equals(BlockType.WATER))
            {
                return false;
            }

            return true;
        }

        [Obsolete("���Ѧb Chunk ���إ� Block �ɡA��U�P�_�U�� Mesh �O�_�ݭn�K�[")]
        bool hasNeighbour(Chunk chunk, int x, int y, int z, BlockType block_type)
        {
            int block_idx = Utils.xyzToFlat(x, y, z, width: WIDTH, depth: DEPTH);

            if (chunk.getBlockType(block_idx).Equals(block_type))
            {
                return true;
            }

            if (chunk.getBlockType(block_idx).Equals(BlockType.AIR) || chunk.getBlockType(block_idx).Equals(BlockType.WATER))
            {
                return false;
            }

            return true;
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
        public NativeArray<CrackState> crack_states;

        // TODO: �쥻�C���}�_���H���Ƴ��|�ۦP�A�O�]���� Unity.Mathematics.Random �� seed ���O 1�A�]���u���ǤJ�H���� seed�A�æb Execute(int i) �~���إ� Unity.Mathematics.Random ����Y�i
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

            dig_cave = (int)Cluster.fBM3D(x: xyz.x, y: xyz.y, z: xyz.z,
                                          octaves: World.cave_cluster.octaves,
                                          scale: World.cave_cluster.scale,
                                          height_scale: World.cave_cluster.height_scale,
                                          height_offset: World.cave_cluster.height_offset);


            plant_tree = (int)Cluster.fBM3D(x: xyz.x, y: xyz.y, z: xyz.z,
                                            octaves: World.tree_cluster.octaves,
                                            scale: World.tree_cluster.scale,
                                            height_scale: World.tree_cluster.height_scale,
                                            height_offset: World.tree_cluster.height_offset);


            dessert_biome = (int)Cluster.fBM3D(x: xyz.x, y: xyz.y, z: xyz.z,
                                               octaves: World.biome_cluster.octaves,
                                               scale: World.biome_cluster.scale,
                                               height_scale: World.biome_cluster.height_scale,
                                               height_offset: World.biome_cluster.height_offset);


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

            // TODO: �ثe���}�ޥi��|����a��A�B�]�S���Ҽ{��O�_�O�a��A�]�ӳy���a���d�g�ӫD��a
            if (dig_cave < World.cave_cluster.boundary)
            {
                block_types[i] = BlockType.AIR;
                return;
            }

            // NOTE: �a��شӴӪ��ɡA�|���]�m�@�ӯS�� BlockType �е��A��ںشӮɤ]�|�N�ӯS�� BlockType �л\�A�i�קK���ƻ{�w�ݭn�ؾ�
            if (xyz.y == surface_height && xyz.y >= WATER_LINE)
            {
                if (dessert_biome < World.biome_cluster.boundary)
                {
                    block_types[i] = BlockType.SAND;

                    if (random.NextFloat(1) <= 0.01f)
                    {
                        block_types[i] = BlockType.CACTUSBASE;
                    }
                }
                else if (plant_tree < World.tree_cluster.boundary)
                {
                    block_types[i] = BlockType.FOREST;

                    // TODO: ��X�{���K��(���v)���ѥ~���]�m
                    if (random.NextFloat(1) <= 0.05f)
                    {
                        // Execute ���@���B�z�@�� Block�A�]���o�̶ȩ�m���A�ӫD�����ؤ@�ʾ�
                        block_types[i] = BlockType.WOODBASE;
                    }
                }
                else
                {
                    block_types[i] = BlockType.GRASSSIDE;
                }
            }

            else if ((diamond_bottom_height < xyz.y) && (xyz.y < diamond_top_height) && (random.NextFloat(1) <= World.diamond_top_strata.probability))
            {
                block_types[i] = BlockType.DIAMOND;
            }

            else if ((xyz.y < stone_height) && (random.NextFloat(1) <= World.stone_strata.probability))
            {
                block_types[i] = BlockType.STONE;
            }

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

