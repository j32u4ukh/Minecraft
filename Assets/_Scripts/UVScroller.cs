using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace udemy
{
    /// <summary>
    /// 透過 UV 位置的偏移，造成流動的效果，但僅限於鄰近 UV 圖案相同時
    /// 水面 或 岩漿 會需要使用
    /// </summary>
    public class UVScroller : MonoBehaviour
    {
        Vector2 speed = new Vector2(0, 0.01f);
        Vector2 offset = Vector2.zero;
        Renderer m_renderer;

        void Start()
        {
            m_renderer = GetComponent<Renderer>();
        }

        void LateUpdate()
        {
            offset += speed * Time.deltaTime;

            if (offset.y > MeshUtils.UV_SIZE)
            {
                offset = new Vector2(offset.x, 0);
            }

            m_renderer.materials[0].SetTextureOffset("_MainTex", offset);
        }
    }
}
