using UnityEngine;

namespace TetraGen
{
    /// <summary>
    ///     Shape data to be sent into the compute shader.
    /// </summary>
    public struct ShapeData
    {
        public uint shapeType;          // uint * 1
        public uint blendMode;          // uint * 1
        public float blendFactor;       // float * 1
        public float bevelRadius;       // float * 1
        public Matrix4x4 world2Local;   // float * 16
        public Matrix4x4 local2World;   // float * 16
    }                                   // sizeof(uint) * 2 + sizeof(float) * 34

    /// <summary>
    ///     GameObject representation of ShapeData used in TetraGen surface
    ///     generation. Add TetraGenShapes to GameObjects as children of 
    ///     TetraGenMasters to be included in surface generation.
    /// </summary>
    [DisallowMultipleComponent]
    public class TetraGenShape : MonoBehaviour
    {
        public enum ShapeType { Sphere, Box, Plane };
        public enum BlendType { Union, Subtraction, Smooth, SmoothUnion, Intersect, Repel, Lerp };

        public ShapeType shape = ShapeType.Sphere;
        public BlendType blendType = BlendType.Union;
        public float blendFactor = 0;
        public float bevelRadius = 0;

        public void OnValidate()
        {
            gameObject.name = "TetraGen " + blendType + " " + shape;
        }

        public ShapeData Shape
        {
            get => new ShapeData()
            {
                shapeType = (uint)shape,
                blendMode = (uint)blendType,
                blendFactor = blendFactor,
                bevelRadius = bevelRadius,
                world2Local = transform.worldToLocalMatrix,
                local2World = transform.localToWorldMatrix
            };
        }
    }
}
