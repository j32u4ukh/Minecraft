using UnityEngine;

namespace minecraft
{
    [ExecuteInEditMode]
    public class StrataGrapher : MonoBehaviour
    {
        public LineRenderer lr;

        //[Header("�Y��i�����_�T")]
        //public float height_scale = 2;

        //[Header("�Y������I���y��")]
        //[Range(0.0f, 1.0f)]
        //public float scale = 0.5f;

        //[Header("�|�[ PerlinNoise �h��")]
        //[Min(1)]
        //public int octaves = 1;

        //[Header("�ؼЮ��ް���")]
        //public float altitude = 1f;

        public StrataSetting setting;

        [SerializeField] int z = 11;
        [SerializeField] int n_position = 100;
        [SerializeField] bool use_altitude = true;

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

            float offset = Strata.getPerlinMean(0, n_position, 11f, 11f, setting.octaves * setting.height_scale, n_sample: 100 * setting.octaves);
            float y;

            for (int x = 0; x < n_position; x++)
            {
                //float y = Mathf.PerlinNoise(x, z) * height_scale;

                if (use_altitude)
                {
                    //y = Strata.getAltitude(x, z, altitude: setting.altitude, octaves: setting.octaves, scale: setting.scale, height_scale: setting.height_scale, offset);
                    y = setting.getAltitude(x, z, offset);
                }
                else
                {
                    y = Strata.fBM(x, z, octaves: setting.octaves, scale: setting.scale, height_scale: setting.height_scale, height_offset: setting.altitude);
                }
                
                positions[x] = new Vector3(x, y, z);
            }

            lr.SetPositions(positions);
        }
    }
}
