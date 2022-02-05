using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace udemy
{
    public static class Utils
    {
        /// <summary>
        /// x = i % WIDTH 
        /// y = (i / WIDTH) % HEIGHT
        /// z = i / (WIDTH * HEIGHT)
        /// </summary>
        /// <param name="i"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public static Vector3Int flatToVector3Int(int i, int width, int height)
        {
            return new Vector3Int(i % width, (i / width) % height, i / (width * height));
        }

        /// <summary>
        /// Flat[x + WIDTH * (y + DEPTH * z)] = Original[x, y, z]
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        /// <param name="width"></param>
        /// <param name="depth"></param>
        /// <returns></returns>
        public static int xyzToFlat(int x, int y, int z, int width, int depth)
        {
            return x + width * (y + depth * z);
        }
        
        // Flat[x + WIDTH * (y + DEPTH * z)] = Original[x, y, z]
        public static int vector3IntToFlat(Vector3Int v, int width, int depth)
        {
            return v.x + width * (v.y + depth * v.z);
        }
    }
}
