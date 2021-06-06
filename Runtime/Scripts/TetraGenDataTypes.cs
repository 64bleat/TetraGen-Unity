using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace TetraGen
{
    /// <summary> Information associated with specific chunks </summary>
    [System.Serializable]
    public class ChunkData
    {
        public readonly List<GameObject> meshes = new List<GameObject>();
    }

    /// <summary> TetraGen signed-distance shape data </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ShapeData
    {
        public static readonly int stride = sizeof(float) * 34;

        public float blendFactor;
        public float bevelRadius;
        public Matrix4x4 world2Local;  
        public Matrix4x4 local2World;  
    }

    /// <summary> UnityEngine mesh vertex data </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Vertex
    {
        public static readonly int stride = sizeof(float) * 6;
        public static readonly VertexAttributeDescriptor[] meshAttributes = new VertexAttributeDescriptor[]{
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3)
        };

        public Vector3 position;
        public Vector3 normal;

        public Vertex(Vector3 p, Vector3 n)
        {
            position = p;
            normal = n;
        }
    }

    /// <summary> TetraGen mesh triangle </summary>
    /// <remarks> Coincides with Triangle in TetraGenInclude.cginc </remarks>
    [StructLayout(LayoutKind.Sequential)]
    public struct Triangle
    {
        public static readonly int stride = sizeof(float) * 3 * 6;

        public Vector3 a;
        public Vector3 b;
        public Vector3 c;
        public Vector3 na;
        public Vector3 nb;
        public Vector3 nc;
    }

    /// <summary> Tetragen lattice cell data</summary>
    /// <remarks> Coincides with Cell in TetraGenInclude.cginc </remarks>
    [StructLayout(LayoutKind.Sequential)]
    public struct Cell
    {
        public static readonly int stride = sizeof(float) * 8;
    }

    /// <summary> TetraGen lattice shape blending data</summary>
    /// <remarks> Coincides with BlendCell in TetraGenInclude.cginc </remarks>
    [StructLayout(LayoutKind.Sequential)]
    public struct BlendCell
    {
        public static readonly int stride = sizeof(float) * 4;
    }
}
