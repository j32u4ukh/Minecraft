using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace udemy
{
    public struct Strata
    {
        public float height_scale;
        public float scale;
        public int octaves;
        public float height_offset;

        // 用於地層中某些礦物的出現機率，與 PerlinNoise 或 fBM 本身無關
        public float probability;

        public Strata(float height_scale, float scale, int octaves, float height_offset, float probability)
        {
            this.height_scale = height_scale;
            this.scale = scale;
            this.octaves = octaves;
            this.height_offset = height_offset;
            this.probability = probability;
        }

        public float fBM(float x, float z)
        {
            return fBM(x, z, octaves, scale, height_scale, height_offset);
        }

        /// <summary>
        /// 疊加多組 PerlinNoise，並利用其他參數，在相同位置 (x, z) 上獲取不同的 PerlinNoise
        /// reference: https://thebookofshaders.com/13/?lan=ch
        /// </summary>
        /// <param name="x">X 座標</param>
        /// <param name="z">Z 座標</param>
        /// <param name="octaves">疊加 PerlinNoise 的組數，會改變波型</param>
        /// <param name="scale">縮放 x, z 取值範圍，會改變波型</param>
        /// <param name="height_scale">縮放 PerlinNoise 計算值，會改變波型</param>
        /// <param name="height_offset">疊加完多組 PerlinNoise 後，最後在加上的高度偏移量，不會改變波型</param>
        /// <returns> 分形布朗運動（Fractal Brownian Motion） </returns>
        public static float fBM(float x, float z, int octaves, float scale, float height_scale, float height_offset)
        {
            float total = 0f;
            float frequncy = 1f;

            // 音階，一個八度（octave）對應著頻率上的加倍或減半。
            for (int i = 0; i < octaves; i++)
            {
                total += Mathf.PerlinNoise(x * scale * frequncy, z * scale * frequncy) * height_scale;
                frequncy *= 2f;
            }

            return total + height_offset;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="x">X 座標</param>
        /// <param name="z">Z 座標</param>
        /// <param name="altitude">目標海拔</param>
        /// <param name="octaves">疊加 PerlinNoise 的組數，會改變波型</param>
        /// <param name="scale">縮放 x, z 取值範圍，會改變波型</param>
        /// <param name="height_scale">縮放 PerlinNoise 計算值，會改變波型</param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public static float getAltitude(float x, float z, float altitude, int octaves, float scale, float height_scale, float offset = 0f)
        {
            float height = 0f;
            float frequncy = 1f;

            // 音階，一個八度（octave）對應著頻率上的加倍或減半。
            for (int i = 0; i < octaves; i++)
            {
                height += Mathf.PerlinNoise(x * scale * frequncy, z * scale * frequncy) * height_scale;
                frequncy *= 2f;
            }

            // 扣除海拔平均值，可使數值在該海拔上下波動            
            return altitude + height - offset;
        }

        public static float getPerlinMean(float min_x, float max_x, float min_y, float max_y, float scale, int n_sample = 100)
        {
            float total = 0f, x, y;

            for(int i = 0; i < n_sample; i++)
            {
                x = Random.Range(min_x, max_x);
                y = Random.Range(min_y, max_y);
                total += (Mathf.PerlinNoise(x, y) * scale);
            }

            return total / n_sample;
        }
    }
}
