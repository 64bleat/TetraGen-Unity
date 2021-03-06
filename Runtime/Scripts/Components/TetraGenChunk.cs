﻿using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TetraGen
{
    /// <summary> 
    ///     <para> TetraGenChunk contains all the necessary tools to generate 
    ///     isosurface meshes and the necessary signed-distance fields. </para> 
    /// </summary> 
    /// <remarks>
    ///     <para> First initialize the TetraGenChunk with <c>GenerationStart</c>. </para>
    ///     
    ///     <para> After initialization you can <c>SetTriangleBuffer</c> and
    ///     <c>GenerateMesh</c> as much as you like.</para>
    ///         
    ///     <para> This class takes up a ton of memory when initialized.
    ///     When you are done generating, close the chunk with
    ///     <c>GenerationEnd</c>. It will also be called when the
    ///     component is destroyed while initialized. </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class TetraGenChunk : MonoBehaviour
    {
        [Tooltip("The compute shader that contains the isosurface mesh generator kernel.")]
        public TGMeshKernel meshKernel;
        [Tooltip("Generates the signed-distance field point lattice")]
        public TGPositionKernel positionKernel;
        [Tooltip("Generated meshes will be instances of this GameObject.")]
        public MeshFilter meshTemplate;
        [Tooltip("Set generated mesh material to this material if not null.")]
        public Material meshMaterial;
        [Tooltip("Enables MeshCollider generation. Quite a slow procedure.")]
        public bool generateCollision = true;
        [Tooltip("Flip the forward direction of the generated surface.")]
        public bool flipNormals = false;
        [Tooltip("cell cize per direction")]
        public Vector3 cellScale = new Vector3(1, 1, 1);
        [Tooltip("Cell count per direction within a chunk")]
        public Vector3Int cellCount = new Vector3Int(16, 16, 16);

        /// <summary> maximum number of triangles in a Mesh </summary>
        private const int trianglesPerMesh = 21845;
        /// <summary> maximum number of vertices in a Mesh </summary>
        private const int verticesPerMesh = 65535;

        /// <summary> true when this chunk is initialized for mesh generation </summary>
        private bool generationReady = false;
        /// <summary> stores the primary chunk signed distance field </summary>
        private ComputeBuffer weightBuffer;
        /// <summary> stores the secondary chunk signed distance field, used for combining fields into weightBuffer </summary>
        private ComputeBuffer blendBuffer;
        /// <summary> stores the GPU-generated triangles for export </summary>
        private ComputeBuffer triangleBuffer;
        /// <summary> stores a count of how many triangles were generated in each cell for faster sorting </summary>
        private ComputeBuffer tCountBuffer;
        /// <summary> stores a list of shapes to compile into weightBuffer </summary>
        private ComputeBuffer shapeBuffer;
        /// <summary> stores data retrieved from triangleBuffer </summary>
        private Triangle[] gpuTriangleBuffer;
        /// <summary> stores data retrieved from tCountBuffer </summary>
        private int[] gpuTCountBuffer;
        /// <summary> Stores data compiled from triangleData </summary>
        private Vertex[] meshVertexBuffer;
        /// <summary> stores a generic triangle array that gets added to meshes </summary>
        private ushort[] meshTriangleBuffer;
        /// <summary> single structs still have to be passed to the GPU as a struct array </summary>
        private readonly ShapeData[] gpuShapeData = new ShapeData[1];
        /// <summary> a single Vector3 must be passed to the GPU as a float array </summary>
        private readonly float[] gpuCellScale = new float[3];
        /// <summary> calculate chunk bounds in GenerationStart</summary>
        private Bounds chunkBounds;
        /// <summary> Store discarded mesh prefab instances in a pool rather than destroying them</summary>
        private readonly Stack<GameObject> meshObjectPool = new Stack<GameObject>();

        private RenderTexture rtPosition;
        private RenderTexture rtNormalDistance;
        private RenderTexture rtNormalDistanceBlend;

        private void OnDestroy()
        {
            GenerationEnd();
        }

        /// <summary> Initializes the chunk for mesh generation </summary>
        public void GenerationStart()
        {
            int cellDataLength = cellCount.x * cellCount.y * cellCount.z;
            int triangleDataLength = cellDataLength * meshKernel.trianglesPerCell;
            int weightBufferLength = (cellCount.x + 1) * (cellCount.y + 1) * (cellCount.z + 1);
            Vector3 chunkBoundSize;

            chunkBoundSize.x = cellCount.x * cellScale.x;
            chunkBoundSize.y = cellCount.y * cellScale.y;
            chunkBoundSize.z = cellCount.z * cellScale.z;

            chunkBounds = new Bounds(chunkBoundSize / 2, chunkBoundSize);
            gpuTriangleBuffer = new Triangle[triangleDataLength];
            gpuTCountBuffer = new int[cellDataLength];
            meshVertexBuffer = new Vertex[verticesPerMesh];
            meshTriangleBuffer = new ushort[verticesPerMesh];

            weightBuffer = new ComputeBuffer(weightBufferLength, Cell.stride);
            blendBuffer = new ComputeBuffer(weightBufferLength, BlendCell.stride);
            triangleBuffer = new ComputeBuffer(triangleDataLength, Triangle.stride);
            tCountBuffer = new ComputeBuffer(cellDataLength, sizeof(int));
            shapeBuffer = new ComputeBuffer(1, ShapeData.stride);

            meshKernel.Init();
            positionKernel.Init();

            generationReady = true;

            rtPosition = new RenderTexture(32, 32, 0, RenderTextureFormat.ARGBFloat);
            rtPosition.volumeDepth = 32;
            rtPosition.dimension = TextureDimension.Tex3D;
            rtPosition.enableRandomWrite = true;
            rtPosition.Create();

            rtNormalDistance = new RenderTexture(32, 32, 0, RenderTextureFormat.ARGBFloat);
            rtNormalDistance.volumeDepth = 32;
            rtNormalDistance.dimension = TextureDimension.Tex3D;
            rtNormalDistance.enableRandomWrite = true;
            rtNormalDistance.Create();

            rtNormalDistanceBlend = new RenderTexture(32, 32, 0, RenderTextureFormat.ARGBFloat);
            rtNormalDistanceBlend.volumeDepth = 32;
            rtNormalDistanceBlend.dimension = TextureDimension.Tex3D;
            rtNormalDistanceBlend.enableRandomWrite = true;
            rtNormalDistanceBlend.Create();
        }

        //private readonly Queue<TetraGenShape> shapeQueue = new Queue<TetraGenShape>();
        /// <summary> Pupulates the triangleData array for mesh generation. </summary>
        /// <param name="shapes"> shape data passed to the GPU to form the signed distance field </param>
        /// <param name="chunk2world"> transforms chunk space into world space on the GPU</param>
        public void SetTriangleBuffer(IList<TetraGenShape> shapes, Transform master, Vector3Int chunkIndex)
        {
            if (!generationReady)
                GenerationStart();

            Vector3 chunkCenter;
            chunkCenter.x = chunkIndex.x * cellScale.x;
            chunkCenter.y = chunkIndex.y * cellScale.y;
            chunkCenter.z = chunkIndex.z * cellScale.z;
            Vector3 masterPosition;
            masterPosition.x = cellScale.x * cellCount.x * chunkIndex.x;
            masterPosition.y = cellScale.y * cellCount.y * chunkIndex.y;
            masterPosition.z = cellScale.z * cellCount.z * chunkIndex.z;
            masterPosition = master.TransformPoint(masterPosition);
            Matrix4x4 chunk2world = Matrix4x4.TRS(masterPosition, master.rotation, master.lossyScale);
            Matrix4x4 world2Master = master.worldToLocalMatrix;

            //POSITION KERNEL
            gpuCellScale[0] = cellScale.x;
            gpuCellScale[1] = cellScale.y;
            gpuCellScale[2] = cellScale.z;
            positionKernel.computer.SetTexture(positionKernel.id, "sdf_Position", rtPosition);
            positionKernel.computer.SetTexture(positionKernel.id, "sdf_NormalDistance", rtNormalDistance); 
            positionKernel.computer.SetFloats("cellScale", gpuCellScale);
            positionKernel.computer.SetMatrix("chunk2World", chunk2world); 
            positionKernel.computer.SetMatrix("world2Master", world2Master);
            positionKernel.computer.SetInt("yBound", cellCount.y + 1);
            positionKernel.computer.SetInt("zBound", cellCount.z + 1);
            positionKernel.computer.Dispatch(
                positionKernel.id,
                cellCount.x + 1,
                cellCount.y + 1,
                cellCount.z + 1);

            foreach( TetraGenShape shape in shapes)
            {
                // SKIPS
                if (!shape || !shape.gameObject.activeSelf)
                    continue;

                // SHAPE BUFFER
                gpuShapeData[0] = shape.ToShapeData();
                shapeBuffer.SetData(gpuShapeData);

                // SHAPE KERNEL
                shape.addShape.computer.SetTexture(shape.addShape.id, "sdf_Position", rtPosition);
                shape.addShape.computer.SetTexture(shape.addShape.id, "sdf_NormalDistance_Blend", rtNormalDistanceBlend);
                shape.addShape.computer.SetBuffer(shape.addShape.id, "shapeBuffer", shapeBuffer);
                shape.addShape.computer.SetMatrix("chunk2World", chunk2world);
                shape.addShape.computer.SetMatrix("world2Master", world2Master);
                shape.addShape.computer.SetInt("yBound", cellCount.y + 1);
                shape.addShape.computer.SetInt("zBound", cellCount.z + 1);
                shape.addShape.computer.Dispatch(
                    shape.addShape.id,
                    cellCount.x + 1,
                    cellCount.y + 1,
                    cellCount.z + 1);

                //BLEND KERNEL
                shape.blendMode.computer.SetTexture(shape.blendMode.id, "sdf_NormalDistance", rtNormalDistance);
                shape.blendMode.computer.SetTexture(shape.blendMode.id, "sdf_NormalDistance_Blend", rtNormalDistanceBlend);
                shape.blendMode.computer.SetBuffer(shape.blendMode.id, "shapeBuffer", shapeBuffer);
                shape.blendMode.computer.SetInt("yBound", cellCount.y + 1);
                shape.blendMode.computer.SetInt("zBound", cellCount.z + 1);
                shape.blendMode.computer.Dispatch(
                    shape.blendMode.id,
                    cellCount.x + 1,
                    cellCount.y + 1,
                    cellCount.z + 1);
            }

            // MESH KERNEL
            meshKernel.computer.SetTexture(meshKernel.id, "sdf_Position", rtPosition);
            meshKernel.computer.SetTexture(meshKernel.id, "sdf_NormalDistance", rtNormalDistance);
            meshKernel.computer.SetBuffer(meshKernel.id, "tBuffer", triangleBuffer);
            meshKernel.computer.SetBuffer(meshKernel.id, "cellTriangleCount", tCountBuffer);
            meshKernel.computer.SetInt("yBound", cellCount.y + 1);
            meshKernel.computer.SetInt("zBound", cellCount.z + 1);
            meshKernel.computer.SetInt("flipNormal", flipNormals ? 1 : -1);
            meshKernel.computer.Dispatch(
                meshKernel.id,
                cellCount.x / 8,
                cellCount.y / 8,
                cellCount.z / 8);
        }

        private static readonly Dictionary<Vector3, int> welder = new Dictionary<Vector3, int>();
        private static readonly Vertex[] vtemp = new Vertex[3];

        /// <summary> Forms meshes out of triangle data generated in SetTriangleBuffer. </summary>
        /// <remarks> Make sure SetTriangleBuffer has ran before executing this method.</remarks>
        /// <param name="chunkMeshes"> a list of pre-existing GameObjects with meshes for reuse to avoid excessive instantiations </param>
        /// <returns> a list of the meshes that were generated </returns>
        public void GenerateMeshes(Transform parent, Vector3 position, Quaternion rotation, List<GameObject> chunkMeshes)
        {
            int tCount = 0;
            int vCount = 0;

            // Get triangle data from GPU.
            triangleBuffer.GetData(gpuTriangleBuffer);
            tCountBuffer.GetData(gpuTCountBuffer);

            welder.Clear();

            for (int c = 0, ce = gpuTCountBuffer.Length; c < ce; c++)
                for (int t = c * 10, te = t + gpuTCountBuffer[c]; t < te; t++)
                {
                    Triangle tri = gpuTriangleBuffer[t];
                    vtemp[0] = new Vertex(tri.a, tri.na);
                    vtemp[1] = new Vertex(tri.b, tri.nb);
                    vtemp[2] = new Vertex(tri.c, tri.nc);

                    //Welder
                    for (int i = 0; i < 3; i++)
                        if (welder.TryGetValue(vtemp[i].position, out int vIndex))
                            meshTriangleBuffer[tCount++] = (ushort)vIndex;
                        else
                        {
                            welder.Add(vtemp[i].position, vCount);
                            meshTriangleBuffer[tCount++] = (ushort)vCount;
                            meshVertexBuffer[vCount++] = vtemp[i];
                        }
                }

            // Clear Unused Objects
            if (tCount == 0 && chunkMeshes.Count != 0)
            {
                chunkMeshes[0].TryGetComponent(out MeshFilter mf);
                chunkMeshes[0].SetActive(false);
                mf.sharedMesh.Clear();
                meshObjectPool.Push(chunkMeshes[0]);
                chunkMeshes.Clear();
            }
            else if (tCount > 0 && chunkMeshes.Count == 0)
                if (meshObjectPool.Count != 0)
                {
                    GameObject m = meshObjectPool.Pop();
                    m.SetActive(true);
                    chunkMeshes.Add(m);
                }
                else
                    chunkMeshes.Add(Instantiate(meshTemplate.gameObject, parent));

            // Mesh Generation
            if(tCount > 0)
            {
                GameObject mo = chunkMeshes[0];
                mo.TryGetComponent(out Transform tr);
                mo.TryGetComponent(out MeshRenderer mr);
                mo.TryGetComponent(out MeshFilter mf);
                mo.TryGetComponent(out MeshCollider mc);
                Mesh mesh = GetEnsuredMesh(mf);
                mesh.SetVertexBufferParams(vCount, Vertex.meshAttributes);
                mesh.SetVertexBufferData(meshVertexBuffer, 0, 0, vCount);
                mesh.SetTriangles(meshTriangleBuffer, 0, tCount, 0, false, 0);
                mesh.bounds = chunkBounds;
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                //mesh.RecalculateNormals();
                tr.SetPositionAndRotation(position, rotation);
                mo.layer = parent.gameObject.layer;
                mf.sharedMesh = mesh;

                if(meshMaterial && mr)
                    mr.sharedMaterial = meshMaterial;

                if (generateCollision && mc && mc.enabled)
                    mc.sharedMesh = mesh;
            }
        }

        private Mesh GetEnsuredMesh(MeshFilter mf)
        { 
            if (mf.sharedMesh)
                return mf.sharedMesh;

            Mesh mesh = new Mesh();

            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            return mesh;
        }

        /// <summary> Frees all memory associated with mesh generation </summary>
        public void GenerationEnd()
        {
            if (generationReady)
            {
                gpuTriangleBuffer = null;
                weightBuffer.Release();
                blendBuffer.Release();
                triangleBuffer.Release();
                tCountBuffer.Release();
                shapeBuffer.Release();
                generationReady = false;
                meshVertexBuffer = null;
                meshTriangleBuffer = null;

                rtPosition.Release();
                DestroyImmediate(rtPosition);
                rtPosition = null;

                rtNormalDistance.Release();
                DestroyImmediate(rtNormalDistance);
                rtNormalDistance = null;

                rtNormalDistanceBlend.Release();
                DestroyImmediate(rtNormalDistanceBlend);
                rtNormalDistanceBlend = null;
            }
        }
    }
}
