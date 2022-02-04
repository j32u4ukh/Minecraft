using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace udemy
{
    

    public class BlockDemo : MonoBehaviour
    {
        public BlockSide side;
        public Material atlas;

        // Start is called before the first frame update
        void Start()
        {
            MeshFilter filter = gameObject.AddComponent<MeshFilter>();
            MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.material = atlas;

            List<Quad> quads = new List<Quad>();
            quads.Add(new Quad(BlockSide.Bottom, new Vector3(0, 0, 0)));
            quads.Add(new Quad(BlockSide.Top, new Vector3(0, 0, 0)));
            quads.Add(new Quad(BlockSide.Left, new Vector3(0, 0, 0)));
            quads.Add(new Quad(BlockSide.Right, new Vector3(0, 0, 0)));
            quads.Add(new Quad(BlockSide.Front, new Vector3(0, 0, 0)));
            quads.Add(new Quad(BlockSide.Back, new Vector3(0, 0, 0)));

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
