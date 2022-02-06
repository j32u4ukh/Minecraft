using UnityEngine;

namespace udemy
{
    [CreateAssetMenu(fileName = "New StrataSetting", menuName = "Strata", order = 0)]
    public class StrataSetting : ScriptableObject
    {
        [Header("�Y��i�����_�T")]
        public float height_scale = 2;

        [Header("�Y������I���y��")]
        [Range(0.0f, 1.0f)]
        public float scale = 0.5f;

        [Header("�|�[ PerlinNoise �h��")]
        [Min(1)]
        public int octaves = 1;

        [Header("�ؼЮ��ް���")]
        public float altitude = 1f;

        [Header("���~/�q���X�{���v")]
        [Range(0.0f, 1.0f)]
        public float probability = 1f;

        public float getOffset(float min_x = -100f, float max_x = 100f, float min_y = -100f, float max_y = 100f, int n_sample = 10)
        {
            return Strata.getPerlinMean(min_x: min_x, max_x: max_x, min_y: min_y, max_y: max_y, scale: octaves * height_scale, n_sample: n_sample);
        }

        public float getAltitude(float x, float z, float offset = 0f)
        {
            return Strata.getAltitude(x, z, altitude, octaves, scale, height_scale, offset);
        }
    }
}
