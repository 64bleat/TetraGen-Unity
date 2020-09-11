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

        //private TetraGenMaster master;
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
        private readonly ShapeData[] gpuIn_shapeData = new ShapeData[1];
        /// <summary> a single Vector3 must be passed to the GPU as a float array </summary>
        private readonly float[] gpuIn_cellScale = new float[3];
        /// <summary> calculate chunk bounds in GenerationStart</summary>
        private Bounds bounds;

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
            Vector3 chunkBounds;

            chunkBounds.x = cellCount.x * cellScale.x;
            chunkBounds.y = cellCount.x * cellScale.x;
            chunkBounds.z = cellCount.x * cellScale.x;
            bounds = new Bounds(chunkBounds / 2, chunkBounds);

            triangleData = new Triangle[triangleDataLength];
            tCountData = new int[cellDataLength];
            vertexData = new Vertex[verticesPerMesh];
            triangleMap = new ushort[verticesPerMesh];

            for (int t = 0; t < verticesPerMesh; t++)
                triangleMap[t] = (ushort)t;

            weightBuffer = new ComputeBuffer(weightBufferLength, sizeof(float) * 5);
            blendBuffer = new ComputeBuffer(weightBufferLength, sizeof(float) * 5);
            triangleBuffer = new ComputeBuffer(triangleDataLength, Triangle.stride);
            tCountBuffer = new ComputeBuffer(cellDataLength, sizeof(int));
            shapeBuffer = new ComputeBuffer(1, ShapeData.stride);

            meshKernel.Init();
            positionKernel.Init();

            generationReady = true;
        }

        private readonly Queue<TetraGenShape> shapeQueue = new Queue<TetraGenShape>();
        /// <summary> Pupulates the triangleData array for mesh generation. </summary>
        /// <param name="shapes"> shape data passed to the GPU to form the signed distance field </param>
        /// <param name="chunk2world"> transforms chunk space into world space on the GPU</param>
        public void SetTriangleBuffer(TetraGenShape[] shapes, Transform master, Vector3Int chunkIndex)
        {
            if (!generationReady)
                GenerationStart();

            Vector3 chunkCenter;
            chunkCenter.x = chunkIndex.x * cellScale.x;
            chunkCenter.y = chunkIndex.y * cellScale.y;
            chunkCenter.z = chunkIndex.z * cellScale.z;

            for (int i = 0, ie = shapes.Length; i < ie; i++)
                //if (shapes[i].activeRadius <= 0 || Vector3.Distance(shapes[i].transform.position, chunkCenter) <= shapes[i].activeRadius)
                    shapeQueue.Enqueue(shapes[i]);

            Vector3 masterPosition;
            masterPosition.x = cellScale.x * cellCount.x * chunkIndex.x;
            masterPosition.y = cellScale.y * cellCount.y * chunkIndex.y;
            masterPosition.z = cellScale.z * cellCount.z * chunkIndex.z;
            masterPosition = master.TransformPoint(masterPosition);

            Matrix4x4 chunk2world = Matrix4x4.TRS(masterPosition, master.rotation, master.lossyScale);
            Matrix4x4 world2Master = master.worldToLocalMatrix;

            //POSITION KERNEL
            gpuIn_cellScale[0] = cellScale.x;
            gpuIn_cellScale[1] = cellScale.y;
            gpuIn_cellScale[2] = cellScale.z;
            positionKernel.computer.SetFloats("cellScale", gpuIn_cellScale);
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

            //for (int i = 0, ie = shapes.Length; i < ie; i++)
            foreach( TetraGenShape shape in shapeQueue)
            {
                // SHAPE BUFFER
                gpuIn_shapeData[0] = shape.ToShapeData();
                shapeBuffer.SetData(gpuIn_shapeData);

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

            shapeQueue.Clear();

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
        /// <param name="meshSet"> a list of pre-existing GameObjects with meshes for reuse to avoid excessive instantiations </param>
        /// <returns> a list of the meshes that were generated </returns>
        public List<GameObject> GenerateMeshes(Transform parent, Vector3 position, Quaternion rotation, List<GameObject> meshSet = null)
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
            meshSet = meshSet ?? new List<GameObject>(meshCount);

            for (int i = meshCount; i < meshSet.Count; i++)
                DestroyImmediate(meshSet[i]);

            if (meshSet.Count > meshCount)
                meshSet.RemoveRange(meshCount, meshSet.Count - meshCount);

            for (int i = meshCount - meshSet.Count; i > 0; i--)
                meshSet.Add(Instantiate(meshTemplate.gameObject, parent));

            // Mesh Generation
            for (int m = 0; m < meshCount; m++)
            {
                GameObject mo = meshSet[m];
                MeshRenderer mr = mo.GetComponent<MeshRenderer>();
                MeshFilter mf = mo.GetComponent<MeshFilter>();
                MeshCollider mc = mo.GetComponent<MeshCollider>();
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
                mesh.bounds = bounds;
                mesh.name = mo.name = "Generated Mesh " + m;
                mo.transform.position = position;
                mo.transform.rotation = rotation;
                mo.layer = parent.gameObject.layer;
                mf.sharedMesh = mesh;

                if(meshMaterial && mr)
                    mr.sharedMaterial = meshMaterial;

                if (generateCollision && mc && mc.enabled)
                    mc.sharedMesh = mesh;
            }

            return meshSet;
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

        /*//
        private void OnDrawGizmosSelected()
        {
            Transform cam = Camera.current.transform;
            TetraGenMaster master = GetComponentInParent<TetraGenMaster>();

            for (int x = 0; x < master.cellCount.x; x++)
                for (int y = 0; y < master.cellCount.y; y++)
                    for (int z = 0; z < master.cellCount.z; z++)
                    {
                        Vector3 localPos = new Vector3(
                            x * master.cellScale.x,
                            y * master.cellScale.y,
                            z * master.cellScale.z);
                        Vector3 worldPos = transform.TransformPoint(localPos);
                        float camDistance = Vector3.Distance(worldPos, cam.position);
                        float distFactor = InvLerp(0, 4, camDistance);
                        float angle = Vector3.Angle(cam.forward, worldPos - cam.position);

                        if (angle < 45 && camDistance < 4 && camDistance > 0)
                        {
                            Gizmos.color = new Color(1, 0, 0, distFactor);
                            Gizmos.DrawLine(worldPos, transform.TransformPoint(localPos + new Vector3(master.cellScale.x, 0, 0)));
                            Gizmos.color = new Color(0, 1, 0, distFactor);
                            Gizmos.DrawLine(worldPos, transform.TransformPoint(localPos + new Vector3(0, master.cellScale.y, 0)));
                            Gizmos.color = new Color(0, 0, 1, distFactor);
                            Gizmos.DrawLine(worldPos, transform.TransformPoint(localPos + new Vector3(0, 0, master.cellScale.z)));
                            Gizmos.color = new Color(x % 2, y % 2, z % 2, distFactor);
                            Gizmos.DrawCube(worldPos + transform.TransformVector(master.cellScale) * 0.05f, master.cellScale * 0.1f);
                        }
                    }
        }
        //*/
    }
}
