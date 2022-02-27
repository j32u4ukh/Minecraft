using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace minecraft
{
    public class DisplayMeshInfo : MonoBehaviour
    {
        Dictionary<Vector3, int> vertices = new Dictionary<Vector3, int>();
        List<int> indexs = new List<int>();

        // Start is called before the first frame update
        void Start()
        {
            Mesh mesh = GetComponent<MeshFilter>().mesh;
            
            
            Vector3 vertex;
            int i, v, len = mesh.vertices.Length;

            for(i = 0; i < len; i++)
            {
                vertex = mesh.vertices[i];

                if (vertices.ContainsKey(vertex))
                {
                    v = vertices[vertex];
                }
                else
                {
                    v = vertices.Count;
                    vertices.Add(vertex, v);
                }

                print($"{i}, Vertex({v}) {vertex}, Normal {mesh.normals[i]}, UV {mesh.uv[i]}");
                indexs.Add(v);
            }

            print("Triangles\n");
            len = mesh.triangles.Length;

            for (i = 0; i < len; i += 3)
            {
                print($"Triangle({indexs[mesh.triangles[i]]}, " +
                      $"{indexs[mesh.triangles[i + 1]]}, " +
                      $"{indexs[mesh.triangles[i + 2]]})");
            }
        }


    }
}
