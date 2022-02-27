using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEditor;

namespace minecraft
{
    public class Perlin3DGrapher : MonoBehaviour
    {
        public ClusterSetting setting;
        Vector3 dimensions = new Vector3(10, 10, 10);

        private void Awake()
        {

        }

        private void OnDrawGizmosSelected()
        {
#if UNITY_EDITOR
            // EditorUtility 不存在任何平台中，只能用於 UNITY_EDITOR，利用 #if UNITY_EDITOR 或是放在 Editor 資料夾當中才不會出現編譯錯誤
            if (EditorUtility.IsDirty(setting.GetInstanceID()))
            {
                Debug.Log("ClusterSetting is dirty");
                graphPerlin3D();
            }
#endif
        }

        void OnValidate()
        {
            graphPerlin3D();
        }

        void graphPerlin3D()
        {
            // destroy existing cubes
            MeshRenderer[] cubes = GetComponentsInChildren<MeshRenderer>();

            if (cubes.Length == 0)
            {
                createCubes();
            }

            if (cubes.Length == 0)
            {
                return;
            }

            int n_enable = 0, idx;
            float p3d;

            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    for (int x = 0; x < dimensions.x; x++)
                    {
                        p3d = setting.fBM3D(x, y, z);
                        idx = Utils.xyzToFlat(x, y, z, (int)dimensions.x, (int)dimensions.z);

                        if(p3d >= setting.boundary)
                        {
                            cubes[idx].enabled = true;
                            n_enable++;
                        }
                        else
                        {
                            cubes[idx].enabled = false;
                        }
                    }
                }
            }

            //print($"n_enable: {n_enable}");
        }

        void createCubes()
        {
            GameObject cube;

            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    for (int x = 0; x < dimensions.x; x++)
                    {
                        cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        cube.name = "perlin_cube";
                        cube.transform.parent = transform;
                        cube.transform.position = new Vector3(x, y, z);
                    }
                }
            }
        }
    }

}