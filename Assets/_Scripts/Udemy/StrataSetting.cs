using UnityEngine;

namespace udemy
{
    [CreateAssetMenu(fileName = "New StrataSetting", menuName = "Strata", order = 0)]
    public class StrataSetting : ScriptableObject
    {
        [Header("疊加 PerlinNoise 層數")]
        [Min(1)]
        public int octaves = 1;

        [Header("縮放取樣點的座標")]
        [Range(0.0f, 1.0f)]
        public float scale = 0.5f;

        [Header("縮放波型的震幅")]
        public float height_scale = 2;

        [Header("目標海拔高度")]
        public float altitude = 1f;

        [Header("物品/礦物出現機率")]
        [Range(0.0f, 1.0f)]
        public float probability = 1f;

        public float getOffset(float min_x = -100f, float max_x = 100f, float min_y = -100f, float max_y = 100f, int n_sample = 10)
        {
            return Strata.getPerlinMean(min_x: min_x, max_x: max_x, min_y: min_y, max_y: max_y, scale: octaves * height_scale, n_sample: n_sample);
        }

        public float getAltitude(float x, float z, float offset = 0f)
        {
            return Strata.fBM(x, z, octaves, scale, height_scale, height_offset: altitude - offset);
        }
    }
}
