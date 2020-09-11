using UnityEngine;

namespace TetraGen
{
    /// <summary> Generic kernel identification </summary>
    public class TGKernel : ScriptableObject
    {
        public ComputeShader computer;
        public string kernelName;
        public Vector3Int numThreads = new Vector3Int(1,1,1);
        [HideInInspector]
        public int id;

        /// <summary> ensures kernel id is set </summary>
        public virtual void Init()
        {
            id = computer.FindKernel(kernelName);
        }
    }
}
