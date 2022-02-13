using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace udemy
{
    public class BlockDemo : MonoBehaviour
    {
        public Material atlas;

        // Start is called before the first frame update
        void Start()
        {
            MeshFilter filter = gameObject.AddComponent<MeshFilter>();
            MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.material = atlas;

            List<Quad1> quads = new List<Quad1>();
            Vector3 offset = new Vector3(0, 0, 0);
            quads.Add(new Quad1(BlockType.DIRT, BlockSide.Bottom, offset));
            quads.Add(new Quad1(BlockType.GRASSTOP, BlockSide.Top, offset));
            quads.Add(new Quad1(BlockType.GRASSSIDE, BlockSide.Left, offset));
            quads.Add(new Quad1(BlockType.GRASSSIDE, BlockSide.Right, offset));
            quads.Add(new Quad1(BlockType.GRASSSIDE, BlockSide.Front, offset));
            quads.Add(new Quad1(BlockType.GRASSSIDE, BlockSide.Back, offset));

            List<Mesh> meshes = new List<Mesh>();

            foreach(Quad1 quad in quads)
            {
                meshes.Add(quad.mesh);
            }

            Mesh mesh = MeshUtils.mergeMeshes(meshes);
            mesh.name = "Cube_0_0_0";
            filter.mesh = mesh;
        }
    }
}
