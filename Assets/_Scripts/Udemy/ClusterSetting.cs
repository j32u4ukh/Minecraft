using UnityEngine;

namespace udemy
{
    [CreateAssetMenu(fileName = "New ClusterSetting", menuName = "Cluster", order = 0)]
    public class ClusterSetting : ScriptableObject
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

        [Header("��ɩw�q�ƭ�")]
        public float boundary = 1f;

        public float fBM3D(float x, float y, float z)
        {
            return Cluster.fBM3D(x, y, z, octaves, scale, height_scale, height_offset: altitude);
        }
    }
}
