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
        public Transform shapeContainer;

        /// <summary> TetraGenChunk handles the mesh generation </summary>
        private TetraGenChunk chunk;
        /// <summary> true when this TetraGenMaster has been initialized for generation </summary>
        private bool readyToGenerate = false;
        /// <summary> Storage of which meshes are associated with which chunk coordinates </summary>
        private readonly Dictionary<Vector3Int, List<GameObject>> meshSets = new Dictionary<Vector3Int, List<GameObject>>();

        private void OnValidate()
        {
            if(generationType.Equals(GenerationType.Static))
                Close();
        }

        private void Awake()
        {
            chunk = GetComponent<TetraGenChunk>();
        }

        private void OnEnable()
        {
            if (!generationType.Equals(GenerationType.Static))
                GenStart();

            if (!shapeContainer)
                shapeContainer = transform;
        }

        private void OnDisable()
        {
            Close();
        }

        private void Update()
        {
            switch (generationType)
            {
                case GenerationType.Static:
                    break;
                case GenerationType.Realtime:
                    RealtimeUpdate(); break;
                case GenerationType.Terrain:
                    FollowTarget(); break;
            }
        }

        
        public void ClearMeshes()
        {
            foreach (Component mesh in GetComponentsInChildren<MeshFilter>())
                DestroyImmediate(mesh.gameObject);

            meshSets.Clear();
        }

        /// <summary>
        /// Static mesh generation. Intended to be called from the inspector.
        /// </summary>
        public void Generate()
        {
            GenStart();
            RealtimeUpdate();
            Close();
        }

        /// <summary>
        /// Initializes memory for mesh generation
        /// </summary>
        private void GenStart()
        {
            ClearMeshes();

            chunk.GenerationStart();

            readyToGenerate = true;
        }

        /// <summary>
        /// Realtime mesh generation method. Updates every chunk every frame.
        /// </summary>
        private void RealtimeUpdate()
        {
            if (!readyToGenerate)
                GenStart();

            TetraGenShape[] shapes = TetraGenShape.GetShapesIn(shapeContainer);

            for (int x = 0; x < chunkCount.x; x++)
                for (int y = 0; y < chunkCount.y; y++)
                    for (int z = 0; z < chunkCount.z; z++)
                    {
                        Vector3Int key = new Vector3Int(x, y, z);

                        if (meshSets.TryGetValue(key, out List<GameObject> currentMeshes))
                            meshSets.Remove(key);

                        Vector3 position = transform.TransformPoint(new Vector3(
                            key.x * chunk.cellScale.x * chunk.cellCount.x,
                            key.y * chunk.cellScale.y * chunk.cellCount.y,
                            key.z * chunk.cellScale.z * chunk.cellCount.z));

                        chunk.SetTriangleBuffer(shapes, transform, key);
                        meshSets.Add(key, chunk.GenerateMeshes(transform, position, transform.rotation, currentMeshes));
                    }
        }

        /// <summary>
        /// Terrain mesh generation method. Updates chunks closest to the camera, one chunk per frame.
        /// Swaps out far chunks for close chunks to maintain a stable amount of geometry.
        /// </summary>
        private void FollowTarget()
        {
            if (!readyToGenerate)
                GenStart();

            Vector3 target;

#if UNITY_EDITOR
            if(UnityEditor.EditorApplication.isPlaying)
                target = transform.InverseTransformPoint(Camera.main.transform.position);
            else
                target = transform.InverseTransformPoint(UnityEditor.SceneView.GetAllSceneCameras()[0].transform.position);
#else
            target = transform.InverseTransformPoint(Camera.main.transform.position);
#endif

            Vector3Int currentCell = new Vector3Int(
                Mathf.FloorToInt(target.x / chunk.cellScale.x / chunk.cellCount.x),
                Mathf.FloorToInt(target.y / chunk.cellScale.y / chunk.cellCount.y),
                Mathf.FloorToInt(target.z / chunk.cellScale.z / chunk.cellCount.z));
            Vector3Int minCell = new Vector3Int(
                currentCell.x - chunkCount.x / 2,
                currentCell.y - chunkCount.y / 2,
                currentCell.z - chunkCount.z / 2);
            Vector3Int maxCell = new Vector3Int(
                currentCell.x + chunkCount.x / 2 + chunkCount.x % 2,
                currentCell.y + chunkCount.y / 2 + chunkCount.y % 2,
                currentCell.z + chunkCount.z / 2 + chunkCount.z % 2);

            // Get closest empty space
            Vector3Int closestEmpty = default;
            float closestDistance = float.MaxValue;
            for (int x = minCell.x; x < maxCell.x; x++)
                for (int y = minCell.y; y < maxCell.y; y++)
                    for (int z = minCell.z; z < maxCell.z; z++)
                    {
                        Vector3Int checkCell = new Vector3Int(x, y, z);
                        float distance = (
                            new Vector3(x, y, z) -
                            new Vector3(currentCell.x, currentCell.y, currentCell.z))
                            .sqrMagnitude;

                        if (distance < closestDistance && !meshSets.ContainsKey(checkCell))
                        {
                            closestEmpty = checkCell;
                            closestDistance = distance;
                        }
                    }

            /* Get farthest loaded mesh set.
             * Only select from out of range chunks */
            List<GameObject> farthestSet = null;
            Vector3Int farthestKey = default;
            float farthestDistance = -1f;
            foreach (var kvp in meshSets)
                if (kvp.Key.x < minCell.x || kvp.Key.x >= maxCell.x
                    || kvp.Key.y < minCell.y || kvp.Key.y >= maxCell.y
                    || kvp.Key.z < minCell.z || kvp.Key.z >= maxCell.z)
                {
                    float distance = (
                        new Vector3(kvp.Key.x, kvp.Key.y, kvp.Key.z) -
                        new Vector3(currentCell.x, currentCell.y, currentCell.z))
                        .sqrMagnitude;

                    if (distance > farthestDistance)
                    {
                        farthestSet = kvp.Value;
                        farthestKey = kvp.Key;
                        farthestDistance = distance;
                    }
                }

            /* Move farthest mesh set to the closest empty space.
               Reuse them to avoid unnecessary instantiations. */
            if (closestDistance != float.MaxValue)
            {
                TetraGenShape[] shapes = TetraGenShape.GetShapesIn(shapeContainer);

                if (farthestSet != null)
                    meshSets.Remove(farthestKey);

                Vector3 position = transform.TransformPoint(new Vector3(
                    closestEmpty.x * chunk.cellScale.x * chunk.cellCount.x,
                    closestEmpty.y * chunk.cellScale.y * chunk.cellCount.y,
                    closestEmpty.z * chunk.cellScale.z * chunk.cellCount.z));

                chunk.SetTriangleBuffer(shapes, transform, closestEmpty);
                meshSets.Add(closestEmpty, chunk.GenerateMeshes(transform, position, transform.rotation, farthestSet));
            }
        } 

        /// <summary>
        /// Frees up all memory involved in mesh generation.
        /// </summary>
        private void Close()
        {
            if (!readyToGenerate)
                return;

            chunk.GenerationEnd();
            meshSets.Clear();
            readyToGenerate = false;
        }
    }
}
