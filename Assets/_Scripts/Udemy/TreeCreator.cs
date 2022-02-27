using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace udemy
{
    [ExecuteInEditMode]
    public class TreeCreator : MonoBehaviour
    {
        public Vector3Int dimensions = new Vector3Int(3, 6, 3);
        public GameObject[,,] cubes;
        public string block_detail = "";
        int half_x;
        int half_z;

        void OnValidate()
        {
            Draw();
        }

        void Draw()
        {
            MeshRenderer[] cubes = GetComponentsInChildren<MeshRenderer>();

            if (cubes.Length == 0)
            {
                CreateCubes();
            }

            if (cubes.Length == 0)
            {
                return;
            }
        }

        // 建立初始方塊
        void CreateCubes()
        {
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    for (int x = 0; x < dimensions.x; x++)
                    {
                        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        cube.name = $"{x}|{y}|{z}";
                        cube.transform.parent = transform;
                        cube.transform.position = new Vector3(x, y, z);
                    }
                }
            }
        }

        public void getDetails()
        {
            getCubes();

            block_detail = "";
            half_x = dimensions.x / 2;
            half_z = dimensions.z / 2;

            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    for (int x = 0; x < dimensions.x; x++)
                    {
                        //(new Vector3Int(0, 1, 0), MeshUtils.BlockType.WOOD)
                        if (cubes[x, y, z] == null)
                        {
                            continue;
                        }

                        Debug.Log(cubes[x, y, z].GetComponent<Renderer>().sharedMaterial);

                        if (cubes[x, y, z].GetComponent<Renderer>().sharedMaterial.ToString().Contains("trunk"))
                        {
                            //block_detail += "(new Vector3Int(" + (x - half_x) + "," + y + "," + (z - half_z) + "), MeshUtils.BlockType.WOOD),\n";
                            block_detail += $"(new Vector3Int({x - half_x},{y},{z - half_z}), BlockType.WOOD),\n";

                        }
                        else
                        {
                            block_detail += $"(new Vector3Int({x - half_x},{y},{z - half_z}), BlockType.LEAVES),\n";
                        }

                    }
                }
            }
        }

        public void reAlignBlocks()
        {
            getCubes();

            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    for (int x = 0; x < dimensions.x; x++)
                    {
                        if (cubes[x, y, z] != null)
                        {
                            cubes[x, y, z].transform.position = new Vector3(x, y, z);
                        }
                    }
                }
            }
        }

        void getCubes()
        {
            cubes = new GameObject[dimensions.x, dimensions.y, dimensions.z];

            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    for (int x = 0; x < dimensions.x; x++)
                    {
                        cubes[x, y, z] = GameObject.Find($"{x}|{y}|{z}");
                    }
                }
            }
        }
    }

}