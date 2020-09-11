using UnityEngine;

namespace TetraGen
{
    /// <summary> Explicitly identifies a mesh generation kernel </summary>
    public class TGMeshKernel : TGKernel
    {
        [Tooltip("The buffer needs to know how many triangles are generated per cell in this kernel.")]
        public int trianglesPerCell;
    }
}
