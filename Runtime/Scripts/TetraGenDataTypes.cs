using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace TetraGen
{
    /// <summary> Representation of Signed Distance shapes for GPU processing. </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ShapeData
    {
        public static readonly int stride = sizeof(float) * 34;

        public float blendFactor;
        public float bevelRadius;
        public Matrix4x4 world2Local;  
        public Matrix4x4 local2World;  
    }

    /// <summary> Representation of a vertex for being assigned to meshes </summary>
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
    }

    /// <summary> Representation of a triangle for GPU processing </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Triangle
    {
        public static readonly int stride = sizeof(float) * 12;

        public Vector3 a;
        public Vector3 b;
        public Vector3 c;
        public Vector3 n;
    }
}
