using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace udemy
{
    public static class Utils
    {
        // x = i % WIDTH
        // y = (i / WIDTH) % HEIGHT
        // z = i / (WIDTH * HEIGHT)
        public static Vector3Int flatToVector3Int(int i, int width, int height)
        {
            return new Vector3Int(i % width, (i / width) % height, i / (width * height));
        }

        // Flat[x + WIDTH * (y + DEPTH * z)] = Original[x, y, z]
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
