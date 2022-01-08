using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// vertex, normal, uv
using VertexData = System.Tuple<UnityEngine.Vector3, UnityEngine.Vector3, UnityEngine.Vector2>;

public static class MeshUtils
{

    public enum BlockSide { BOTTOM, TOP, LEFT, RIGHT, FRONT, BACK }

    public enum BlockType
    {
        GRASSTOP, GRASSSIDE, DIRT, WATER, STONE, SAND, GOLD, BEDROCK, REDSTONE, DIAMOND, NOCRACK,
        CRACK1, CRACK2, CRACK3, CRACK4, AIR
    };

    // 此種取得 UV 邊界座標的方式，與 enum BlockType 的順序有關聯，是不好的方法
    public static Vector2[,] blockUVs =
    {
        // GRASSTOP
        {
            new Vector2(0.1250f, 0.3750f),
            new Vector2(0.1875f, 0.3750f),
            new Vector2(0.1250f, 0.4375f),
            new Vector2(0.1875f, 0.4375f)
        },
        // GRASSSIDE
        {
            new Vector2(0.1875f, 0.9375f),
            new Vector2(0.2500f, 0.9375f),
            new Vector2(0.1875f, 1.0000f),
            new Vector2(0.2500f, 1.0000f)
        },
        // DIRT
        {
            new Vector2(0.1250f, 0.9375f),
            new Vector2(0.1875f, 0.9375f),
            new Vector2(0.1250f, 1.0000f),
            new Vector2(0.1875f, 1.0000f)
        },
        // WATER
        {
            new Vector2(0.8750f, 0.1250f),
            new Vector2(0.9375f, 0.1250f),
            new Vector2(0.8750f, 0.1875f),
            new Vector2(0.9375f, 0.1875f)
        },
        // STONE
        {
            new Vector2(0.0000f, 0.8750f),
            new Vector2(0.0625f, 0.8750f),
            new Vector2(0.0000f, 0.9375f),
            new Vector2(0.0625f, 0.9375f)
        },
        // SAND
        {
            new Vector2(0.1250f, 0.8750f),
            new Vector2(0.1875f, 0.8750f),
            new Vector2(0.1250f, 0.9375f),
            new Vector2(0.1875f, 0.9375f)
        },
        /*GOLD*/        
        { 
            new Vector2(0.0000f, 0.8125f),  
            new Vector2(0.0625f, 0.8125f),
            new Vector2(0.0000f, 0.8750f), 
            new Vector2(0.0625f, 0.8750f)
        },
        /*BEDROCK*/     
        {
            new Vector2(0.3125f, 0.8125f), 
            new Vector2(0.3750f, 0.8125f),
            new Vector2(0.3125f, 0.8750f),
            new Vector2(0.3750f, 0.8750f)
        },
        /*REDSTONE*/    
        {
            new Vector2(0.1875f, 0.7500f), 
            new Vector2(0.2500f, 0.7500f),
            new Vector2(0.1875f, 0.8125f),
            new Vector2(0.2500f, 0.8125f)
        },
        /*DIAMOND*/    
        {
            new Vector2( 0.125f, 0.75f ), 
            new Vector2( 0.1875f, 0.75f),
            new Vector2( 0.125f, 0.8125f ),
            new Vector2( 0.1875f, 0.8125f )
        },
        /*NOCRACK*/     
        {
            new Vector2( 0.6875f, 0f ), 
            new Vector2( 0.75f, 0f),         
            new Vector2( 0.6875f, 0.0625f ),
            new Vector2( 0.75f, 0.0625f )
        },
        /*CRACK1*/      
        { 
            new Vector2(0.0000f,0.0000f),  
            new Vector2(0.0625f,0.0000f),
            new Vector2(0.0000f,0.0625f), 
            new Vector2(0.0625f,0.0625f)
        },
        /*CRACK2*/      
        { 
            new Vector2(0.0625f,0.0000f),  
            new Vector2(0.125f,0.0000f),
            new Vector2(0.0625f,0.0625f), 
            new Vector2(0.125f,0.0625f)
        },
        /*CRACK3*/      
        { 
            new Vector2(0.125f,0.0000f),  
            new Vector2(0.1875f,0.0000f),
            new Vector2(0.125f,0.0625f), 
            new Vector2(0.1875f,0.0625f)
        },
        /*CRACK4*/      
        { 
            new Vector2(0.1875f,0.0000f),  
            new Vector2(0.25f,0.0000f),
            new Vector2(0.1875f,0.0625f), 
            new Vector2(0.25f,0.0625f)
        }
    };

