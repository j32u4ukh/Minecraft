using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace udemy
{
    public struct Cluster
    {
        public float height_scale;
        public float scale;
        public int octaves;
        public float height_offset;

        // 邊界定義數值
        public float boundary;

        public Cluster(float height_scale, float scale, int octaves, float height_offset, float boundary)
        {
            this.height_scale = height_scale;
            this.scale = scale;
            this.octaves = octaves;
            this.height_offset = height_offset;
            this.boundary = boundary;
        }

        /// <summary>
        /// 3D 版分形布朗運動（Fractal Brownian Motion），若不修改預設值，則為 3D 版 PerlinNoise。
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        /// <param name="octaves"></param>
        /// <param name="scale"></param>
        /// <param name="height_scale"></param>
        /// <param name="height_offset"></param>
        /// <returns></returns>
        public static float fBM3D(float x, float y, float z, int octaves = 1, float scale = 1f, float height_scale = 1f, float height_offset = 0f)
        {
            float xy = Strata.fBM(x, y, octaves, scale, height_scale, height_offset);
            float yz = Strata.fBM(y, z, octaves, scale, height_scale, height_offset);
            float xz = Strata.fBM(x, z, octaves, scale, height_scale, height_offset);
            float yx = Strata.fBM(y, x, octaves, scale, height_scale, height_offset);
            float zy = Strata.fBM(z, y, octaves, scale, height_scale, height_offset);
            float zx = Strata.fBM(z, x, octaves, scale, height_scale, height_offset);

            return (xy + yz + xz + yx + zy + zx) / 6.0f;
        }
    }
}
