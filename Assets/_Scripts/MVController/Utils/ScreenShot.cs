using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[ExecuteInEditMode]
public class ScreenShot : MonoBehaviour
{
    public int width;
    public int height;
    Camera m_camera;

    private void Start()
    {
        m_camera = GetComponent<Camera>();
    }

    public void takePhoto()
    {
        // ¼È¦s·í«e RenderTexture
        RenderTexture origin_texture = m_camera.targetTexture;

        RenderTexture render_texture = new RenderTexture(width, height, 24);
        m_camera.targetTexture = render_texture;
        m_camera.Render();

        RenderTexture.active = render_texture;
        Texture2D texture = new Texture2D(width, height);
        texture.ReadPixels(new Rect(0, 0, render_texture.width, render_texture.height), 0, 0);
        texture.Apply();

        m_camera.targetTexture = origin_texture;

        var bytes = texture.EncodeToPNG();
        DestroyImmediate(texture);

        string folder = Path.Combine(Application.streamingAssetsPath, "ScreenShot");

        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        string file_name = DateTime.Now.ToString("yyyy-MM-dd#HH-mm-ss-ffff");
        string path = Path.Combine(Application.streamingAssetsPath, "ScreenShot", $"{file_name}.png");
        File.WriteAllBytes(path, bytes);
    }
}
