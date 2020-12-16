using System.Collections.Generic;
using UnityEngine;

namespace TetraGen
{
    /// <summary>
    ///     Component representation of ShapeData used in TetraGen surface
    ///     generation. Add TetraGenShapes to GameObjects to be included 
    ///     in surface generation.
    /// </summary>
    public class TetraGenShape : MonoBehaviour
    {
        public TGShapeKernel addShape;
        public TGBlendKernel blendMode;
        public float blendFactor = 0;
        public float bevelRadius = 0;
        public float activeRadius = 100;

        public void OnValidate()
        {
            if (addShape && blendMode)
                gameObject.name = addShape.name + " " + blendMode.name;

            if (addShape)
                addShape.Init();

            if (blendMode)
                blendMode.Init();
        }

        private void Awake()
        {
            if (addShape)
                addShape.Init();

            if (blendMode)
                blendMode.Init();
        }

        public ShapeData ToShapeData()
        {
            return new ShapeData()
            {
                blendFactor = blendFactor,
                bevelRadius = bevelRadius,
                world2Local = transform.worldToLocalMatrix,
                local2World = transform.localToWorldMatrix
            };
        }

        private static readonly List<TetraGenShape> addBuffer = new List<TetraGenShape>();

        public static TetraGenShape[] GetShapesIn(Transform container)
        {
            addBuffer.Clear();

            foreach (TetraGenShape shape in container.GetComponentsInChildren<TetraGenShape>())
                if (shape.addShape && shape.blendMode && shape.gameObject.activeInHierarchy)
                    addBuffer.Add(shape);

            return addBuffer.ToArray();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (addShape && addShape.gizmoMesh)
                Gizmos.DrawWireMesh(addShape.gizmoMesh, transform.position, transform.rotation, transform.lossyScale);
        }

#endif
    }
}
