using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using System.Runtime.InteropServices;

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
        private Triangle[] triangleData;
        /// <summary> stores data retrieved from tCountBuffer </summary>
        private int[] tCountData;
        /// <summary> Stores data compiled from triangleData </summary>
        private Vertex[] vertexData;
        /// <summary> stores a generic triangle array that gets added to meshes </summary>
        private ushort[] triangleMap;
        /// <summary> single structs still have to be passed to the GPU as a struct array </summary>
        private readonly ShapeData[] gpuShapeData = new ShapeData[1];
        /// <summary> a single Vector3 must be passed to the GPU as a float array </summary>
        private readonly float[] gpuCellScale = new float[3];
        /// <summary> calculate chunk bounds in GenerationStart</summary>
        private Bounds chunkBounds;
        /// <summary> Store discarded mesh prefab instances in a pool rather than destroying them</summary>
        private readonly Stack<GameObject> meshObjectPool = new Stack<GameObject>();

        private void OnDestroy()
        {
            GenerationEnd();
        }

        /// <summary> Initializes the chunk for mesh generation </summary>
        public void GenerationStart()
        {
            int cellDataStride = sizeof(float) * 5;
            int cellDataLength = cellCount.x * cellCount.y * cellCount.z;
            int triangleDataLength = cellDataLength * meshKernel.trianglesPerCell;
            int weightBufferLength = (cellCount.x + 1) * (cellCount.y + 1) * (cellCount.z + 1);
            Vector3 chunkBoundSize;

            chunkBoundSize.x = cellCount.x * cellScale.x;
            chunkBoundSize.y = cellCount.y * cellScale.y;
            chunkBoundSize.z = cellCount.z * cellScale.z;

            chunkBounds = new Bounds(chunkBoundSize / 2, chunkBoundSize);
            triangleData = new Triangle[triangleDataLength];
            tCountData = new int[cellDataLength];
            vertexData = new Vertex[verticesPerMesh];
            triangleMap = new ushort[verticesPerMesh];

            for (int t = 0; t < triangleMap.Length; t++)
                triangleMap[t] = (ushort)t;

            weightBuffer = new ComputeBuffer(weightBufferLength, cellDataStride);
            blendBuffer = new ComputeBuffer(weightBufferLength, cellDataStride);
            triangleBuffer = new ComputeBuffer(triangleDataLength, Triangle.stride);
            tCountBuffer = new ComputeBuffer(cellDataLength, sizeof(int));
            shapeBuffer = new ComputeBuffer(1, ShapeData.stride);

            meshKernel.Init();
            positionKernel.Init();

            generationReady = true;
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
            positionKernel.computer.SetFloats("cellScale", gpuCellScale);
            positionKernel.computer.SetBuffer(positionKernel.id, "weightBuffer", weightBuffer);
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
                shape.addShape.computer.SetBuffer(shape.addShape.id, "shapeBuffer", shapeBuffer);
                shape.addShape.computer.SetBuffer(shape.addShape.id, "blendBuffer", blendBuffer);
                shape.addShape.computer.SetBuffer(shape.addShape.id, "weightBuffer", weightBuffer);
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
                shape.blendMode.computer.SetBuffer(shape.blendMode.id, "shapeBuffer", shapeBuffer);
                shape.blendMode.computer.SetBuffer(shape.blendMode.id, "blendBuffer", blendBuffer);
                shape.blendMode.computer.SetBuffer(shape.blendMode.id, "weightBuffer", weightBuffer);
                shape.blendMode.computer.SetInt("yBound", cellCount.y + 1);
                shape.blendMode.computer.SetInt("zBound", cellCount.z + 1);
                shape.blendMode.computer.Dispatch(
                    shape.blendMode.id,
                    cellCount.x + 1,
                    cellCount.y + 1,
                    cellCount.z + 1);
            }

            // MESH KERNEL
            meshKernel.computer.SetBuffer(meshKernel.id, "weightBuffer", weightBuffer);
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

        /// <summary> Forms meshes out of triangle data generated in SetTriangleBuffer. </summary>
        /// <remarks> Make sure SetTriangleBuffer has ran before executing this method.</remarks>
        /// <param name="chunkMeshes"> a list of pre-existing GameObjects with meshes for reuse to avoid excessive instantiations </param>
        /// <returns> a list of the meshes that were generated </returns>
        public void GenerateMeshes(Transform parent, Vector3 position, Quaternion rotation, List<GameObject> chunkMeshes)
        {
            int tCount = 0;
            int meshCount;

            // Get triangle data from GPU.
            triangleBuffer.GetData(triangleData);
            tCountBuffer.GetData(tCountData);

            // Collapse Buffer and count triangles
            for (int c = 0, ce = tCountData.Length; c < ce; c++)
                for (int t = c * 10, te =  t + tCountData[c]; t < te; t++)
                    triangleData[tCount++] = triangleData[t];

            // Prepare mesh list
            meshCount = Mathf.CeilToInt((float)tCount / trianglesPerMesh);

            // Trim excess mesh GameObjects
            for (int i = meshCount; i < chunkMeshes.Count; i++)
            {
                chunkMeshes[i].TryGetComponent(out MeshFilter mf);
                chunkMeshes[i].SetActive(false);
                mf.sharedMesh.Clear();
                meshObjectPool.Push(chunkMeshes[i]);
            }

            // Add or remove unused indeces
            if (chunkMeshes.Count > meshCount)
                chunkMeshes.RemoveRange(meshCount, chunkMeshes.Count - meshCount);
            else
                for (int i = meshCount - chunkMeshes.Count; i > 0; i--)
                    if (meshObjectPool.Count != 0)
                    {
                        GameObject m = meshObjectPool.Pop();
                        m.SetActive(true);
                        chunkMeshes.Add(m);
                    }
                    else
                        chunkMeshes.Add(Instantiate(meshTemplate.gameObject, parent));

            // Mesh Generation
            for (int m = 0; m < meshCount; m++)
            {
                GameObject mo = chunkMeshes[m];
                mo.TryGetComponent(out Transform tr);
                mo.TryGetComponent(out MeshRenderer mr);
                mo.TryGetComponent(out MeshFilter mf);
                mo.TryGetComponent(out MeshCollider mc);
                Mesh mesh = mf.sharedMesh ? mf.sharedMesh : new Mesh();
                int tStart = m * trianglesPerMesh;
                int tEnd = Mathf.Min(tCount, tStart + trianglesPerMesh);
                int vCount = (tEnd - tStart) * 3;

                // Disperse chunk mesh data into individual meshes   
                for (int t = tStart, v = 0; t < tEnd; t++)
                {
                    vertexData[v  ].position  = triangleData[t].a;
                    vertexData[v++].normal = triangleData[t].n;
                    vertexData[v  ].position  = triangleData[t].b;
                    vertexData[v++].normal = triangleData[t].n;
                    vertexData[v  ].position  = triangleData[t].c;
                    vertexData[v++].normal = triangleData[t].n;
                }
                
                mesh.SetVertexBufferParams(vCount, Vertex.meshAttributes);
                mesh.SetVertexBufferData(vertexData, 0, 0, vCount);
                mesh.SetTriangles(triangleMap, 0, vCount, 0, false, 0);
                mesh.bounds = chunkBounds;
                tr.SetPositionAndRotation(position, rotation);
                mo.layer = parent.gameObject.layer;
                mf.sharedMesh = mesh;

                if(meshMaterial && mr)
                    mr.sharedMaterial = meshMaterial;

                if (generateCollision && mc && mc.enabled)
                    mc.sharedMesh = mesh;
            }
        }

        /// <summary> Frees all memory associated with mesh generation </summary>
        public void GenerationEnd()
        {
            if (generationReady)
            {
                triangleData = null;
                weightBuffer.Release();
                blendBuffer.Release();
                triangleBuffer.Release();
                tCountBuffer.Release();
                shapeBuffer.Release();
                generationReady = false;
                vertexData = null;
                triangleMap = null;
            }
        }
    }
}
