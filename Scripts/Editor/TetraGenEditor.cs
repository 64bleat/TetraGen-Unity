using UnityEditor;
using UnityEngine;

namespace TetraGen
{
    /// <summary>
    ///     Adds a "Generate Surface" button to TetraGenMaster Components
    /// </summary>
    [CustomEditor(typeof(TetraGenMaster))]
    public class TetraGenMasterEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (GUILayout.Button("Generate Surface"))
                ((TetraGenMaster)target).Generate();
        }
    }
}
