using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine;
using System.Collections;
using UnityEditor;

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
