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

            List<Quad> quads = new List<Quad>();
            Vector3 offset = new Vector3(0, 0, 0);
            quads.Add(new Quad(BlockType.DIRT, BlockSide.Bottom, offset));
            quads.Add(new Quad(BlockType.GRASSTOP, BlockSide.Top, offset));
            quads.Add(new Quad(BlockType.GRASSSIDE, BlockSide.Left, offset));
            quads.Add(new Quad(BlockType.GRASSSIDE, BlockSide.Right, offset));
            quads.Add(new Quad(BlockType.GRASSSIDE, BlockSide.Front, offset));
            quads.Add(new Quad(BlockType.GRASSSIDE, BlockSide.Back, offset));

            List<Mesh> meshes = new List<Mesh>();

            foreach(Quad quad in quads)
            {
                meshes.Add(quad.mesh);
            }

            Mesh mesh = MeshUtils.mergeMeshes(meshes);
            mesh.name = "Cube_0_0_0";
            filter.mesh = mesh;
        }
    }
}
