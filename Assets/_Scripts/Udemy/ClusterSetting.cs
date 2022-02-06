using UnityEngine;

namespace udemy
{
    [CreateAssetMenu(fileName = "New ClusterSetting", menuName = "Cluster", order = 0)]
    public class ClusterSetting : ScriptableObject
    {
        [Header("縮放波型的震幅")]
        public float height_scale = 2;

        [Header("縮放取樣點的座標")]
        [Range(0.0f, 1.0f)]
        public float scale = 0.5f;

        [Header("疊加 PerlinNoise 層數")]
        [Min(1)]
        public int octaves = 1;

        [Header("目標海拔高度")]
        public float altitude = 1f;

        [Header("邊界定義數值")]
        public float boundary = 1f;

        public float fBM3D(float x, float y, float z)
        {
            return Cluster.fBM3D(x, y, z, octaves, scale, height_scale, height_offset: altitude);
        }
    }
}
