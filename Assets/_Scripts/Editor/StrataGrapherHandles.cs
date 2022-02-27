using UnityEditor;
using UnityEngine;

namespace minecraft
{
    [CustomEditor(typeof(StrataGrapher))]
    public class StrataGrapherHandler : Editor
    {
        void OnSceneGUI()
        {
            StrataGrapher grapher = (StrataGrapher)target;

            if (grapher == null)
            {
                return;
            }

            Handles.color = Color.white;
            Handles.Label(grapher.lr.GetPosition(0) + Vector3.up * 2,
                "Layer: " +
                grapher.gameObject.name);
        }

    }
}
