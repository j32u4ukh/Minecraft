using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// vertex, normal, uv0, uv1
using VertexData = System.Tuple<UnityEngine.Vector3, UnityEngine.Vector3, UnityEngine.Vector2, UnityEngine.Vector2>;

namespace udemy
{
    public static class MeshUtils
    {
        // Dict<BlockType, Tuple(x, y)> -> (x, y) �A�Q�� Vector2[] getBlockUVs(int x, int y) ���o UV ��ɮy��
        private static readonly Dictionary<BlockType, Tuple<int, int>> block_anchor = new Dictionary<BlockType, Tuple<int, int>>()
        {
            {BlockType.GRASSTOP, new Tuple<int, int>(2, 6) },
            {BlockType.GRASSSIDE, new Tuple<int, int>(3, 15) },
            {BlockType.DIRT, new Tuple<int, int>(2, 15) },
            {BlockType.WATER, new Tuple<int, int>(2, 4) },
            {BlockType.STONE, new Tuple<int, int>(0, 15) },
            {BlockType.SAND, new Tuple<int, int>(2, 14) },
            {BlockType.LEAVES, new Tuple<int, int>(1, 6) },
            {BlockType.WOOD, new Tuple<int, int>(4, 14) },
            {BlockType.WOODBASE, new Tuple<int, int>(4, 14) },
            {BlockType.FOREST, new Tuple<int, int>(4, 13) },
            {BlockType.CACTUS, new Tuple<int, int>(2, 5) },
            {BlockType.GOLD, new Tuple<int, int>(0, 13) },
            {BlockType.BEDROCK, new Tuple<int, int>(5, 13) },
            {BlockType.REDSTONE, new Tuple<int, int>(3, 12) },
            {BlockType.DIAMOND, new Tuple<int, int>(2, 12) },
        };

        private static readonly Dictionary<CrackState, Tuple<int, int>> crack_anchor = new Dictionary<CrackState, Tuple<int, int>>()
        {
            {CrackState.None, new Tuple<int, int>(11, 0) },
            {CrackState.Crack1, new Tuple<int, int>(0, 0) },
            {CrackState.Crack2, new Tuple<int, int>(1, 0) },
            {CrackState.Crack3, new Tuple<int, int>(2, 0) },
            {CrackState.Crack4, new Tuple<int, int>(3, 0) },
        };

        // Coordinate of block which is queried 
        private static Dictionary<BlockType, Vector2[,]> block_to_coordinate = new Dictionary<BlockType, Vector2[,]>();

        // Coordinate of crack which is queried 
        private static Dictionary<CrackState, Vector2[,]> crack_to_coordinate = new Dictionary<CrackState, Vector2[,]>();

        // �w�q�U�ؤ���ݭn�V���X���~�|�Q����(-1 ��ܵL�k�Q�}�a)
        private static Dictionary<BlockType, int> block_strength = new Dictionary<BlockType, int>() {
            { BlockType.GRASSTOP, 2 },
            { BlockType.GRASSSIDE, 2 },
            { BlockType.DIRT, 1 },
            { BlockType.WATER, 1 },
            { BlockType.STONE, 4 },
            { BlockType.SAND, 3 },
            { BlockType.GOLD, 4 },
            { BlockType.BEDROCK, -1 },
            { BlockType.REDSTONE, 3 },
            { BlockType.DIAMOND, 4 },
        };

        private static HashSet<BlockType> drop_blocks = new HashSet<BlockType>() { BlockType.SAND, BlockType.WATER };

        private static HashSet<BlockType> spread_blocks = new HashSet<BlockType>() { BlockType.WATER };

