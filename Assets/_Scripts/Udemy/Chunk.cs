using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace udemy
{
    public class Chunk : MonoBehaviour
    {
        public Material atlas;

        // �y����������󳣷|�Q�γo�� Material
        public Material fluid;

        public int width = 2;
        public int height = 2;
        public int depth = 2;

        public Vector3 location;

        public Block[,,] blocks;
        // Flat[x + WIDTH * (y + DEPTH * z)] = Original[x, y, z]
        // x = i % WIDTH
        // y = (i / WIDTH) % HEIGHT
        // z = i / (WIDTH * HEIGHT)
        public MeshUtils.BlockType[] chunkData;
        public MeshUtils.BlockType[] healthData;

        public MeshRenderer meshRendererSolid;
        public MeshRenderer meshRendererFluid;
        GameObject mesh_obj_solid;
        GameObject mesh_obj_fluid;

        CalculateBlockTypes calculateBlockTypes;
        JobHandle jobHandle;
        public NativeArray<Unity.Mathematics.Random> RandomArray { get; private set; }

        struct CalculateBlockTypes : IJobParallelFor
        {
            public NativeArray<MeshUtils.BlockType> cData;
            public NativeArray<MeshUtils.BlockType> hData;
            public int width;
            public int height;
            public Vector3 location;

            public NativeArray<Unity.Mathematics.Random> randoms;

            public void Execute(int i)
            {
                int x = i % width + (int)location.x;
                int y = (i / width) % height + (int)location.y;
                int z = i / (width * height) + (int)location.z;

                var random = randoms[i];

                float surfaceHeight = (int)MeshUtils.fBM(x, z,
                                                         World.surfaceSettings.octaves,
                                                         World.surfaceSettings.scale,
                                                         World.surfaceSettings.heightScale,
                                                         World.surfaceSettings.heightOffset);

                float stoneHeight = (int)MeshUtils.fBM(x, z,
                                                       World.stoneSettings.octaves,
                                                       World.stoneSettings.scale,
                                                       World.stoneSettings.heightScale,
                                                       World.stoneSettings.heightOffset);

                float diamondTHeight = (int)MeshUtils.fBM(x, z,
                                                          World.diamondTSettings.octaves,
                                                          World.diamondTSettings.scale,
                                                          World.diamondTSettings.heightScale,
                                                          World.diamondTSettings.heightOffset);

                float diamondBHeight = (int)MeshUtils.fBM(x, z,
                                                          World.diamondBSettings.octaves,
                                                          World.diamondBSettings.scale,
                                                          World.diamondBSettings.heightScale,
                                                          World.diamondBSettings.heightOffset);

                float digCave = (int)MeshUtils.fBM3D(x, y, z,
                                                     World.caveSettings.octaves,
                                                     World.caveSettings.scale,
                                                     World.caveSettings.heightScale,
                                                     World.caveSettings.heightOffset);

                float plantTree = (int)MeshUtils.fBM3D(x, y, z,
                                                       World.treeSettings.octaves,
                                                       World.treeSettings.scale,
                                                       World.treeSettings.heightScale,
                                                       World.treeSettings.heightOffset);

                float desertBiome = (int)MeshUtils.fBM3D(x, y, z,
                                                         World.biomeSettings.octaves,
                                                         World.biomeSettings.scale,
                                                         World.biomeSettings.heightScale,
                                                         World.biomeSettings.heightOffset);

                int WATER_LINE = 16;

                hData[i] = MeshUtils.BlockType.NOCRACK;

                if (y == 0)
                {
                    cData[i] = MeshUtils.BlockType.BEDROCK;
                    return;
                }

                if (digCave < World.caveSettings.probability)
                {
                    cData[i] = MeshUtils.BlockType.AIR;
                    return;
                }

                if (y == surfaceHeight && y >= WATER_LINE)
                {
                    if (desertBiome < World.biomeSettings.probability)
                    {
                        cData[i] = MeshUtils.BlockType.SAND;

                        if (random.NextFloat(1) <= 0.1)
                        {
                            cData[i] = MeshUtils.BlockType.CACTUS;
                        }
                    }
                    else if (plantTree < World.treeSettings.probability)
                    {
                        cData[i] = MeshUtils.BlockType.FOREST;

                        if (random.NextFloat(1) <= 0.1)
                        {
                            // Execute ���@���B�z�@�� Block�A�]���o�̶ȩ�m���A�ӫD�����ؤ@�ʾ�
                            cData[i] = MeshUtils.BlockType.WOODBASE;
                        }
                    }
                    else
                    {
                        cData[i] = MeshUtils.BlockType.GRASSSIDE;
                    }
                }

                else if ((diamondBHeight < y) && (y < diamondTHeight) && (random.NextFloat(1) < World.diamondTSettings.probability))
                {
                    cData[i] = MeshUtils.BlockType.DIAMOND;
                }

                else if ((y < stoneHeight) && (random.NextFloat(1) < World.stoneSettings.probability))
                {
                    cData[i] = MeshUtils.BlockType.STONE;
                }

                else if (y < surfaceHeight)
                {
                    cData[i] = MeshUtils.BlockType.DIRT;
                }

                else if (y < WATER_LINE)
                {
                    cData[i] = MeshUtils.BlockType.WATER;
                }

                else
                {
                    cData[i] = MeshUtils.BlockType.AIR;
                }
            }
        }

        // BurstCompile �ݨϥ� .NET 4.0 �H�W
        [BurstCompile]
        struct ProcessMeshDataJob : IJobParallelFor
        {
            [ReadOnly] public Mesh.MeshDataArray meshData;
            public Mesh.MeshData outputMesh;
            public NativeArray<int> vertexStart;
            public NativeArray<int> triStart;

            public void Execute(int index)
            {
                var data = meshData[index];
                var vCount = data.vertexCount;
                var vStart = vertexStart[index];

                // Reinterpret<Vector3>: Ū�J Vector3�A�ഫ�� float3
                var verts = new NativeArray<float3>(vCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                data.GetVertices(verts.Reinterpret<Vector3>());

                var normals = new NativeArray<float3>(vCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                data.GetNormals(normals.Reinterpret<Vector3>());

                // uv �������O Vector2�A���b Job System �����ϥ� Vector3
                var uvs = new NativeArray<float3>(vCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                data.GetUVs(0, uvs.Reinterpret<Vector3>());

                var uvs2 = new NativeArray<float3>(vCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                data.GetUVs(1, uvs2.Reinterpret<Vector3>());

                var outputVerts = outputMesh.GetVertexData<Vector3>(stream: 0);
                var outputNormals = outputMesh.GetVertexData<Vector3>(stream: 1);
                var outputUVs = outputMesh.GetVertexData<Vector3>(stream: 2);
                var outputUVs2 = outputMesh.GetVertexData<Vector3>(stream: 3);

                for (int i = 0; i < vCount; i++)
                {
                    outputVerts[i + vStart] = verts[i];
                    outputNormals[i + vStart] = normals[i];
                    outputUVs[i + vStart] = uvs[i];
                    outputUVs2[i + vStart] = uvs2[i];
                }

                /* NativeArray �ϥΫ����I�s Dispose()�A�H�קK�O���鷸�X */
                verts.Dispose();
                normals.Dispose();
                uvs.Dispose();
                uvs2.Dispose();

                var tStart = triStart[index];
                var tCount = data.GetSubMesh(0).indexCount;
                var outputTris = outputMesh.GetIndexData<int>();

                // Android
                if (data.indexFormat == IndexFormat.UInt16)
                {
                    var tris = data.GetIndexData<ushort>();

                    for (int i = 0; i < tCount; ++i)
                    {
                        int idx = tris[i];
                        outputTris[i + tStart] = vStart + idx;
                    }
                }

                // IndexFormat.UInt32: PC
                else
                {
                    var tris = data.GetIndexData<int>();
                    for (int i = 0; i < tCount; ++i)
                    {
                        int idx = tris[i];
                        outputTris[i + tStart] = vStart + idx;
                    }
                }
            }
        }

        void BuildChunk()
        {
            int blockCount = width * depth * height;
            chunkData = new MeshUtils.BlockType[blockCount];
            healthData = new MeshUtils.BlockType[blockCount];
            NativeArray<MeshUtils.BlockType> blockTypes = new NativeArray<MeshUtils.BlockType>(chunkData, Allocator.Persistent);
            NativeArray<MeshUtils.BlockType> healthTypes = new NativeArray<MeshUtils.BlockType>(healthData, Allocator.Persistent);

            var randomArray = new Unity.Mathematics.Random[blockCount];
            var seed = new System.Random();

            for (int i = 0; i < blockCount; i++)
            {
                randomArray[i] = new Unity.Mathematics.Random((uint)seed.Next());
            }

            RandomArray = new NativeArray<Unity.Mathematics.Random>(randomArray, Allocator.Persistent);

            calculateBlockTypes = new CalculateBlockTypes()
            {
                cData = blockTypes,
                hData = healthTypes,
                width = width,
                height = height,
                location = location,
                randoms = RandomArray
            };

            jobHandle = calculateBlockTypes.Schedule(chunkData.Length, 64);

            // Schedule ���槹�~�|����o�@��A�Y���[ jobHandle.Complete()�A�h�|�b�I���~�����A�]����U��{���X
            jobHandle.Complete();

            calculateBlockTypes.cData.CopyTo(chunkData);
            calculateBlockTypes.hData.CopyTo(healthData);
            blockTypes.Dispose();
            healthTypes.Dispose();
            RandomArray.Dispose();

            BuildTrees();
        }

        // Start is called before the first frame update
        public void CreateChunk(Vector3 dimensions, Vector3 position, bool rebuildBlocks = true)
        {
            location = position;
            width = (int)dimensions.x;
            height = (int)dimensions.y;
            depth = (int)dimensions.z;

            // �i�N���B�� mrs, mrf �Υ��쪺 meshRendererSolid, meshRendererFluid ���N�C�ڪ��u�ƪ��w�ĥΡC
            // �T��(Solid)��� Mesh
            MeshFilter mesh_filter_solid;
            MeshRenderer mesh_renderer_solid;

            // �y��(Fluid)��� Mesh
            MeshFilter mesh_filter_fluid;
            MeshRenderer mesh_renderer_fluid;

            if (mesh_obj_solid == null)
            {
                mesh_obj_solid = new GameObject("Solid");
                mesh_obj_solid.transform.parent = transform;
                mesh_filter_solid = mesh_obj_solid.AddComponent<MeshFilter>();
                mesh_renderer_solid = mesh_obj_solid.AddComponent<MeshRenderer>();
                meshRendererSolid = mesh_renderer_solid;
                mesh_renderer_solid.material = atlas;
            }
            else
            {
                mesh_filter_solid = mesh_obj_solid.GetComponent<MeshFilter>();
                DestroyImmediate(mesh_obj_solid.GetComponent<Collider>());
            }

            if (mesh_obj_fluid == null)
            {
                mesh_obj_fluid = new GameObject("Fluid");
                mesh_obj_fluid.transform.parent = transform;
                mesh_filter_fluid = mesh_obj_fluid.AddComponent<MeshFilter>();
                mesh_renderer_fluid = mesh_obj_fluid.AddComponent<MeshRenderer>();
                mesh_obj_fluid.AddComponent<UVScroller>();
                meshRendererFluid = mesh_renderer_fluid;
                mesh_renderer_fluid.material = fluid;
            }
            else
            {
                mesh_filter_fluid = mesh_obj_fluid.GetComponent<MeshFilter>();
                DestroyImmediate(mesh_obj_fluid.GetComponent<Collider>());
            }

            blocks = new Block[width, height, depth];

            if (rebuildBlocks)
            {
                BuildChunk();
            }

            for (int pass = 0; pass < 2; pass++)
            {
                List<Mesh> inputMeshes = new List<Mesh>();
                int vertexStart = 0;
                int triStart = 0;
                int meshCount = width * height * depth;
                int m = 0;

                // Job ���ƾڤ��|�Q�s�W�ΧR���A�]���ǤJ�̤j�i�� Mesh �Ӽ� width * height * depth
                var jobs = new ProcessMeshDataJob();
                jobs.vertexStart = new NativeArray<int>(meshCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                jobs.triStart = new NativeArray<int>(meshCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                for (int z = 0; z < depth; z++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int chunk_idx = x + width * (y + depth * z);
                            blocks[x, y, z] = new Block(new Vector3(x, y, z) + location, chunkData[chunk_idx], this, healthData[chunk_idx]);

                            // �u�N�� mesh �� Block �[�J inputMeshes
                            /* [condition]
                             * condition1: blocks[x, y, z].mesh != null
                             * condition2-1: (pass == 0) && !MeshUtils.canFlow.Contains(chunkData[chunk_idx])
                             * condition2-2: (pass == 1) && MeshUtils.canFlow.Contains(chunkData[chunk_idx])
                             * condition2: condition2-1 || condition2-2
                             * condition: condition1 && condition2
                             */
                            if (blocks[x, y, z].mesh != null &&
                                (((pass == 0) && !MeshUtils.canFlow.Contains(chunkData[chunk_idx])) ||
                                ((pass == 1) && MeshUtils.canFlow.Contains(chunkData[chunk_idx]))))
                            {
                                inputMeshes.Add(blocks[x, y, z].mesh);

                                var vcount = blocks[x, y, z].mesh.vertexCount;

                                // ���o�T���γ��I�ƶq
                                var icount = (int)blocks[x, y, z].mesh.GetIndexCount(0);

                                jobs.vertexStart[m] = vertexStart;
                                jobs.triStart[m] = triStart;
                                vertexStart += vcount;
                                triStart += icount;
                                m++;
                            }
                        }
                    }
                }

                // inputMeshes -> jobs.meshData -> jobs.outputMesh -> outputMeshData -> newMesh
                jobs.meshData = Mesh.AcquireReadOnlyMeshData(inputMeshes);

                // Mesh.AllocateWritableMeshData ���t�@�ӥi�g������ƾڡA�M��q�L jobs �i�泻�I�ާ@�A
                Mesh.MeshDataArray outputMeshData = Mesh.AllocateWritableMeshData(1);

                // inputMeshes -> jobs.meshData -> jobs.outputMesh -> outputMeshData -> newMesh
                jobs.outputMesh = outputMeshData[0];
                jobs.outputMesh.SetIndexBufferParams(triStart, IndexFormat.UInt32);

                // �o�̪� stream �����ǡA���M ProcessMeshDataJob �� GetVertexData �� stream �����ǬۦP
                jobs.outputMesh.SetVertexBufferParams(
                    vertexStart,
                    new VertexAttributeDescriptor(VertexAttribute.Position, stream: 0),
                    new VertexAttributeDescriptor(VertexAttribute.Normal, stream: 1),
                    new VertexAttributeDescriptor(VertexAttribute.TexCoord0, stream: 2),
                    new VertexAttributeDescriptor(VertexAttribute.TexCoord1, stream: 3));

                /* �b���T���ɶ��ե� Schedule �M Complete
                 * �@���A�֦��F�@�� job �һݪ��ƾڡA�ɥi��֦a�b job �W�ե� Schedule�A�b�A�ݭn�������浲�G���e���n�ե� Complete�C
                 * �@�Ө}�n�����O�իפ@�ӧA���ݭn���ݪ� job�A�P�ɥ����|�P��e���b�B�檺��Ljob�����v���C
                 * �|�Ҩӻ��A�p�G�A�b�@�V�����M�U�@�V�}�l���e�֦��@�q�S����L job �b�B�檺�ɶ��A�åB�i�H�����@�V������A�A�i�H�b�@�V�������ɭԽիפ@�� job�A�b�U�@�V���ϥΥ������G�C
                 * �Ϊ̡A�p�G�o���ഫ�ɶ��w�g�Q��L job �e���F�A���O�b�@�V�����@�j�q���R���Q�Ϊ��ɬq�A�b�o���իקA�� job �|�󦳮Ĳv�C
                 * 
                 * job �֦��@�� Run ��k�A�A�i�H�Υ��Ӵ��N Schedule �q�����D�u�{�ߨ����o�� job�C�A�i�H�ϥΥ��ӹF��ոեت��C
                 */
                var handle = jobs.Schedule(inputMeshes.Count, 4);
                var newMesh = new Mesh();
                newMesh.name = $"Chunk_{location.x}_{location.y}_{location.z}";

                var sm = new SubMeshDescriptor(0, triStart, MeshTopology.Triangles);
                sm.firstVertex = 0;
                sm.vertexCount = vertexStart;

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

                jobs.outputMesh.subMeshCount = 1;
                jobs.outputMesh.SetSubMesh(0, sm);

                // �q�L Mesh.ApplyAndDisposeWritableMeshData ���f��Ȧ^ Mesh
                // inputMeshes -> jobs.meshData -> jobs.outputMesh -> outputMeshData -> newMesh
                Mesh.ApplyAndDisposeWritableMeshData(outputMeshData, new[] { newMesh });

                jobs.meshData.Dispose();
                jobs.vertexStart.Dispose();
                jobs.triStart.Dispose();
                newMesh.RecalculateBounds();

                // (pass: 0)���J�T���� Mesh
                if (pass == 0)
                {
                    mesh_filter_solid.mesh = newMesh;
                    MeshCollider collider = mesh_obj_solid.AddComponent<MeshCollider>();
                    collider.sharedMesh = mesh_filter_solid.mesh;
                }

                // (pass: 1)���J�y���� Mesh
                else
                {
                    mesh_filter_fluid.mesh = newMesh;
                    MeshCollider collider = mesh_obj_fluid.AddComponent<MeshCollider>();
                    mesh_obj_fluid.layer = 4;
                    collider.sharedMesh = mesh_filter_fluid.mesh;
                }
            }
        }

        (Vector3Int, MeshUtils.BlockType)[] treeDesign = new (Vector3Int, MeshUtils.BlockType)[] {
        (new Vector3Int(-1,2,-1), MeshUtils.BlockType.LEAVES),
        (new Vector3Int(0,2,-1), MeshUtils.BlockType.LEAVES),
        (new Vector3Int(0,3,-1), MeshUtils.BlockType.LEAVES),
        (new Vector3Int(1,3,-1), MeshUtils.BlockType.LEAVES),
        (new Vector3Int(-1,4,-1), MeshUtils.BlockType.LEAVES),
        (new Vector3Int(0,4,-1), MeshUtils.BlockType.LEAVES),
        (new Vector3Int(0,5,-1), MeshUtils.BlockType.LEAVES),
        (new Vector3Int(0,0,0), MeshUtils.BlockType.WOOD),
        (new Vector3Int(0,1,0), MeshUtils.BlockType.WOOD),
        (new Vector3Int(-1,2,0), MeshUtils.BlockType.LEAVES),
        (new Vector3Int(0,2,0), MeshUtils.BlockType.WOOD),
        (new Vector3Int(1,2,0), MeshUtils.BlockType.LEAVES),
        (new Vector3Int(-1,3,0), MeshUtils.BlockType.LEAVES),
        (new Vector3Int(0,3,0), MeshUtils.BlockType.WOOD),
        (new Vector3Int(1,3,0), MeshUtils.BlockType.LEAVES),
        (new Vector3Int(-1,4,0), MeshUtils.BlockType.LEAVES),
        (new Vector3Int(0,4,0), MeshUtils.BlockType.WOOD),
        (new Vector3Int(1,4,0), MeshUtils.BlockType.LEAVES),
        (new Vector3Int(-1,5,0), MeshUtils.BlockType.LEAVES),
        (new Vector3Int(0,5,0), MeshUtils.BlockType.LEAVES),
        (new Vector3Int(1,5,0), MeshUtils.BlockType.LEAVES),
        (new Vector3Int(0,2,1), MeshUtils.BlockType.LEAVES),
        (new Vector3Int(1,2,1), MeshUtils.BlockType.LEAVES),
        (new Vector3Int(-1,3,1), MeshUtils.BlockType.LEAVES),
        (new Vector3Int(0,3,1), MeshUtils.BlockType.LEAVES),
        (new Vector3Int(0,4,1), MeshUtils.BlockType.LEAVES),
        (new Vector3Int(1,4,1), MeshUtils.BlockType.LEAVES),
        (new Vector3Int(0,5,1), MeshUtils.BlockType.LEAVES)
    };

        (Vector3Int, MeshUtils.BlockType)[] cactusDesign = new (Vector3Int, MeshUtils.BlockType)[] {
                                            (new Vector3Int(0,0,0), MeshUtils.BlockType.WOOD),
                                            (new Vector3Int(0,1,0), MeshUtils.BlockType.GRASSTOP),
                                            (new Vector3Int(-2,2,0), MeshUtils.BlockType.GRASSTOP),
                                            (new Vector3Int(-1,2,0), MeshUtils.BlockType.GRASSTOP),
                                            (new Vector3Int(0,2,0), MeshUtils.BlockType.GRASSTOP),
                                            (new Vector3Int(-2,3,0), MeshUtils.BlockType.GRASSTOP),
                                            (new Vector3Int(0,3,0), MeshUtils.BlockType.GRASSTOP),
                                            (new Vector3Int(1,3,0), MeshUtils.BlockType.GRASSTOP),
                                            (new Vector3Int(2,3,0), MeshUtils.BlockType.GRASSTOP),
                                            (new Vector3Int(-2,4,0), MeshUtils.BlockType.GRASSTOP),
                                            (new Vector3Int(0,4,0), MeshUtils.BlockType.GRASSTOP),
                                            (new Vector3Int(2,4,0), MeshUtils.BlockType.GRASSTOP),
                                            (new Vector3Int(0,5,0), MeshUtils.BlockType.GRASSTOP)
    };

        void BuildTrees()
        {
            for (int i = 0; i < chunkData.Length; i++)
            {
                if (chunkData[i] == MeshUtils.BlockType.WOODBASE)
                {
                    foreach ((Vector3Int, MeshUtils.BlockType) v in treeDesign)
                    {
                        Vector3Int blockPos = World.FromFlat(i) + v.Item1;
                        int bIndex = World.ToFlat(blockPos);

                        // �o�Ӫ������Y��n�b Chunk ����ɤW�A�h�|�Q�����C�ڪ��u�ƪ��w�ѨM�C
                        if ((0 <= bIndex) && (bIndex < chunkData.Length))
                        {
                            chunkData[bIndex] = v.Item2;
                            healthData[bIndex] = MeshUtils.BlockType.NOCRACK;
                        }
                    }
                }
                else if (chunkData[i] == MeshUtils.BlockType.CACTUS)
                {
                    foreach ((Vector3Int, MeshUtils.BlockType) v in cactusDesign)
                    {
                        Vector3Int blockPos = World.FromFlat(i) + v.Item1;
                        int bIndex = World.ToFlat(blockPos);

                        if ((0 <= bIndex) && (bIndex < chunkData.Length))
                        {
                            chunkData[bIndex] = v.Item2;
                            healthData[bIndex] = MeshUtils.BlockType.NOCRACK;
                        }
                    }
                }
            }
        }
    }

}