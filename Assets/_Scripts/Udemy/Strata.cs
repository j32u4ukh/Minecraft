using UnityEngine;

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

        //public float fBM(float x, float z)
        //{
        //    return fBM(x, z, octaves, scale, height_scale, height_offset);
        //}

        /// <summary>
        /// 疊加多組 PerlinNoise，並利用其他參數，在相同位置 (x, z) 上獲取不同的 PerlinNoise
        /// reference: https://thebookofshaders.com/13/?lan=ch
        /// 若不修改預設值，則與原始 PerlinNoise 相同
        /// </summary>
        /// <param name="x">X 座標</param>
        /// <param name="z">Z 座標</param>
        /// <param name="octaves">疊加 PerlinNoise 的組數，會改變波型</param>
        /// <param name="scale">縮放 x, z 取值範圍，會改變波型</param>
        /// <param name="height_scale">縮放 PerlinNoise 計算值，會改變波型</param>
        /// <param name="height_offset">疊加完多組 PerlinNoise 後，最後在加上的高度偏移量，不會改變波型</param>
        /// <returns> 分形布朗運動（Fractal Brownian Motion） </returns>
        public static float fBM(float x, float z, int octaves = 1, float scale = 1f, float height_scale = 1f, float height_offset = 0f)
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
        /// <param name="offset">在這組參數下的 fBM 的樣本平均</param>
        /// <returns>在目標海拔上下波動的數值，波型與 fBM 相同</returns>
        public static float getAltitude(float x, float z, float altitude, int octaves, float scale, float height_scale, float offset = 0f)
        {
            float height = fBM(x, z, octaves, scale, height_scale);

            // 扣除海拔平均值，可使數值在該海拔上下波動            
            return altitude + height - offset;
        }

        /// <summary>
        /// 取得當前參數下的 PerlinNoise 數值平均數
        /// </summary>
        /// <param name="min_x">X 取樣最小值</param>
        /// <param name="max_x">X 取樣最大值</param>
        /// <param name="min_y">Y 取樣最小值</param>
        /// <param name="max_y">Y 取樣最大值</param>
        /// <param name="scale">PerlinNoise 縮放比例，fBM 中的 octaves * height_scale</param>
        /// <param name="n_sample">取樣點數</param>
        /// <returns></returns>
        public static float getPerlinMean(float min_x = -100f, float max_x = 100f, float min_y = -100f, float max_y = 100f, float scale = 1f, int n_sample = 10)
        {
            float total = 0f, x, y;
            int i, j;

            for (i = 0; i < n_sample; i++)
            {
                for (j = 0; j < n_sample; j++)
                {
                    x = Mathf.Lerp(min_x, max_x, (float)i / n_sample);
                    y = Mathf.Lerp(min_y, max_y, (float)j / n_sample);
                    total += (Mathf.PerlinNoise(x, y) * scale);
                }
            }

            return total / (n_sample * n_sample);
        }
    }
}
