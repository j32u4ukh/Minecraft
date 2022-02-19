using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace udemy
{
    public class Quad
    {
        public Mesh mesh;

        // 考慮法線方向時須注意 Unity 為左手座標系
        // ((LeftBottom 00, RightBottom 01), 
        //  (LeftTop    10, RightTop    11))
        // 頂點選取順序為 (00, 01, 11), (11, 10, 00)，根據左手座標系形成兩個三角形，組合成一個正方形
        public Quad(BlockType block_type, CrackState crack_state, BlockSide side, Vector3 offset)
        {
            mesh = new Mesh();
            mesh.name = "ScriptedQuad";

            // 數值參考 Cube
            Vector3[] vertices, normals;
            //Vector3 p0 = new Vector3(-0.5f, -0.5f,  0.5f) + offset;
            //Vector3 p1 = new Vector3( 0.5f, -0.5f,  0.5f) + offset;
            //Vector3 p2 = new Vector3( 0.5f, -0.5f, -0.5f) + offset;
            //Vector3 p3 = new Vector3(-0.5f, -0.5f, -0.5f) + offset;
            //Vector3 p4 = new Vector3(-0.5f,  0.5f,  0.5f) + offset;
            //Vector3 p5 = new Vector3( 0.5f,  0.5f,  0.5f) + offset;
            //Vector3 p6 = new Vector3( 0.5f,  0.5f, -0.5f) + offset;
            //Vector3 p7 = new Vector3(-0.5f,  0.5f, -0.5f) + offset;

            Vector3 p0 = new Vector3( 0.5f, -0.5f,  0.5f) + offset;
            Vector3 p1 = new Vector3( 0.5f,  0.5f,  0.5f) + offset;
            Vector3 p2 = new Vector3(-0.5f,  0.5f,  0.5f) + offset;
            Vector3 p3 = new Vector3(-0.5f, -0.5f,  0.5f) + offset;
            Vector3 p4 = new Vector3(-0.5f, -0.5f, -0.5f) + offset;
            Vector3 p5 = new Vector3(-0.5f,  0.5f, -0.5f) + offset;
            Vector3 p6 = new Vector3( 0.5f,  0.5f, -0.5f) + offset;
            Vector3 p7 = new Vector3( 0.5f, -0.5f, -0.5f) + offset;

            switch (side)
            {
                case BlockSide.Back:
                    //vertices = new Vector3[] { p6, p7, p3, p2 };
                    vertices = new Vector3[] { p4, p5, p6, p7 };
                    normals = new Vector3[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
                    break;
                case BlockSide.Top:
                    //vertices = new Vector3[] { p7, p6, p5, p4 };
                    vertices = new Vector3[] { p1, p6, p5, p2 };
                    normals = new Vector3[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
                    break;
                case BlockSide.Bottom:
                    //vertices = new Vector3[] { p0, p1, p2, p3 };
                    vertices = new Vector3[] { p7, p0, p3, p4 };
                    normals = new Vector3[] { Vector3.down, Vector3.down, Vector3.down, Vector3.down };
                    break;

                // 指向 Vector3.left 的那一面(X 逐漸變小那個方向)，而非面對正面時，在左邊的那面
                case BlockSide.Left:
                    //vertices = new Vector3[] { p7, p4, p0, p3 };
                    vertices = new Vector3[] { p3, p2, p5, p4 };
                    normals = new Vector3[] { Vector3.left, Vector3.left, Vector3.left, Vector3.left };
                    break;

                // 指向 Vector3.right 的那一面(X 逐漸變大那個方向)，而非面對正面時，在右邊的那面
                case BlockSide.Right:
                    //vertices = new Vector3[] { p5, p6, p2, p1 };
                    vertices = new Vector3[] { p7, p6, p1, p0 };
                    normals = new Vector3[] { Vector3.right, Vector3.right, Vector3.right, Vector3.right };
                    break;
                default:
                case BlockSide.Front:
                    //vertices = new Vector3[] { p4, p5, p1, p0 };
                    vertices = new Vector3[] { p0, p1, p2, p3 };
                    normals = new Vector3[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
                    break;
            }

            //Vector2 uv00 = new Vector2(0, 0);
            //Vector2 uv10 = new Vector2(1, 0);
            //Vector2 uv01 = new Vector2(0, 1);
            //Vector2 uv11 = new Vector2(1, 1);

            // uv0
            Vector2 uv000 = MeshUtils.getBlockTypeCoordinate(block_type)[0, 0];
            Vector2 uv001 = MeshUtils.getBlockTypeCoordinate(block_type)[0, 1];
            Vector2 uv010 = MeshUtils.getBlockTypeCoordinate(block_type)[1, 0];
            Vector2 uv011 = MeshUtils.getBlockTypeCoordinate(block_type)[1, 1];

            // uv1
            Vector2 uv100 = MeshUtils.getCrackStateCoordinate(crack_state)[0, 0];
            Vector2 uv101 = MeshUtils.getCrackStateCoordinate(crack_state)[0, 1];
            Vector2 uv110 = MeshUtils.getCrackStateCoordinate(crack_state)[1, 0];
            Vector2 uv111 = MeshUtils.getCrackStateCoordinate(crack_state)[1, 1];

            mesh.vertices = vertices;
            mesh.normals = normals;
            // (10, 01, 11), (10, 00, 01)
            //mesh.uv = new Vector2[] { uv11, uv01, uv00, uv10 };
            //mesh.uv2 = new Vector2[] { uv11, uv01, uv00, uv10 };
            // 依照順序: uv00, uv01, uv11, uv10
            mesh.uv = new Vector2[] { uv000, uv001, uv011, uv010 };
            mesh.uv2 = new Vector2[] { uv100, uv101, uv111, uv110 };

            // NOTE: 目前看起來，和 mesh.uv2 效果相同
            //List<Vector2> uv2 = new List<Vector2>() { uv100, uv101, uv111, uv110 };
            //mesh.SetUVs(1, uv2);

            // 前 3 定義第一個三角形，後 3 定義第二個三角形，每個三角形的頂點順序應為順時鐘
            //mesh.triangles = new int[] { 3, 1, 0, 3, 2, 1 };
            // LeftBottom, LeftTop, RightTop, RightBottom
            //mesh.triangles = new int[] { 0, 1, 2, 2, 3, 0 };
            mesh.triangles = new int[] { 0, 1, 2, 2, 3, 0 };
            
            mesh.RecalculateBounds();
        }
    }

    public class Quad1
    {
        public Mesh mesh;

        // 考慮法線方向時須注意 Unity 為左手座標系
        // ((LeftBottom 00, RightBottom 01), 
        //  (LeftTop    10, RightTop    11))
        // 頂點選取順序為 (00, 01, 11), (11, 10, 00)，根據左手座標系形成兩個三角形，組合成一個正方形
        public Quad1(BlockType block_type, BlockSide side, Vector3 offset)
        {
            mesh = new Mesh();
            mesh.name = "ScriptedMesh";

            // 數值參考 Cube
            Vector3[] vertices, normals;
            //Vector3 p0 = new Vector3(-0.5f, -0.5f,  0.5f) + offset;
            //Vector3 p1 = new Vector3( 0.5f, -0.5f,  0.5f) + offset;
            //Vector3 p2 = new Vector3( 0.5f, -0.5f, -0.5f) + offset;
            //Vector3 p3 = new Vector3(-0.5f, -0.5f, -0.5f) + offset;
            //Vector3 p4 = new Vector3(-0.5f,  0.5f,  0.5f) + offset;
            //Vector3 p5 = new Vector3( 0.5f,  0.5f,  0.5f) + offset;
            //Vector3 p6 = new Vector3( 0.5f,  0.5f, -0.5f) + offset;
            //Vector3 p7 = new Vector3(-0.5f,  0.5f, -0.5f) + offset;

            Vector3 p0 = new Vector3( 0.5f, -0.5f,  0.5f) + offset;
            Vector3 p1 = new Vector3( 0.5f,  0.5f,  0.5f) + offset;
            Vector3 p2 = new Vector3(-0.5f,  0.5f,  0.5f) + offset;
            Vector3 p3 = new Vector3(-0.5f, -0.5f,  0.5f) + offset;
            Vector3 p4 = new Vector3(-0.5f, -0.5f, -0.5f) + offset;
            Vector3 p5 = new Vector3(-0.5f,  0.5f, -0.5f) + offset;
            Vector3 p6 = new Vector3( 0.5f,  0.5f, -0.5f) + offset;
            Vector3 p7 = new Vector3( 0.5f, -0.5f, -0.5f) + offset;

            switch (side)
            {
                case BlockSide.Back:
                    //vertices = new Vector3[] { p6, p7, p3, p2 };
                    vertices = new Vector3[] { p4, p5, p6, p7 };
                    normals = new Vector3[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
                    break;
                case BlockSide.Top:
                    //vertices = new Vector3[] { p7, p6, p5, p4 };
                    vertices = new Vector3[] { p1, p6, p5, p2 };
                    normals = new Vector3[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
                    break;
                case BlockSide.Bottom:
                    //vertices = new Vector3[] { p0, p1, p2, p3 };
                    vertices = new Vector3[] { p7, p0, p3, p4 };
                    normals = new Vector3[] { Vector3.down, Vector3.down, Vector3.down, Vector3.down };
                    break;

                // 指向 Vector3.left 的那一面(X 逐漸變小那個方向)，而非面對正面時，在左邊的那面
                case BlockSide.Left:
                    //vertices = new Vector3[] { p7, p4, p0, p3 };
                    vertices = new Vector3[] { p3, p2, p5, p4 };
                    normals = new Vector3[] { Vector3.left, Vector3.left, Vector3.left, Vector3.left };
                    break;

                // 指向 Vector3.right 的那一面(X 逐漸變大那個方向)，而非面對正面時，在右邊的那面
                case BlockSide.Right:
                    //vertices = new Vector3[] { p5, p6, p2, p1 };
                    vertices = new Vector3[] { p7, p6, p1, p0 };
                    normals = new Vector3[] { Vector3.right, Vector3.right, Vector3.right, Vector3.right };
                    break;
                default:
                case BlockSide.Front:
                    //vertices = new Vector3[] { p4, p5, p1, p0 };
                    vertices = new Vector3[] { p0, p1, p2, p3 };
                    normals = new Vector3[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
                    break;
            }

            //Vector2 uv00 = new Vector2(0, 0);
            //Vector2 uv10 = new Vector2(1, 0);
            //Vector2 uv01 = new Vector2(0, 1);
            //Vector2 uv11 = new Vector2(1, 1);

            Vector2 uv00 = MeshUtils.getBlockTypeCoordinate(block_type)[0, 0];
            Vector2 uv01 = MeshUtils.getBlockTypeCoordinate(block_type)[0, 1];
            Vector2 uv10 = MeshUtils.getBlockTypeCoordinate(block_type)[1, 0];
            Vector2 uv11 = MeshUtils.getBlockTypeCoordinate(block_type)[1, 1];

            mesh.vertices = vertices;
            mesh.normals = normals;
            // (10, 01, 11), (10, 00, 01)
            //mesh.uv = new Vector2[] { uv11, uv01, uv00, uv10 };
            //mesh.uv2 = new Vector2[] { uv11, uv01, uv00, uv10 };
            // 依照順序: uv00, uv01, uv11, uv10
            mesh.uv = new Vector2[] { uv00, uv01, uv11, uv10 };
            mesh.uv2 = new Vector2[] { uv00, uv01, uv11, uv10 };

            // 前 3 定義第一個三角形，後 3 定義第二個三角形，每個三角形的頂點順序應為順時鐘
            //mesh.triangles = new int[] { 3, 1, 0, 3, 2, 1 };
            // LeftBottom, LeftTop, RightTop, RightBottom
            //mesh.triangles = new int[] { 0, 1, 2, 2, 3, 0 };
            mesh.triangles = new int[] { 0, 1, 2, 2, 3, 0 };
            
            mesh.RecalculateBounds();
        }
    }
}
