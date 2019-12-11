using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SHK.Isosurfaces
{
    /// <summary>
    /// Creates a large area of TetraGen chunks
    /// </summary>
    public class TetraGenMaster : MonoBehaviour
    {
        [Header("Size of each cell")]
        public Vector3 cellAspect = new Vector3(1, 1, 1);
        [Header("Cells per chunk")]
        public int chunkCellsX = 16;
        public int chunkCellsY = 16;
        public int chunkCellsZ = 16;
        [Header("Total number of chunks")]
        public int chunkCountX = 1;
        public int chunkCountY = 1;
        public int chunkCountZ = 1;
        [Header("Other")]
        public bool flipNormals = false;
        public bool generateCollision = true;
        public GameObject tetraGenChunk;

        public void OnValidate()
        {
            chunkCellsX = Mathf.Max(1, chunkCellsX);
            chunkCellsY = Mathf.Max(1, chunkCellsY);
            chunkCellsZ = Mathf.Max(1, chunkCellsZ);
            chunkCountX = Mathf.Max(1, chunkCountX);
            chunkCountY = Mathf.Max(1, chunkCountY);
            chunkCountZ = Mathf.Max(1, chunkCountZ);
        }

        /// <summary>
        /// Master generation script. Will go through and generate
        /// multiple isosurface chunks on a single set of shape data.
        /// </summary>
        public void Generate()
        {
            TetraGen.ShapeData[] shapeData;

            // Destroy old meshes first, then old chunks.
            {
                TetraGen[] oldChunks = GetComponentsInChildren<TetraGen>();
                TetraGenMesh[] meshes = GetComponentsInChildren<TetraGenMesh>();

                for (int m = 0; m < meshes.Length; m++)
                    DestroyImmediate(meshes[m].gameObject.GetComponent<MeshFilter>().sharedMesh);

                for (int i = 0; i < oldChunks.Length; i++)
                    DestroyImmediate(oldChunks[i].gameObject);
            }
            
            // Gather shape data for generating meshes
            {
                TetraGenShape[] shapes = GetComponentsInChildren<TetraGenShape>();
                shapeData = new TetraGen.ShapeData[shapes.Length];
                int cur = 0;

                for (int s = 0; s < shapes.Length; s++)
                    if (shapes[s].gameObject.activeSelf)
                        shapeData[cur++] = shapes[s].GetShapeData();

                Array.Resize(ref shapeData, cur);
            }

            // Pass shape data to every chunk and generate chunk meshes with it
            for (int x = 0; x < chunkCountX; x++)
                for (int y = 0; y < chunkCountY; y++)
                    for (int z = 0; z < chunkCountZ; z++)
                    {
                        Vector3 transformOffset = new Vector3(x * chunkCellsX * cellAspect.x, y * chunkCellsY * cellAspect.y, z * chunkCellsZ * cellAspect.z);
                        GameObject chunkObject = Instantiate(tetraGenChunk, transform);
                        TetraGen tetraGen = chunkObject.GetComponent<TetraGen>();
                        chunkObject.transform.localPosition = transformOffset;
                        tetraGen.xVerts = chunkCellsX;
                        tetraGen.yVerts = chunkCellsY;
                        tetraGen.zVerts = chunkCellsZ;
                        tetraGen.cellBounds = cellAspect;
                        tetraGen.flipNormals = flipNormals;
                        tetraGen.Generate(shapeData, generateCollision);
                        tetraGen.Flush();
                    }
        }
    }
}
