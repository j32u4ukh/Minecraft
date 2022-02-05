using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace udemy
{
    public class Block
    {
        public Mesh mesh;

        public Block(BlockType type0, Vector3 offset)
        {
            List<Quad> quads = new List<Quad>();
            quads.Add(new Quad(type0, BlockSide.Bottom, offset));
            quads.Add(new Quad(type0, BlockSide.Top, offset));
            quads.Add(new Quad(type0, BlockSide.Left, offset));
            quads.Add(new Quad(type0, BlockSide.Right, offset));
            quads.Add(new Quad(type0, BlockSide.Front, offset));
            quads.Add(new Quad(type0, BlockSide.Back, offset));

            List<Mesh> meshes = new List<Mesh>();

            foreach (Quad quad in quads)
            {
                meshes.Add(quad.mesh);
            }

            mesh = MeshUtils.mergeMeshes(meshes);
            mesh.name = "Cube_0_0_0";
        }
    }
}
