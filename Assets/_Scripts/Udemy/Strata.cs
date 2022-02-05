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

        public float fBM(float x, float z)
        {
            return fBM(x, z, octaves, scale, height_scale, height_offset);
        }

        /// <summary>
        /// �|�[�h�� PerlinNoise�A�çQ�Ψ�L�ѼơA�b�ۦP��m (x, z) �W������P�� PerlinNoise
        /// reference: https://thebookofshaders.com/13/?lan=ch
        /// </summary>
        /// <param name="x">X �y��</param>
        /// <param name="z">Z �y��</param>
        /// <param name="octaves">�|�[ PerlinNoise ���ռơA�|���ܪi��</param>
        /// <param name="scale">�Y�� x, z ���Ƚd��A�|���ܪi��</param>
        /// <param name="height_scale">�Y�� PerlinNoise �p��ȡA�|���ܪi��</param>
        /// <param name="height_offset">�|�[���h�� PerlinNoise ��A�̫�b�[�W�����װ����q�A���|���ܪi��</param>
        /// <returns> ���Υ��ԹB�ʡ]Fractal Brownian Motion�^ </returns>
        public static float fBM(float x, float z, int octaves, float scale, float height_scale, float height_offset)
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
        /// <param name="offset"></param>
        /// <returns></returns>
        public static float getAltitude(float x, float z, float altitude, int octaves, float scale, float height_scale, float offset = 0f)
        {
            float height = 0f;
            float frequncy = 1f;

            // �����A�@�ӤK�ס]octave�^�������W�v�W���[���δ�b�C
            for (int i = 0; i < octaves; i++)
            {
                height += Mathf.PerlinNoise(x * scale * frequncy, z * scale * frequncy) * height_scale;
                frequncy *= 2f;
            }

            // �������ޥ����ȡA�i�ϼƭȦb�Ӯ��ޤW�U�i��            
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
