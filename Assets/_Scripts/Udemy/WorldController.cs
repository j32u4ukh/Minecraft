using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace udemy
{
	public class WorldController : MonoBehaviour
	{

		public GameObject block;
		public int width = 1;
		public int height = 1;
		public int depth = 1;

		// Use this for initialization
		void Start()
		{
			StartCoroutine(BuildWorld());
		}

		public IEnumerator BuildWorld()
		{
			for (int z = 0; z < depth; z++)
			{
				for (int y = 0; y < height; y++)
				{
					for (int x = 0; x < width; x++)
					{
						if (y == height - 1 && Random.Range(0, 100) < 50)
						{
							continue;
						}

						Vector3 pos = new Vector3(x, y, z);
						GameObject cube = GameObject.Instantiate(block, pos, Quaternion.identity);
						cube.name = x + "_" + y + "_" + z;

						// 令所有方塊使用相同 Material
						cube.GetComponent<Renderer>().material = new Material(Shader.Find("Standard"));
					}

					yield return null;
				}
			}
		}

		public IEnumerator BuildWorld2()
		{
			bool skip;

			for (int z = 0; z < depth; z++)
			{
				for (int x = 0; x < width; x++)
				{
					skip = Random.Range(0, 100) < 50;

					for (int y = 0; y < height; y++)
					{
						if (skip && (y >= height - 1))
						{
							continue;
						}

						Vector3 pos = new Vector3(x, y, z);
						GameObject cube = GameObject.Instantiate(block, pos, Quaternion.identity);
						cube.name = x + "_" + y + "_" + z;

						// 令所有方塊使用相同 Material
						cube.GetComponent<Renderer>().material = new Material(Shader.Find("Standard"));
					}

					yield return null;
				}
			}
		}
	}
}