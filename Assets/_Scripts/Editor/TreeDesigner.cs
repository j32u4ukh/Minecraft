using UnityEngine;
using System.Collections;
using UnityEditor;

namespace udemy
{
    [CustomEditor(typeof(TreeCreator))]
    public class TreeDesigner : Editor
    {
        Vector2 scroll_pos;
        int SCROLL_HEIGHT = 100;
        int TEXT_AREA_HEIGHT = 800;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            TreeCreator handle = (TreeCreator)target;

            if (GUILayout.Button("Realign Blocks"))
            {
                handle.reAlignBlocks();
            }

            scroll_pos = EditorGUILayout.BeginScrollView(scroll_pos, GUILayout.Height(SCROLL_HEIGHT));
            EditorGUILayout.TextArea(handle.block_detail, GUILayout.Height(TEXT_AREA_HEIGHT));
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Update Details"))
            {
                handle.getDetails();
            }
        }
    }
}
