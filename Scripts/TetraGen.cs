using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SHK.Isosurfaces
{
    /// <summary>
    /// One chunk of the TetraGen system. May be used by itself, or through a TetraGenMaster
    /// </summary>
    [ExecuteInEditMode]
    public class TetraGen : MonoBehaviour
    {
        public Vector3 cellBounds;
        public int xVerts, yVerts, zVerts;
        public bool flipNormals = false;
        public ComputeShader tetraComputer;
        public GameObject meshTemplate;

        [HideInInspector]
        public List<GameObject> isoMeshes = new List<GameObject>();
        private TriangleData[] newTriangleData;
        private Vector3[] newVertices;
        private Vector3[] newNormals;
        private int[] newTriangles;

        /// <summary>
        /// Triangle to be sent out of the compute shader.
        /// </summary>
        private struct TriangleData
        {
            public Vector3 position;    // float * 3
            public Vector3 normal;      // float * 3
            public Vector3 color;       // float * 3
            public int index;           // uint * 1
        }                               // sizeof(float) * 9 + sizeof(uint) * 6

        /// <summary>
        /// Shape data to be sent into the compute shader.
        /// </summary>
        public struct ShapeData
        {
            public uint shapeType;                  // uint * 1
            public uint blendMode;                  // uint * 1
            public float blendFactor;               // float * 1
            public float bevelRadius;               // float * 1
            public Matrix4x4 worldToLocalMatrix;    // float * 16
            public Matrix4x4 localToWorldMatrix;    // float * 16
        }                                           // sizeof(uint) * 2 + sizeof(float) * 34

        /// <summary>
        /// Clear all temporary data after generating meshes.
        /// </summary>
        public void Flush()
        {
            newTriangleData = null;
            newVertices = null;
            newNormals = null;
            newTriangles = null;
        }

        /// <summary>
        /// Convert shapes into ShapeData for the compute shader.
        /// Shapes must be children of this object and will be
        /// computed from top to bottom, as they appear in the heirarchy.
        /// </summary>
        public ShapeData[] RetrieveShapeData()
        {
            TetraGenShape[] shapes = GetComponentsInChildren<TetraGenShape>();
            ShapeData[] shapeData = new ShapeData[shapes.Length];
            int cur = 0;

            for (int s = 0; s < shapes.Length; s++)
                if (shapes[s].gameObject.activeSelf)
                    shapeData[cur++] = shapes[s].GetShapeData();

            Array.Resize<ShapeData>(ref shapeData, cur);

            return shapeData;
        }

        /// <summary>
        /// Generates the isosurvace for this chunk.
        /// </summary>
        /// <param name="shapeData"></param>
        /// <param name="collisionEnabled">Enabling collision takes a lot of time.</param>
        public void Generate(ShapeData[] shapeData, bool collisionEnabled = true)
        {
            int bufferSize = 36 * xVerts * yVerts * zVerts;
            int weightBufferSize = (xVerts + 1) * (yVerts + 1) * (zVerts + 1);

            if (newTriangleData == null || newTriangleData.Length != bufferSize)
                newTriangleData = new TriangleData[bufferSize];
            if (newVertices == null || newVertices.Length != bufferSize)
                newVertices = new Vector3[bufferSize];
            if (newNormals == null || newNormals.Length != bufferSize)
                newNormals = new Vector3[bufferSize];
            if (newTriangles == null || newTriangles.Length != bufferSize)
                newTriangles = new int[bufferSize];

            if (shapeData.Length != 0)
            {
                // Use the GPU compute shader to generate the weight field
                // and mesh triangles, then export the triangle data
                // as an unrefined array with blank spaces
                // where no triangle exists.
                int meshKernel = tetraComputer.FindKernel("MarchingTetrahedra");
                int weightKernel = tetraComputer.FindKernel("CalculateWeights");
                ComputeBuffer shapeBuffer = new ComputeBuffer(shapeData.Length, sizeof(uint) * 2 + sizeof(float) * 34);
                ComputeBuffer weightBuffer = new ComputeBuffer(weightBufferSize, sizeof(float));
                ComputeBuffer triangleBuffer = new ComputeBuffer(bufferSize, sizeof(float) * 9 + sizeof(uint));

                shapeBuffer.SetData(new List<ShapeData>(shapeData));
                tetraComputer.SetBuffer(weightKernel, "shapes", shapeBuffer);
                tetraComputer.SetBuffer(meshKernel, "weights", weightBuffer);
                tetraComputer.SetBuffer(weightKernel, "weights", weightBuffer);
                tetraComputer.SetBuffer(meshKernel, "trianglesToAdd", triangleBuffer);
                tetraComputer.SetMatrix("gridToWorldMatrix", transform.localToWorldMatrix);
                tetraComputer.SetInt("shapeCount", shapeData.Length);
                tetraComputer.SetInt("xBound", xVerts + 1);
                tetraComputer.SetInt("yBound", yVerts + 1);
                tetraComputer.SetInt("zBound", zVerts + 1);
                tetraComputer.SetInt("flipNormal", flipNormals ? 1 : -1);
                tetraComputer.SetFloats("cellBounds", new float[] { cellBounds.x, cellBounds.y, cellBounds.z });

                tetraComputer.Dispatch(weightKernel, xVerts + 1, yVerts + 1, zVerts + 1);
                tetraComputer.Dispatch(meshKernel, xVerts, yVerts, zVerts);

                triangleBuffer.GetData(newTriangleData);

                shapeBuffer.Release();
                weightBuffer.Release();
                triangleBuffer.Release();

                // remove all blank spaces from the triangle array
                // if valid, index == 1, else index == 0
                int vertexCount = 0;

                for (int t = 0; t < newTriangleData.Length; t += 3)
                {
                    if (newTriangleData[t].index == 1)
                    {
                        for (int v = 0; v < 3; v++)
                        {
                            newVertices[vertexCount + v] = newTriangleData[t + v].position;
                            newNormals[vertexCount + v] = newTriangleData[t + v].normal;
                            newTriangles[vertexCount + v] = vertexCount + v;
                        }

                        vertexCount += 3;
                    }
                }

                // Destroy pre-existing meshes and generate new ones.
                // Meshes may only contain 65535 vertices.
                // If there are too many vertices, make more meshes.
                if (vertexCount != 0)
                {
                    for (int g = 0; g < isoMeshes.Count; g++)
                        if (isoMeshes[g])
                        {
                            DestroyImmediate(isoMeshes[g].GetComponent<MeshFilter>().sharedMesh);
                            DestroyImmediate(isoMeshes[g]);
                        }

                    isoMeshes.Clear();

                    int meshCount = Mathf.CeilToInt((float)vertexCount / 65535f);

                    for (int i = 0; i < meshCount; i++)
                    {
                        GameObject mo = Instantiate(meshTemplate, transform);
                        MeshFilter mf = mo.GetComponent<MeshFilter>();
                        MeshCollider mc = mo.GetComponent<MeshCollider>();
                        Mesh mesh = new Mesh();
                        int startIndex = i * 65535;
                        int endIndex = Mathf.Min(vertexCount, startIndex + 65535);
                        int meshSize = endIndex - startIndex;
                        Vector3[] meshVerts = new Vector3[meshSize];
                        Vector3[] meshNorms = new Vector3[meshSize];
                        int[] meshTris = new int[meshSize];

                        for (int m = startIndex; m < endIndex; m++)
                        {
                            meshVerts[m - startIndex] = newVertices[m];
                            meshNorms[m - startIndex] = newNormals[m];
                            meshTris[m - startIndex] = m - startIndex;
                        }

                        mesh.vertices = meshVerts;
                        mesh.normals = meshNorms;
                        mesh.triangles = meshTris;
                        mesh.RecalculateBounds();
                        mesh.RecalculateTangents();
                        mesh.name = mo.name = "Generated Mesh " + i;

                        mf.sharedMesh = mesh;

                        if (collisionEnabled && mc.enabled)
                            mc.sharedMesh = mesh;

                        isoMeshes.Add(mo);
                    }
                }
            }
        }
    }
}