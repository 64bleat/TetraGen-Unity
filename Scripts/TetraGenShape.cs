using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SHK.Isosurfaces
{
    /// <summary>
    /// TetraGen shapes to be passed into the generation step. This Class
    /// simply generates data for the compute shader. The GameObject's
    /// transform is used to transform the shape within the shader.
    /// Distance sampling points are transformed into object space where
    /// the shape's dimensions are normalized, a closest point is found and 
    /// transformed back to world space, and the distance value is taken from
    /// that.
    /// </summary>
    public class TetraGenShape : MonoBehaviour
    {
        public enum Shape { Sphere, Box, Plane };
        public Shape shape;

        public enum BlendType { Union, Subtraction, Smooth, SmoothUnion, Intersect, Repel };
        public BlendType blendType;

        public float blendFactor = 0;
        public float bevelRadius = 0;

        public TetraGen.ShapeData GetShapeData()
        {
            TetraGen.ShapeData shapeData = new TetraGen.ShapeData()
            {
                shapeType = (uint)shape,
                blendMode = (uint)blendType,
                blendFactor = this.blendFactor,
                bevelRadius = this.bevelRadius,
                worldToLocalMatrix = transform.worldToLocalMatrix,
                localToWorldMatrix = transform.localToWorldMatrix
            };

            return shapeData;
        }

        public void OnValidate()
        {
            gameObject.name = "TetraGen " + blendType + " " + shape;
        }
    }
}