    // TODO: Dict<BlockType, Tuple(x, y)> -> (x, y) 再利用 Vector2[] getBlockUVs(int x, int y) 取得 UV 邊界座標
    public static readonly Dictionary<BlockType, Tuple<int, int>> blockAnchor = new Dictionary<BlockType, Tuple<int, int>>()
    {
        {BlockType.GRASSTOP, new Tuple<int, int>(2, 15) },
        {BlockType.GRASSSIDE, new Tuple<int, int>(2, 15) },
        {BlockType.DIRT, new Tuple<int, int>(2, 15) },
        {BlockType.WATER, new Tuple<int, int>(2, 15) },
        {BlockType.STONE, new Tuple<int, int>(2, 15) },
        {BlockType.SAND, new Tuple<int, int>(2, 15) },
    };

    public static Dictionary<BlockType, Vector2[]> block2Coordinate = new Dictionary<BlockType, Vector2[]>();

    public static Vector2[] getBlockTypeCoordinate(BlockType block_type)
    {
        if (!blockAnchor.ContainsKey(block_type))
        {
            return null;
        }
        else if (!block2Coordinate.ContainsKey(block_type))
        {
            Tuple<int, int> anchor = blockAnchor[block_type];
            block2Coordinate[block_type] = getBlockUVs(x: anchor.Item1, y: anchor.Item2);
        }

        return block2Coordinate[block_type];
    }

    // 每個方塊尺寸為 0.0625 * 0.0625，根據 (x, y) 位置，返回邊界四點座標
    public static Vector2[] getBlockUVs(int x, int y)
    {
        float SIZE = 0.0625f;
        float left = SIZE * x, right = SIZE * (x + 1);
        float bottom = SIZE * y, top = SIZE * (y + 1);

        return new Vector2[] {
            new Vector2(left, bottom),
            new Vector2(right, bottom),
            new Vector2(left, top),
            new Vector2(right, top)
        };
    }

    public static Mesh MergeMeshes(Mesh[] meshes)
    {
        Mesh mesh = new Mesh();

        Dictionary<VertexData, int> pointsOrder = new Dictionary<VertexData, int>();
        HashSet<VertexData> pointsHash = new HashSet<VertexData>();
        List<int> tris = new List<int>();

        int pIndex = 0;

        // loop through each mesh
        for (int i = 0; i < meshes.Length; i++)
        {
            if (meshes[i] == null)
            {
                continue;
            }

            // loop through each vertex of the current mesh
            for (int j = 0; j < meshes[i].vertices.Length; j++)
            {
                Vector3 v = meshes[i].vertices[j];
                Vector3 n = meshes[i].normals[j];
                Vector2 u = meshes[i].uv[j];
                VertexData p = new VertexData(v, n, u);

                if (!pointsHash.Contains(p))
                {
                    pointsOrder.Add(p, pIndex);
                    pointsHash.Add(p);

                    pIndex++;
                }

            }

            for (int t = 0; t < meshes[i].triangles.Length; t++)
            {
                int triPoint = meshes[i].triangles[t];
                Vector3 v = meshes[i].vertices[triPoint];
                Vector3 n = meshes[i].normals[triPoint];
                Vector2 u = meshes[i].uv[triPoint];
                VertexData p = new VertexData(v, n, u);

                int index;
                pointsOrder.TryGetValue(p, out index);
                tris.Add(index);
            }

            meshes[i] = null;
        }

        ExtractArrays(pointsOrder, mesh);
        mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds();

        return mesh;
    }

    public static void ExtractArrays(Dictionary<VertexData, int> list, Mesh mesh)
    {
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();

        foreach (VertexData v in list.Keys)
        {
            verts.Add(v.Item1);
            norms.Add(v.Item2);
            uvs.Add(v.Item3);
        }

        mesh.vertices = verts.ToArray();
        mesh.normals = norms.ToArray();
        mesh.uv = uvs.ToArray();
    }

    // TODO: 可以考慮直接返回 int
    public static float fBM(float x, float z, int octaves, float scale, float heightScale, float heightOffset)
    {
        float total = 0f;
        float frequncy = 1f;

        for (int i = 0; i < octaves; i++)
        {
            total += Mathf.PerlinNoise(x * scale * frequncy, z * scale * frequncy) * heightScale;
            frequncy *= 2f;
        }

        return total + heightOffset;
    }

    public static float fBM3D(float x, float y, float z, int octaves, float scale, float heightScale, float heightOffset)
    {
        float XY = fBM(x, y, octaves, scale, heightScale, heightOffset);
        float YZ = fBM(y, z, octaves, scale, heightScale, heightOffset);
        float XZ = fBM(x, z, octaves, scale, heightScale, heightOffset);
        float YX = fBM(y, x, octaves, scale, heightScale, heightOffset);
        float ZY = fBM(z, y, octaves, scale, heightScale, heightOffset);
        float ZX = fBM(z, x, octaves, scale, heightScale, heightOffset);

        return (XY + YZ + XZ + YX + ZY + ZX) / 6.0f;
    }

}
