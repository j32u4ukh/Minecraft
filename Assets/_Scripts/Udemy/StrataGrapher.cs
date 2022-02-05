using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace udemy
{
    public class StrataGrapher : MonoBehaviour
    {
        public LineRenderer lr;
        public float height_scale = 2;

        [Range(0.0f, 1.0f)]
        public float scale = 0.5f;

        public int octaves = 1;
        public float height_offset = 1f;

        public int z = 11;
        public int n_position = 100;

        // Start is called before the first frame update
        void Start()
        {
            lr = GetComponent<LineRenderer>();
            lr.positionCount = n_position;
            graphPerlinNoise();
        }
        
        private void OnValidate()
        {
            graphPerlinNoise();
        }

        void graphPerlinNoise()
        {
            lr = GetComponent<LineRenderer>();
            lr.positionCount = n_position;

            Vector3[] positions = new Vector3[n_position];
            float offset = Strata.getPerlinMean(0, n_position, 11f, 11.1f, octaves * height_scale, n_sample: 100 * octaves);

            for (int x = 0; x < n_position; x++)
            {
                //float y = Mathf.PerlinNoise(x, z) * height_scale;
                //float y = Strata.fBM(x, z, octaves: octaves, scale: scale, height_scale: height_scale, height_offset: height_offset);
                float y = Strata.getAltitude(x, z, altitude: height_offset, octaves: octaves, scale: scale, height_scale: height_scale, offset);
                //float y = MeshUtils.fBM(x, z, octaves, scale, heightScale, heightOffset);
                positions[x] = new Vector3(x, y, z);
            }

            lr.SetPositions(positions);
        }
    }
}
