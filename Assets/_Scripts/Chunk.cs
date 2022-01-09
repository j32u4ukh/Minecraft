using System.Collections.Generic;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;


public class Chunk : MonoBehaviour
{
    public Material atlas;

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
    public MeshRenderer meshRenderer;

    CalculateBlockTypes calculateBlockTypes;
    JobHandle jobHandle;
    public NativeArray<Unity.Mathematics.Random> RandomArray { get; private set; }

    struct CalculateBlockTypes : IJobParallelFor
    {
        public NativeArray<MeshUtils.BlockType> cData;
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
                cData[i] = MeshUtils.BlockType.GRASSSIDE;
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

            else
            {
                cData[i] = MeshUtils.BlockType.AIR;
            }
        }
    }

    void BuildChunk()
    {
        int blockCount = width * depth * height;
        chunkData = new MeshUtils.BlockType[blockCount];
        NativeArray<MeshUtils.BlockType> blockTypes = new NativeArray<MeshUtils.BlockType>(chunkData, Allocator.Persistent);

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
            width = width,
            height = height,
            location = location,
            randoms = RandomArray
        };

        jobHandle = calculateBlockTypes.Schedule(chunkData.Length, 64);

        // Schedule 執行完才會執行這一行，若不加 jobHandle.Complete()，則會在背景繼續執行，也執行下方程式碼
        jobHandle.Complete();

        calculateBlockTypes.cData.CopyTo(chunkData);
        blockTypes.Dispose();
        RandomArray.Dispose();
    }

    private void Start()
    {
        
    }

    // Start is called before the first frame update
    public void CreateChunk(Vector3 dimensions, Vector3 position, bool rebuildBlocks = true)
    {
        location = position;
        width = (int)dimensions.x;
        height = (int)dimensions.y;
        depth = (int)dimensions.z;

        MeshFilter mf = gameObject.AddComponent<MeshFilter>();

        // TODO: 可將此處的 mr 用全域的 meshRenderer 取代
        MeshRenderer mr = gameObject.AddComponent<MeshRenderer>();
        meshRenderer = mr;

        mr.material = atlas;
        blocks = new Block[width, height, depth];

        if (rebuildBlocks)
        {
            BuildChunk();
        }

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
                    blocks[x, y, z] = new Block(new Vector3(x, y, z) + location, chunkData[chunk_idx], this, MeshUtils.BlockType.NOCRACK);

                    // 只將有 mesh 的 Block 加入 inputMeshes
                    if (blocks[x, y, z].mesh != null)
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
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, stream: 2));

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

        mf.mesh = newMesh;

        MeshCollider collider = gameObject.AddComponent<MeshCollider>();
        collider.sharedMesh = mf.mesh;
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

            var outputVerts = outputMesh.GetVertexData<Vector3>(stream: 0);
            var outputNormals = outputMesh.GetVertexData<Vector3>(stream: 1);
            var outputUVs = outputMesh.GetVertexData<Vector3>(stream: 2);

            for (int i = 0; i < vCount; i++)
            {
                outputVerts[i + vStart] = verts[i];
                outputNormals[i + vStart] = normals[i];
                outputUVs[i + vStart] = uvs[i];
            }

            /* NativeArray 使用後應呼叫 Dispose()，以避免記憶體溢出 */
            verts.Dispose();
            normals.Dispose();
            uvs.Dispose();

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

    // Update is called once per frame
    void Update()
    {
        
    }
}
