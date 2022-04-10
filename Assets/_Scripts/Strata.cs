using UnityEngine;

namespace udemy
{
    public struct Strata
    {
        // 疊加 PerlinNoise 層數
        public int octaves;

        // 縮放取樣點的座標
        public float scale;

        // 縮放波型的震幅
        public float height_scale;

        // 高度偏移量
        public float height_offset;

        // 用於地層中某些礦物的出現機率，與 PerlinNoise 或 fBM 本身無關
        public float probability;

        public Strata(StrataSetting setting, float min_x = -100f, float max_x = 100f, float min_y = -100f, float max_y = 100f, int n_sample = 10)
        {
            this = new Strata(octaves: setting.octaves, 
                              scale: setting.scale, 
                              height_scale: setting.height_scale, 
                              height_offset: setting.altitude - getPerlinMean(min_x: min_x, max_x: max_x, 
                                                                              min_y: min_y, max_y: max_y,
                                                                              scale: setting.octaves * setting.height_scale, 
                                                                              n_sample: n_sample), 
                              probability: setting.probability);
        }

        public Strata(int octaves = 1, float scale = 1f, float height_scale = 1f, float height_offset = 0f, float probability = 1f)
        {
            this.octaves = octaves;
            this.scale = scale;
            this.height_scale = height_scale;
            this.height_offset = height_offset;
            this.probability = probability;
        }

        public void setAltitude(float altitude, float min_x = -100f, float max_x = 100f, float min_y = -100f, float max_y = 100f, int n_sample = 10)
        {
            height_offset = altitude - getPerlinMean(min_x: min_x, max_x: max_x,
                                                     min_y: min_y, max_y: max_y,
                                                     scale: octaves * height_scale,
                                                     n_sample: n_sample);
        }

        public float fBM(float x, float z)
        {
            return fBM(x, z, octaves, scale, height_scale, height_offset);
        }

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