        /// <summary>
        /// Merge multi meshes into one mesh.
        /// TODO: unit test
        /// </summary>
        /// <param name="meshes">mesh list which we want to merge into one mesh</param>
        /// <returns>A mesh conmbine all inputed meshes</returns>
        public static Mesh mergeMeshes(List<Mesh> meshes)
        {
            Mesh mesh = new Mesh();
            Dictionary<VertexData, int> datas = new Dictionary<VertexData, int>();

            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Vector2> uv2s = new List<Vector2>();
            List<int> triangles = new List<int>();

            int i, j, t, triangle, n_mesh = meshes.Count, n_triangle;
            VertexData data;
            Vector3 vertex, normal;
            Vector2 uv, uv2;

            // loop through each mesh
            for (i = 0; i < n_mesh; i++)
            {
                // �D�n�Φb Block ���A�� meshes ���ۦ�ǤJ�� List<Mesh>�A���Ӥ��|���ŭ�
                if (meshes[i] == null)
                {
                    Debug.LogError($"[MeshUtils] mergeMeshes | meshes[{i}] == null");
                    continue;
                }

                n_triangle = meshes[i].triangles.Length;

                // loop through each vertex of the current mesh
                for (j = 0; j < n_triangle; j++)
                {
                    // get triangle index of current mesh
                    t = meshes[i].triangles[j];
                    vertex = meshes[i].vertices[t];
                    normal = meshes[i].normals[t];
                    uv = meshes[i].uv[t];
                    uv2 = meshes[i].uv2[t];
                    data = new VertexData(vertex, normal, uv, uv2);

                    if (datas.ContainsKey(data))
                    {
                        // get triangle index of mesh which will be merged
                        // t(triangle index of current mesh) may be 1, but triangle is 5
                        // because there has been 5 vertex from previous mesh
                        triangle = datas[data];
                    }
                    else
                    {
                        triangle = datas.Count;

                        // Non-duplicate values
                        vertices.Add(vertex);
                        normals.Add(normal);
                        uvs.Add(uv);
                        uv2s.Add(uv2);

                        datas.Add(data, triangle);
                    }

                    triangles.Add(triangle);
                }
            }

            mesh.vertices = vertices.ToArray();
            mesh.normals = normals.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.uv2 = uv2s.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// According to the block type to return the coordinate of uv texture
        /// if it has been queried, we can read the coordinate from block_to_coordinate.
        /// Or we can use the (x, y) from block_anchor, and use getBlockUVs to get the coordinate.
        /// We will save the coordinate into block_to_coordinate for next query.
        /// </summary>
        /// <param name="block_type">what kind of block</param>
        /// <returns></returns>
        public static Vector2[,] getBlockTypeCoordinate(BlockType block_type)
        {
            if (!block_anchor.ContainsKey(block_type))
            {
                // uv00, uv01, uv11, uv10
                return new Vector2[,] { { new Vector2(0, 0), new Vector2(0, 1) }, 
                                        { new Vector2(1, 0), new Vector2(1, 1) } };
            }
            else if (!block_to_coordinate.ContainsKey(block_type))
            {
                Tuple<int, int> anchor = block_anchor[block_type];
                block_to_coordinate[block_type] = getUVCoordinate(x: anchor.Item1, y: anchor.Item2);
            }

            return block_to_coordinate[block_type];
        }

        public static Vector2[,] getCrackStateCoordinate(CrackState crack_state)
        {
            if (!crack_anchor.ContainsKey(crack_state))
            {
                // uv00, uv01, uv11, uv10
                return new Vector2[,] { { new Vector2(0, 0), new Vector2(0, 1) },
                                        { new Vector2(1, 0), new Vector2(1, 1) } };
            }
            else if (!crack_to_coordinate.ContainsKey(crack_state))
            {
                Tuple<int, int> anchor = crack_anchor[crack_state];
                crack_to_coordinate[crack_state] = getUVCoordinate(x: anchor.Item1, y: anchor.Item2);
            }

            return crack_to_coordinate[crack_state];
        }

        /// <summary>
        /// �C�Ӥ���ؤo�� 0.0625 * 0.0625�A�ھ� (x, y) ��m�A��^��ɥ|�I�y��
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns>the coordinate of uv texture ((LeftBottom  00, LeftTop   01), 
        ///                                        (RightBottom 10, RightTop  11))</returns>
        private static Vector2[,] getUVCoordinate(int x, int y)
        {
            const float SIZE = 0.0625f;
            float left = SIZE * x, right = SIZE * (x + 1);
            float bottom = SIZE * y, top = SIZE * (y + 1);

            return new Vector2[,] {
                { new Vector2(left, bottom), new Vector2(left, top) },
                { new Vector2(right, bottom), new Vector2(right, top) }
            };
        }

        /// <summary>
        /// �U�ؤ���ݭn�V���X���~�|�Q����(-1 ��ܵL�k�Q�}�a)
        /// </summary>
        /// <param name="block_type"></param>
        /// <returns>�һݺV������</returns>
        public static int getStrenth(BlockType block_type)
        {
            return block_strength[block_type];
        }

        public static bool canDrop(BlockType block_type)
        {
            return drop_blocks.Contains(block_type);
        }

        public static bool canSpread(BlockType block_type)
        {
            return spread_blocks.Contains(block_type);
        }



    }
}
