using UnityEngine;

namespace udemy
{
    public struct Strata
    {
        public float height_scale;
        public float scale;
        public int octaves;
        public float height_offset;

        // �Ω�a�h���Y���q�����X�{���v�A�P PerlinNoise �� fBM �����L��
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
        /// 
        /// </summary>
        /// <param name="x">X �y��</param>
        /// <param name="z">Z �y��</param>
        /// <param name="altitude">�ؼЮ���</param>
        /// <param name="octaves">�|�[ PerlinNoise ���ռơA�|���ܪi��</param>
        /// <param name="scale">�Y�� x, z ���Ƚd��A�|���ܪi��</param>
        /// <param name="height_scale">�Y�� PerlinNoise �p��ȡA�|���ܪi��</param>
        /// <param name="offset">�b�o�հѼƤU�� fBM ���˥�����</param>
        /// <returns>�b�ؼЮ��ޤW�U�i�ʪ��ƭȡA�i���P fBM �ۦP</returns>
        public static float getAltitude(float x, float z, float altitude, int octaves, float scale, float height_scale, float offset = 0f)
        {
            float height = fBM(x, z, octaves, scale, height_scale);

            // �������ޥ����ȡA�i�ϼƭȦb�Ӯ��ޤW�U�i��            
            return altitude + height - offset;
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
