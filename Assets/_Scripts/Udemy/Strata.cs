using UnityEngine;

namespace udemy
{
    public struct Strata
    {
        // �|�[ PerlinNoise �h��
        public int octaves;

        // �Y������I���y��
        public float scale;

        // �Y��i�����_�T
        public float height_scale;

        // ���װ����q
        public float height_offset;

        // �Ω�a�h���Y���q�����X�{���v�A�P PerlinNoise �� fBM �����L��
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
        /// �|�[�h�� PerlinNoise�A�çQ�Ψ�L�ѼơA�b�ۦP��m (x, z) �W������P�� PerlinNoise
        /// reference: https://thebookofshaders.com/13/?lan=ch
        /// �Y���ק�w�]�ȡA�h�P��l PerlinNoise �ۦP
        /// </summary>
        /// <param name="x">X �y��</param>
        /// <param name="z">Z �y��</param>
        /// <param name="octaves">�|�[ PerlinNoise ���ռơA�|���ܪi��</param>
        /// <param name="scale">�Y�� x, z ���Ƚd��A�|���ܪi��</param>
        /// <param name="height_scale">�Y�� PerlinNoise �p��ȡA�|���ܪi��</param>
        /// <param name="height_offset">�|�[���h�� PerlinNoise ��A�̫�b�[�W�����װ����q�A���|���ܪi��</param>
        /// <returns> ���Υ��ԹB�ʡ]Fractal Brownian Motion�^ </returns>
        public static float fBM(float x, float z, int octaves = 1, float scale = 1f, float height_scale = 1f, float height_offset = 0f)
        {
            float total = 0f;
            float frequncy = 1f;

            // �����A�@�ӤK�ס]octave�^�������W�v�W���[���δ�b�C
            for (int i = 0; i < octaves; i++)
            {
                total += Mathf.PerlinNoise(x * scale * frequncy, z * scale * frequncy) * height_scale;
                frequncy *= 2f;
            }

            return total + height_offset;
        }

        /// <summary>
        /// ���o��e�ѼƤU�� PerlinNoise �ƭȥ�����
        /// </summary>
        /// <param name="min_x">X ���˳̤p��</param>
        /// <param name="max_x">X ���˳̤j��</param>
        /// <param name="min_y">Y ���˳̤p��</param>
        /// <param name="max_y">Y ���˳̤j��</param>
        /// <param name="scale">PerlinNoise �Y���ҡAfBM ���� octaves * height_scale</param>
        /// <param name="n_sample">�����I��</param>
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
