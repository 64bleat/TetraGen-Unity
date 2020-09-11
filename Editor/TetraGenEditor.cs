using UnityEditor;
using UnityEngine;

namespace TetraGen
{
    [CustomEditor(typeof(TetraGenMaster))]
    public class TetraGenMasterEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (GUILayout.Button("Generate Surface"))
                ((TetraGenMaster)target).Generate();
            if (GUILayout.Button("Clear Generated Objects"))
                ((TetraGenMaster)target).ClearMeshes();
        }
    }
}
