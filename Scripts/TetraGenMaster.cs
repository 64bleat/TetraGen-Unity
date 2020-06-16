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
        [Tooltip("The size of one cell in a chunk.")]
        public Vector3 cellScale = new Vector3(1, 1, 1);   
        [Tooltip("Number of cells per chunk for each dimension.")]
        public IntVector cellCount;
        [Tooltip("Maximum number of chunks to generate for each dimension.")]
        public IntVector chunkCount;
        public bool flipNormals = false;
        public bool generateCollision = true;
        public bool realtime = false;
        public int realtimeChunkUpdateCount = 1;
        public TetraGenChunk chunkTemplate;

        private int chunkUpdate = 0;
        private bool realtimeReady = false;
        private readonly List<TetraGenChunk> loadedChunks = new List<TetraGenChunk>();

        private void OnValidate()
        {
            if (!realtime && realtimeReady)
                FullChunkEnd();
        }

        private void OnEnable()
        {
            if (realtime)
                FullChunkStart();
        }

        private void OnDisable()
        {
            if (realtime)
                FullChunkEnd();
        }

        private void Update()
        {
            if (realtime)
                FullChunkUpdate();
        }

        public void Generate()
        {
            FullChunkStart();
            FullChunkUpdate();
            FullChunkEnd();
        }

        private void FullChunkStart()
        {
            // Destory pre-existing chunks
            foreach(Component chunk in GetComponentsInChildren<TetraGenChunk>())
                DestroyImmediate(chunk.gameObject);

            // Clear chunk dictionary.
            loadedChunks.Clear();

            // Pass shape data to every chunk get them ready for generation
            for (int x = 0; x < chunkCount.x; x++)
                for (int y = 0; y < chunkCount.y; y++)
                    for (int z = 0; z < chunkCount.x; z++)
                    {
                        TetraGenChunk tetraChunk = Instantiate(chunkTemplate.gameObject, transform).GetComponent<TetraGenChunk>();

                        tetraChunk.transform.localPosition = new Vector3(
                            x * cellCount.x * cellScale.x,
                            y * cellCount.y * cellScale.y,
                            z * cellCount.z * cellScale.z);
                        tetraChunk.GenerationStart();

                        loadedChunks.Add(tetraChunk);
                    }

            // Initialized and Ready
            realtimeReady = true;
        }

        private void FullChunkUpdate()
        {
            if (!realtimeReady)
                FullChunkStart();

            ShapeData[] shapeData = (from shape in GetComponentsInChildren<TetraGenShape>()
                                     where shape.gameObject.activeSelf
                                     select shape.Shape).ToArray();

            //if(realtime)
            //    for (int i = 0; i < updateChunksPerFrame; i++, chunkUpdate = ++chunkUpdate % loadedChunks.Count)
            //        loadedChunks[chunkUpdate].GenerationUpdateNewShapeData(shapeData, generateCollision);
            //else
                foreach (TetraGenChunk chunk in loadedChunks)
                    chunk.GenerationUpdateNewShapeData(shapeData, generateCollision);
        }

        private void FullChunkEnd()
        {
            if (realtimeReady)
            {
                foreach (TetraGenChunk chunk in loadedChunks)
                    chunk.GenerationEnd();

                realtimeReady = false;
            }
        }
    }
}
