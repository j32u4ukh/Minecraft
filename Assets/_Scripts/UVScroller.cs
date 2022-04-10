using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace udemy
{
    /// <summary>
    /// �z�L UV ��m�������A�y���y�ʪ��ĪG�A���ȭ���F�� UV �Ϯ׬ۦP��
    /// ���� �� ���� �|�ݭn�ϥ�
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
