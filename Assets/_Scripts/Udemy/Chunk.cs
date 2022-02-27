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
                            // Execute 當中一次處理一個 Block，因此這裡僅放置樹基，而非直接種一棵樹
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

            // 可將此處的 mrs, mrf 用全域的 meshRendererSolid, meshRendererFluid 取代。我的優化版已採用。
            // 固體(Solid)方塊 Mesh
            MeshFilter mesh_filter_solid;
            MeshRenderer mesh_renderer_solid;

            // 流體(Fluid)方塊 Mesh
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
                                (((pass == 0) && !MeshUtils.canFlow.Contains(chunkData[chunk_idx])) ||
                                ((pass == 1) && MeshUtils.canFlow.Contains(chunkData[chunk_idx]))))
                            {
                                inputMeshes.Add(blocks[x, y, z].mesh);

                                var vcount = blocks[x, y, z].mesh.vertexCount;

                                // 取得三角形頂點數量
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

                // Mesh.AllocateWritableMeshData 分配一個可寫的網格數據，然後通過 jobs 進行頂點操作，
                Mesh.MeshDataArray outputMeshData = Mesh.AllocateWritableMeshData(1);

                // inputMeshes -> jobs.meshData -> jobs.outputMesh -> outputMeshData -> newMesh
                jobs.outputMesh = outputMeshData[0];
                jobs.outputMesh.SetIndexBufferParams(triStart, IndexFormat.UInt32);

                // 這裡的 stream 的順序，應和 ProcessMeshDataJob 當中 GetVertexData 的 stream 的順序相同
                jobs.outputMesh.SetVertexBufferParams(
                    vertexStart,
                    new VertexAttributeDescriptor(VertexAttribute.Position, stream: 0),
                    new VertexAttributeDescriptor(VertexAttribute.Normal, stream: 1),
                    new VertexAttributeDescriptor(VertexAttribute.TexCoord0, stream: 2),
                    new VertexAttributeDescriptor(VertexAttribute.TexCoord1, stream: 3));

                /* 在正確的時間調用 Schedule 和 Complete
                 * 一旦你擁有了一個 job 所需的數據，盡可能快地在 job 上調用 Schedule，在你需要它的執行結果之前不要調用 Complete。
                 * 一個良好的實踐是調度一個你不需要等待的 job，同時它不會與當前正在運行的其他job產生競爭。
                 * 舉例來說，如果你在一幀結束和下一幀開始之前擁有一段沒有其他 job 在運行的時間，並且可以接受一幀的延遲，你可以在一幀結束的時候調度一個 job，在下一幀中使用它的結果。
                 * 或者，如果這個轉換時間已經被其他 job 占滿了，但是在一幀中有一大段未充分利用的時段，在這里調度你的 job 會更有效率。
                 * 
                 * job 擁有一個 Run 方法，你可以用它來替代 Schedule 從而讓主線程立刻執行這個 job。你可以使用它來達到調試目的。
                 */
                var handle = jobs.Schedule(inputMeshes.Count, 4);
                var newMesh = new Mesh();
                newMesh.name = $"Chunk_{location.x}_{location.y}_{location.z}";

                var sm = new SubMeshDescriptor(0, triStart, MeshTopology.Triangles);
                sm.firstVertex = 0;
                sm.vertexCount = vertexStart;

                /* 調用 JobHandle.Complete 來重新獲得歸屬權
                 * 在主線程重新使用數據前，追蹤數據的所有權需要依賴項都完成。只檢查 JobHandle.IsCompleted 是不夠的。
                 * 你必須調用 JobHandle.Complete 來在主線程中重新獲取 NaitveContainer 類型的所有權。調用 Complete 同時會清理安全性系統中的狀態。
                 * 不這樣做的話會造成內存泄漏。這個過程也在你每一幀都調度依賴於上一幀 job 的新 job 時被采用。
                 * 
                 * 在主線程中調用 Schedule 和 Complete
                 * 你只能在主線程中調用 Schedule 和 Complete 方法。如果一個 job 需要依賴於另一個，使用 JobHandle 來處理依賴關系而不是嘗試在 job 中調度新的 job。
                 * 
                 * 
                 */
                handle.Complete();

                jobs.outputMesh.subMeshCount = 1;
                jobs.outputMesh.SetSubMesh(0, sm);

                // 通過 Mesh.ApplyAndDisposeWritableMeshData 接口賦值回 Mesh
                // inputMeshes -> jobs.meshData -> jobs.outputMesh -> outputMeshData -> newMesh
                Mesh.ApplyAndDisposeWritableMeshData(outputMeshData, new[] { newMesh });

                jobs.meshData.Dispose();
                jobs.vertexStart.Dispose();
                jobs.triStart.Dispose();
                newMesh.RecalculateBounds();

                // (pass: 0)載入固體方塊 Mesh
                if (pass == 0)
                {
                    mesh_filter_solid.mesh = newMesh;
                    MeshCollider collider = mesh_obj_solid.AddComponent<MeshCollider>();
                    collider.sharedMesh = mesh_filter_solid.mesh;
                }

                // (pass: 1)載入流體方塊 Mesh
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

                        // 這個版本樹木若剛好在 Chunk 的邊界上，則會被切掉。我的優化版已解決。
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