using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace udemy
{
    public class QuadDemo : MonoBehaviour
    {
        // Start is called before the first frame update
        void Start()
        {
            MeshFilter filter = gameObject.AddComponent<MeshFilter>();
            Mesh mesh = new Mesh();
            mesh.name = "ScriptedMesh";

            Vector2 uv00 = new Vector2(0, 0);
            Vector2 uv10 = new Vector2(1, 0);
            Vector2 uv01 = new Vector2(0, 1);
            Vector2 uv11 = new Vector2(1, 1);

            // 數值參考 Cube
            Vector3 p0 = new Vector3(-0.5f, -0.5f,  0.5f);
            Vector3 p1 = new Vector3( 0.5f, -0.5f,  0.5f);
            Vector3 p2 = new Vector3( 0.5f, -0.5f, -0.5f);
            Vector3 p3 = new Vector3(-0.5f, -0.5f, -0.5f);
            Vector3 p4 = new Vector3(-0.5f,  0.5f,  0.5f);
            Vector3 p5 = new Vector3( 0.5f,  0.5f,  0.5f);
            Vector3 p6 = new Vector3( 0.5f,  0.5f, -0.5f);
            Vector3 p7 = new Vector3(-0.5f,  0.5f, -0.5f);

            Vector3[] vertices = new Vector3[] { p4, p5, p1, p0};
            Vector3[] normals = new Vector3[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
            Vector2[] uvs = new Vector2[] { uv11, uv01, uv00, uv10};

            // 前 3 定義第一個三角形，後 3 定義第二個三角形，每個三角形的頂點順序應為順時鐘
            int[] triangles = new int[] { 3, 1, 0, 3, 2, 1 };

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            filter.mesh = mesh;
        }
    }
}
