using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TetraGen
{
    /// <summary>
    ///     Main class of the TetraGen system.
    ///     Place anywhere in your scene to act as the origin of an isosurface.
    /// </summary>
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    public class TetraGenMaster : MonoBehaviour
    {
        public enum GenerationType { Static, Realtime, Terrain }

        [Tooltip("What kind of mesh is being created")]
        public GenerationType generationType = GenerationType.Static;
        [Tooltip("Number of chunks to generate per direction")]
        public Vector3Int chunkCount = new Vector3Int(8, 8, 8);
        [Tooltip("The transform that contains the TetraGenShapes that will form the isosurface mesh.")]
        public TGShapeContainer shapeContainer;
#if UNITY_EDITOR
        public bool generateInEditMode = true;
#endif

        /// <summary> TetraGenChunk handles the mesh generation </summary>
        private TetraGenChunk chunk;
        /// <summary> true when this TetraGenMaster has been initialized for generation </summary>
        private bool readyToGenerate = false;
        /// <summary> Storage of which meshes are associated with which chunk coordinates </summary>
        private readonly Dictionary<Vector3Int, ChunkData> activeChunks = new Dictionary<Vector3Int, ChunkData>();
        /// <summary> This is the target followed in terrain mode </summary>
        private Transform followTarget = null;
        /// <summary> Store the order in which chunks are loaded during terrain mode in a LUT </summary>
        private Vector3Int[] chunkOrderLut = null;
        /// <summary> Current position chunkOrderLut is associated with </summary>
        private Vector3Int chunkOrderLutPosition = Vector3Int.zero;
        /// <summary> How many chunks are guaranteed to be loaded for the current position </summary>
        /// <remarks> This is reset when chunkOrderLutPosition changes </remarks>
        private int chunkOrderLutIndex = 0;

        private void OnValidate()
        {
            if(generationType.Equals(GenerationType.Static))
                GenerationEnd();
        }

        private void Awake()
        {
            TryGetComponent(out chunk);
        }

        private void OnEnable()
        {
            if (!generationType.Equals(GenerationType.Static))
                GenStart();
        }

        private void OnDisable()
        {
            GenerationEnd();
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (!generateInEditMode && !UnityEditor.EditorApplication.isPlaying)
                return;
#endif
                switch (generationType)
            {
                case GenerationType.Static:
                    break;
                case GenerationType.Realtime:
                    GenerateRealtime(); break;
                case GenerationType.Terrain:
                    GenerateFollowTarget(); break;
            }
        }
        
        public void ClearMeshes()
        {
            foreach (Component mesh in GetComponentsInChildren<MeshFilter>())
                DestroyImmediate(mesh.gameObject);

            activeChunks.Clear();
        }

        /// <summary>
        /// Static mesh generation. Intended to be called from the inspector.
        /// </summary>
        public void Generate()
        {
            GenStart();
            GenerateRealtime();
            GenerationEnd();
        }

        /// <summary>
        /// Initializes memory for mesh generation
        /// </summary>
        private void GenStart()
        {
            ClearMeshes();

            chunk.GenerationStart();

            readyToGenerate = true;

            if (generationType.Equals(GenerationType.Terrain))
            {
                SetTargetToCamera();

                SortedList<float, Vector3Int> sortedChunks = new SortedList<float, Vector3Int>();
                Vector3Int centerChunk = chunkCount / 2;
                for (int x = 0; x < chunkCount.x; x++)
                    for (int y = 0; y < chunkCount.y; y++)
                        for (int z = 0; z < chunkCount.z; z++)
                        {
                            Vector3Int offset = new Vector3Int(
                                x - centerChunk.x,
                                y - centerChunk.y,
                                z - centerChunk.z);
                            float dist = offset.sqrMagnitude;

                            while (sortedChunks.ContainsKey(dist))
                                dist += 0.0001f;

                            sortedChunks.Add(dist, offset);
                        }
                chunkOrderLut = sortedChunks.Values.ToArray();
                chunkOrderLutIndex = 0;
            }
        }

        /// <summary>
        /// Sets the follow target to the main camera.
        /// </summary>
        public void SetTargetToCamera()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPlaying)
                followTarget = Camera.main.transform;
            else if(UnityEditor.SceneView.GetAllSceneCameras() is var camList && camList.Length > 0)
                followTarget = UnityEditor.SceneView.GetAllSceneCameras()[0].transform;
#else
            followTarget = Camera.main.transform;
#endif
        }

        /// <summary>
        /// Realtime mesh generation method. Updates every chunk every frame.
        /// </summary>
        private void GenerateRealtime()
        {
            if (!readyToGenerate)
                GenStart();

            for (int x = 0; x < chunkCount.x; x++)
                for (int y = 0; y < chunkCount.y; y++)
                    for (int z = 0; z < chunkCount.z; z++)
                    {
                        Vector3Int chunkId = new Vector3Int(x, y, z);
                        Vector3 position = transform.TransformPoint(new Vector3(
                            chunkId.x * chunk.cellScale.x * chunk.cellCount.x,
                            chunkId.y * chunk.cellScale.y * chunk.cellCount.y,
                            chunkId.z * chunk.cellScale.z * chunk.cellCount.z));

                        if (!activeChunks.TryGetValue(chunkId, out ChunkData currentChunk))
                        {
                            currentChunk = new ChunkData();
                            activeChunks.Add(chunkId, currentChunk);
                        }

                        chunk.SetTriangleBuffer(shapeContainer.shapes, transform, chunkId);
                        chunk.GenerateMeshes(transform, position, transform.rotation, currentChunk.meshes);
                    }
        }

        /// <summary>
        /// Terrain mesh generation method. Updates chunks closest to the camera, one chunk per frame.
        /// Swaps out far chunks for close chunks to maintain a stable amount of geometry.
        /// </summary>
        private void GenerateFollowTarget()
        {
            if (!readyToGenerate)
                GenStart();

            Vector3 targetPosition = followTarget ? transform.InverseTransformPoint(followTarget.position) : Vector3.zero;
            Vector3Int targetChunk = new Vector3Int(
                Mathf.FloorToInt(targetPosition.x / chunk.cellScale.x / chunk.cellCount.x),
                Mathf.FloorToInt(targetPosition.y / chunk.cellScale.y / chunk.cellCount.y),
                Mathf.FloorToInt(targetPosition.z / chunk.cellScale.z / chunk.cellCount.z));

            // Closest unloaded in-bounds chunk
            Vector3Int closestEmptyChunkId;
            float closestDistance;

            if (!targetChunk.Equals(chunkOrderLutPosition))
            {
                chunkOrderLutIndex = 0;
                chunkOrderLutPosition = targetChunk;
            }

            while (chunkOrderLutIndex < chunkOrderLut.Length && activeChunks.ContainsKey(targetChunk + chunkOrderLut[chunkOrderLutIndex]))
                chunkOrderLutIndex++;

            if (chunkOrderLutIndex < chunkOrderLut.Length)
            {
                closestEmptyChunkId = targetChunk + chunkOrderLut[chunkOrderLutIndex];
                closestDistance = 0;
            }
            else return;

            // Farthest loaded out-of-bounds chunk
            ChunkData farthestActiveChunk = null;
            Vector3Int farthestActiveChunkId = default;
            float farthestDistance = -1f;
            Vector3Int minCell = new Vector3Int(
                targetChunk.x - chunkCount.x / 2,
                targetChunk.y - chunkCount.y / 2,
                targetChunk.z - chunkCount.z / 2);
            Vector3Int maxCell = new Vector3Int(
                targetChunk.x + chunkCount.x / 2 + chunkCount.x % 2,
                targetChunk.y + chunkCount.y / 2 + chunkCount.y % 2,
                targetChunk.z + chunkCount.z / 2 + chunkCount.z % 2);

            foreach (var kvp in activeChunks)
                if (kvp.Key.x < minCell.x || kvp.Key.x >= maxCell.x
                    || kvp.Key.y < minCell.y || kvp.Key.y >= maxCell.y
                    || kvp.Key.z < minCell.z || kvp.Key.z >= maxCell.z)
                {
                    float distance = (kvp.Key - targetChunk).sqrMagnitude;

                    if (distance > farthestDistance)
                    {
                        farthestActiveChunk = kvp.Value;
                        farthestActiveChunkId = kvp.Key;
                        farthestDistance = distance;
                    }
                }

            // Unload far chunk, Load closest chunk
            if (closestDistance != float.MaxValue)
            {
                Vector3 chunkPosition = transform.TransformPoint(new Vector3(
                    closestEmptyChunkId.x * chunk.cellScale.x * chunk.cellCount.x,
                    closestEmptyChunkId.y * chunk.cellScale.y * chunk.cellCount.y,
                    closestEmptyChunkId.z * chunk.cellScale.z * chunk.cellCount.z));

                if (farthestActiveChunk != null)
                    activeChunks.Remove(farthestActiveChunkId);
                else
                    farthestActiveChunk = new ChunkData();

                chunk.SetTriangleBuffer(shapeContainer.shapes, transform, closestEmptyChunkId);
                chunk.GenerateMeshes(transform, chunkPosition, transform.rotation, farthestActiveChunk.meshes);
                activeChunks.Add(closestEmptyChunkId, farthestActiveChunk);
            }
        } 

        /// <summary>
        /// Frees up all memory involved in mesh generation.
        /// </summary>
        private void GenerationEnd()
        {
            if (!readyToGenerate)
                return;

            chunk.GenerationEnd();
            activeChunks.Clear();
            readyToGenerate = false;
            chunkOrderLut = null;
        }
    }
}
