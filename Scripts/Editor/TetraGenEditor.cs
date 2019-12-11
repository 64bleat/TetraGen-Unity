using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SHK.Isosurfaces
{
    [CustomEditor(typeof(TetraGen))]
    public class TetraGenEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (GUILayout.Button("Generate"))
                ((TetraGen)target).GetComponentInParent<TetraGenMaster>().Generate();
        }
    }

    [CustomEditor(typeof(TetraGenMesh))]
    public class TetraGenMeshEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (GUILayout.Button("Generate"))
                ((TetraGenMesh)target).GetComponentInParent<TetraGenMaster>().Generate();
        }
    }

    [CustomEditor(typeof(TetraGenMaster))]
    public class TetraGenMasterEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (GUILayout.Button("Generate"))
            {
                ((TetraGenMaster)target).Generate();
            }
        }
    }
}
