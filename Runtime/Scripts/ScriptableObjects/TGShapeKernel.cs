using UnityEngine;

namespace TetraGen
{
    /// <summary> Explicitly identifies a shape kernel </summary>
    public class TGShapeKernel : TGKernel
    {
#if UNITY_EDITOR
        public Mesh gizmoMesh;
#endif
    }
}
