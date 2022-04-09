using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ScreenShot))]
public class ScreenShotEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ScreenShot handle = (ScreenShot)target;

        if (GUILayout.Button("Screen Shot"))
        {
            handle.takePhoto();
        }
    }
}
