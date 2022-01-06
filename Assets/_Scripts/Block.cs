using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Block : MonoBehaviour
{
    [System.Serializable] 
    public enum BlockSide{ BOTTOM, TOP, LEFT, RIGHT, FRONT, BACK}

    public Material atlas;

    // Start is called before the first frame update
    void Start()
    {
        MeshFilter mf = gameObject.AddComponent<MeshFilter>();
        MeshRenderer mr = gameObject.AddComponent<MeshRenderer>();
        mr.material = atlas;

        Quad[] quads = new Quad[6];
        quads[0] = new Quad(BlockSide.TOP, new Vector3(0f, 0f, 0f), MeshUtils.BlockType.GRASSTOP);
        quads[1] = new Quad(BlockSide.FRONT, new Vector3(0f, 0f, 0f), MeshUtils.BlockType.GRASSSIDE);
        quads[2] = new Quad(BlockSide.BACK, new Vector3(0f, 0f, 0f), MeshUtils.BlockType.GRASSSIDE);
        quads[3] = new Quad(BlockSide.LEFT, new Vector3(0f, 0f, 0f), MeshUtils.BlockType.GRASSSIDE);
        quads[4] = new Quad(BlockSide.RIGHT, new Vector3(0f, 0f, 0f), MeshUtils.BlockType.GRASSSIDE);
        quads[5] = new Quad(BlockSide.BOTTOM, new Vector3(0f, 0f, 0f), MeshUtils.BlockType.DIRT);
        //mf.mesh = quad.Build(side, new Vector3(1f, 1f, 1f));

        Mesh[] sideMeshes = new Mesh[6];
        sideMeshes[0] = quads[0].mesh;
        sideMeshes[1] = quads[1].mesh;
        sideMeshes[2] = quads[2].mesh;
        sideMeshes[3] = quads[3].mesh;
        sideMeshes[4] = quads[4].mesh;
        sideMeshes[5] = quads[5].mesh;

        mf.mesh = MeshUtils.MergeMeshes(sideMeshes);
        mf.mesh.name = "Cube_0_0_0";
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
