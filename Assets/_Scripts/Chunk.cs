using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

public class Chunk : MonoBehaviour
{
    public Material atlas;

    // 流體相關的物件都會利用這個 Material
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
    GameObject solidMesh;
    GameObject fluidMesh;

    CalculateBlockTypes calculateBlockTypes;
    JobHandle jobHandle;
    public NativeArray<Unity.Mathematics.Random> RandomArray { get; private set; }

    private void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    struct CalculateBlockTypes : IJobParallelFor
    {
        public NativeArray<MeshUtils.BlockType> cData;
        public NativeArray<MeshUtils.BlockType> hData;
        public int width;
        public int height;
        public Vector3 location;

        // TODO: 原本每次開起的隨機數都會相同，是因為給 Unity.Mathematics.Random 的 seed 都是 1，因此只須傳入隨機的 seed，並在 Execute(int i) 外部建立 Unity.Mathematics.Random 物件即可
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

            hData[i] = MeshUtils.BlockType.NOCRACK;

            if (y == 0)
            {
                cData[i] = MeshUtils.BlockType.BEDROCK;
                return;
            }

            // TODO: 目前的洞穴可能會挖到地表，且因沒有考慮到是否是地表，因而造成地表為泥土而非草地
            if (digCave < World.caveSettings.probability)
            {
                cData[i] = MeshUtils.BlockType.AIR;
                return;
            }

            if (y == surfaceHeight)
            {
                if (plantTree < World.treeSettings.probability && random.NextFloat(1) <= 0.1)
                {
                    // Execute 當中一次處理一個 Block，因此這裡僅放置樹基，而非直接種一棵樹
                    cData[i] = MeshUtils.BlockType.WOODBASE;
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

            // TODO: 實際數值要根據地形高低來做調整
            // TODO: 如何確保水是自己一個區塊，而非隨機的散佈在地圖中？大概要像樹一樣，使用 fBM3D
            else if (y < 20)
            {
                cData[i] = MeshUtils.BlockType.WATER;
            }

            else
            {
                cData[i] = MeshUtils.BlockType.AIR;
            }
        }
    }

    // BurstCompile 需使用 .NET 4.0 以上
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

            // Reinterpret<Vector3>: 讀入 Vector3，轉換成 float3
            var verts = new NativeArray<float3>(vCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            data.GetVertices(verts.Reinterpret<Vector3>());

            var normals = new NativeArray<float3>(vCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            data.GetNormals(normals.Reinterpret<Vector3>());

            // uv 本身雖是 Vector2，但在 Job System 中應使用 Vector3
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

            /* NativeArray 使用後應呼叫 Dispose()，以避免記憶體溢出 */
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

        // Schedule 執行完才會執行這一行，若不加 jobHandle.Complete()，則會在背景繼續執行，也執行下方程式碼
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

        // TODO: 可將此處的 mrs, mrf 用全域的 meshRendererSolid, meshRendererFluid 取代
        // 固體(Solid)方塊 Mesh
        MeshFilter mfs;
        MeshRenderer mrs;

        // 流體(Fluid)方塊 Mesh
        MeshFilter mff;
        MeshRenderer mrf;

        if(solidMesh == null)
        {
            solidMesh = new GameObject("Solid");
            solidMesh.transform.parent = transform;
            mfs = solidMesh.AddComponent<MeshFilter>();
            mrs = solidMesh.AddComponent<MeshRenderer>();
            meshRendererSolid = mrs;
            mrs.material = atlas;
        }
        else
        {
            mfs = solidMesh.GetComponent<MeshFilter>();
            DestroyImmediate(solidMesh.GetComponent<Collider>());
        }

        if(fluidMesh == null)
        {
            fluidMesh = new GameObject("Fluid");
            fluidMesh.transform.parent = transform;
            mff = fluidMesh.AddComponent<MeshFilter>();
            mrf = fluidMesh.AddComponent<MeshRenderer>();
            meshRendererSolid = mrf;
            mrf.material = fluid;
        }
        else
        {
            mff = fluidMesh.GetComponent<MeshFilter>();
            DestroyImmediate(fluidMesh.GetComponent<Collider>());
        }

        blocks = new Block[width, height, depth];

        if (rebuildBlocks)
        {
            BuildChunk();
        }

        for(int pass = 0; pass < 2; pass++)
        {
            var inputMeshes = new List<Mesh>();
            int vertexStart = 0;
            int triStart = 0;
            int meshCount = width * height * depth;
            int m = 0;

            // Job 當中數據不會被新增或刪除，因此傳入最大可能 Mesh 個數 width * height * depth
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

                        // 只將有 mesh 的 Block 加入 inputMeshes
                        /* [condition]
                         * condition1: blocks[x, y, z].mesh != null
                         * condition2-1: (pass == 0) && !MeshUtils.canFlow.Contains(chunkData[chunk_idx])
                         * condition2-2: (pass == 1) && MeshUtils.canFlow.Contains(chunkData[chunk_idx])
                         * condition2: condition2-1 || condition2-2
                         * condition: condition1 && condition2
                         */
                        if (blocks[x, y, z].mesh != null && 
                            (((pass == 0) && !MeshUtils.canFlow.Contains(chunkData[chunk_idx]))||
                            ((pass == 1) && MeshUtils.canFlow.Contains(chunkData[chunk_idx]))))
                        {
                            inputMeshes.Add(blocks[x, y, z].mesh);
                            var vcount = blocks[x, y, z].mesh.vertexCount;

                            // 取得三角形數量
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

            jobs.meshData = Mesh.AcquireReadOnlyMeshData(inputMeshes);
            var outputMeshData = Mesh.AllocateWritableMeshData(1);
            jobs.outputMesh = outputMeshData[0];
            jobs.outputMesh.SetIndexBufferParams(triStart, IndexFormat.UInt32);

            // 這裡的 stream 的順序，應和 ProcessMeshDataJob 當中 GetVertexData 的 stream 的順序相同
            jobs.outputMesh.SetVertexBufferParams(
                vertexStart,
                new VertexAttributeDescriptor(VertexAttribute.Position, stream: 0),
                new VertexAttributeDescriptor(VertexAttribute.Normal, stream: 1),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, stream: 2),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord1, stream: 3));

            var handle = jobs.Schedule(inputMeshes.Count, 4);
            var newMesh = new Mesh();
            newMesh.name = $"Chunk_{location.x}_{location.y}_{location.z}";

            var sm = new SubMeshDescriptor(0, triStart, MeshTopology.Triangles);
            sm.firstVertex = 0;
            sm.vertexCount = vertexStart;
            handle.Complete();

            jobs.outputMesh.subMeshCount = 1;
            jobs.outputMesh.SetSubMesh(0, sm);
            Mesh.ApplyAndDisposeWritableMeshData(outputMeshData, new[] { newMesh });
            jobs.meshData.Dispose();
            jobs.vertexStart.Dispose();
            jobs.triStart.Dispose();
            newMesh.RecalculateBounds();

            if(pass == 0)
            {
                mfs.mesh = newMesh;
                MeshCollider collider = solidMesh.AddComponent<MeshCollider>();
                collider.sharedMesh = mfs.mesh;
            }
            else
            {
                mff.mesh = newMesh;
                MeshCollider collider = fluidMesh.AddComponent<MeshCollider>();
                collider.sharedMesh = mff.mesh;
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

    void BuildTrees()
    {
        for(int i = 0; i < chunkData.Length; i++)
        {
            if(chunkData[i] == MeshUtils.BlockType.WOODBASE)
            {
                foreach ((Vector3Int, MeshUtils.BlockType) v in treeDesign)
                {
                    Vector3Int blockPos = World.FromFlat(i) + v.Item1;
                    int bIndex = World.ToFlat(blockPos);

                    // TODO: 目前樹木若剛好在 Chunk 的邊界上，則會被切掉
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
