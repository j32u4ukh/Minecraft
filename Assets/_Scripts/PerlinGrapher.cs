using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class PerlinGrapher : MonoBehaviour
{
    public LineRenderer lr;
    public float hightScale = 2;
    public float scale = 0.85f;
    public int octaves = 1;
    public float heightOffset = 1f;

    // Start is called before the first frame update
    void Start()
    {
        lr = GetComponent<LineRenderer>();
        lr.positionCount = 100;
        Graph();
    }

    float fBM(float x, float z)
    {
        float total = 0f;
        float frequncy = 1f;

        for(int i = 0; i < octaves; i++)
        {
            total += Mathf.PerlinNoise(x * scale * frequncy, z * scale * frequncy) * hightScale;
            frequncy *= 2f;
        }

        return total;
    }

    void Graph()
    {
        lr = GetComponent<LineRenderer>();
        lr.positionCount = 100;

        int z = 11;
        Vector3[] positions = new Vector3[lr.positionCount];

        for(int x = 0; x < lr.positionCount; x++) 
        {
            float y = fBM(x, z) + heightOffset;
            positions[x] = new Vector3(x, y, z);
        }

        lr.SetPositions(positions);
    }

    private void OnValidate()
    {
        Graph();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
