using System.Collections.Generic;
using UnityEngine;

namespace WorldGeneration
{
    /// <summary>
    /// Manages terrain chunk spawning, despawning, and LOD based on player position.
    /// Uses object pooling for efficient chunk management.
    /// </summary>
    public class ChunkManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private NoiseGenerator noiseGenerator;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private Material terrainMaterial;

        [Header("Chunk Settings")]
        [Tooltip("Size of each chunk in world units. Must match IslandMeshGenerator.chunkSize.")]
        [SerializeField] private int chunkSize = 100;
        [Tooltip("Vertex resolution used when creating chunk meshes. Higher = more detail.")]
        [SerializeField] private int chunkResolution = 128;
        [Tooltip("How many chunks to load from the player in each direction (radial view).")]
        [SerializeField] private int viewDistanceInChunks = 5;
        [Tooltip("Minimum seconds between chunk visibility updates. Increase to reduce update frequency.")]
        [SerializeField] private float updateInterval = 0.5f;

        [Header("LOD Settings")]
        [SerializeField] private int[] lodDistances = { 1, 2, 4, 8 }; // In chunks
        [SerializeField] private bool useLOD = true;

        [Header("Pool Settings")]
        [Tooltip("Initial number of pooled chunk GameObjects. Increase if you expect many chunks to be active.")]
        [SerializeField] private int initialPoolSize = 25;
        [Tooltip("Whether to create MeshColliders for chunks. Disabling can save CPU if not needed.")]
        [SerializeField] private bool generateColliders = true;
        [Header("Generation Throttling")]
        [Tooltip("Maximum number of chunk generations processed per frame to avoid hitches.")]
        [SerializeField] private int maxChunksPerFrame = 2;

        // Active chunks dictionary (coord -> chunk)
        private Dictionary<Vector2Int, IslandMeshGenerator> activeChunks;
        
        // Object pool
        private Queue<IslandMeshGenerator> chunkPool;
        private Transform poolParent;

        // Tracking
        private Vector2Int lastPlayerChunkCoord;
        private float lastUpdateTime;
        private bool isInitialized = false;

        // Chunk coords that need to be loaded
        private HashSet<Vector2Int> chunksToLoad;
        private List<Vector2Int> chunksToUnload;
        // Generation queue to avoid heavy work in a single frame
        private Queue<Vector2Int> generationQueue;
        private HashSet<Vector2Int> queuedChunks;

        private void Awake()
        {
            activeChunks = new Dictionary<Vector2Int, IslandMeshGenerator>();
            chunkPool = new Queue<IslandMeshGenerator>();
            chunksToLoad = new HashSet<Vector2Int>();
            chunksToUnload = new List<Vector2Int>();
            generationQueue = new Queue<Vector2Int>();
            queuedChunks = new HashSet<Vector2Int>();
        }

        private void Start()
        {
            Initialize();
        }

        /// <summary>
        /// Initialize the chunk system.
        /// </summary>
        public void Initialize()
        {
            if (noiseGenerator == null)
            {
                noiseGenerator = FindObjectOfType<NoiseGenerator>();
                if (noiseGenerator == null)
                {
                    Debug.LogError("ChunkManager: NoiseGenerator not found!");
                    return;
                }
            }

            if (playerTransform == null)
            {
                // Try to find player or main camera
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    playerTransform = player.transform;
                }
                else if (Camera.main != null)
                {
                    playerTransform = Camera.main.transform;
                }
                else
                {
                    Debug.LogError("ChunkManager: No player transform assigned!");
                    return;
                }
            }

            // Create pool parent
            poolParent = new GameObject("ChunkPool").transform;
            poolParent.SetParent(transform);

            // Initialize object pool
            InitializePool();

            // Generate initial chunks
            lastPlayerChunkCoord = GetChunkCoordFromWorldPos(playerTransform.position);
            UpdateChunks();

            isInitialized = true;
        }

        private void Update()
        {
            if (!isInitialized || playerTransform == null) return;

            // Throttle updates
            if (Time.time - lastUpdateTime < updateInterval) return;
            lastUpdateTime = Time.time;

            Vector2Int currentChunkCoord = GetChunkCoordFromWorldPos(playerTransform.position);

            // Only update if player moved to a new chunk
            if (currentChunkCoord != lastPlayerChunkCoord)
            {
                lastPlayerChunkCoord = currentChunkCoord;
                UpdateChunks();
            }
            else if (useLOD)
            {
                // Update LOD levels even if in same chunk
                UpdateChunkLODs();
            }

            // Process a small number of queued chunk generations each frame
            int processed = 0;
            while (generationQueue.Count > 0 && processed < maxChunksPerFrame)
            {
                Vector2Int coord = generationQueue.Dequeue();
                queuedChunks.Remove(coord);
                // Double-check not already loaded
                if (!activeChunks.ContainsKey(coord))
                {
                    GenerateAndActivateChunk(coord);
                }
                processed++;
            }
        }

        /// <summary>
        /// Update visible chunks based on player position.
        /// </summary>
        private void UpdateChunks()
        {
            chunksToLoad.Clear();
            chunksToUnload.Clear();

            // Determine which chunks should be visible
            for (int x = -viewDistanceInChunks; x <= viewDistanceInChunks; x++)
            {
                for (int z = -viewDistanceInChunks; z <= viewDistanceInChunks; z++)
                {
                    Vector2Int chunkCoord = new Vector2Int(
                        lastPlayerChunkCoord.x + x,
                        lastPlayerChunkCoord.y + z
                    );

                    // Check if within circular view distance
                    float distance = Mathf.Sqrt(x * x + z * z);
                    if (distance <= viewDistanceInChunks)
                    {
                        chunksToLoad.Add(chunkCoord);
                    }
                }
            }

            // Find chunks to unload
            foreach (var kvp in activeChunks)
            {
                if (!chunksToLoad.Contains(kvp.Key))
                {
                    chunksToUnload.Add(kvp.Key);
                }
            }

            // Unload distant chunks
            foreach (var coord in chunksToUnload)
            {
                UnloadChunk(coord);
            }

            // Load new chunks (enqueue generation to avoid hitches)
            foreach (var coord in chunksToLoad)
            {
                if (!activeChunks.ContainsKey(coord) && !queuedChunks.Contains(coord))
                {
                    EnqueueChunkGeneration(coord);
                }
            }

            // Update LOD levels
            if (useLOD)
            {
                UpdateChunkLODs();
            }
        }

        /// <summary>
        /// Load a chunk at the specified coordinate.
        /// </summary>
        private void LoadChunk(Vector2Int coord)
        {
            // This method is now replaced by queued generation. Keep for backward compatibility.
            EnqueueChunkGeneration(coord);
        }

        private void EnqueueChunkGeneration(Vector2Int coord)
        {
            // Quick check: skip if likely fully ocean
            if (noiseGenerator != null && !noiseGenerator.HasLandInChunk(coord, chunkSize, 6))
            {
                return;
            }

            if (!queuedChunks.Contains(coord) && !activeChunks.ContainsKey(coord))
            {
                generationQueue.Enqueue(coord);
                queuedChunks.Add(coord);
            }
        }

        /// <summary>
        /// Create and activate chunk immediately (used by the generation queue processor).
        /// </summary>
        private void GenerateAndActivateChunk(Vector2Int coord)
        {
            IslandMeshGenerator chunk = GetChunkFromPool();
            if (chunk == null)
            {
                chunk = CreateNewChunk();
            }

            // Position the chunk
            chunk.transform.position = new Vector3(
                coord.x * chunkSize,
                0f,
                coord.y * chunkSize
            );

            // Calculate LOD level
            int lodLevel = useLOD ? CalculateLODLevel(coord) : 0;

            // Initialize the chunk
            chunk.Initialize(noiseGenerator, coord, chunkSize, chunkResolution, lodLevel);
            chunk.gameObject.SetActive(true);

            activeChunks[coord] = chunk;
        }

        /// <summary>
        /// Unload a chunk and return it to the pool.
        /// </summary>
        private void UnloadChunk(Vector2Int coord)
        {
            if (activeChunks.TryGetValue(coord, out IslandMeshGenerator chunk))
            {
                activeChunks.Remove(coord);
                ReturnChunkToPool(chunk);
            }
        }

        /// <summary>
        /// Update LOD levels for all active chunks.
        /// </summary>
        private void UpdateChunkLODs()
        {
            // Guard against any null or destroyed chunk references that may
            // linger in the dictionary (can happen if chunks are returned to
            // the pool or destroyed elsewhere). Collect keys to remove while
            // iterating and update valid chunks' LODs.
            var keysToRemove = new System.Collections.Generic.List<Vector2Int>();
            foreach (var kvp in activeChunks)
            {
                var chunk = kvp.Value;
                if (chunk == null)
                {
                    keysToRemove.Add(kvp.Key);
                    continue;
                }

                int newLod = CalculateLODLevel(kvp.Key);
                chunk.SetLOD(newLod);
            }

            // Remove any null entries discovered during iteration
            if (keysToRemove.Count > 0)
            {
                foreach (var k in keysToRemove)
                {
                    activeChunks.Remove(k);
                }
            }
        }

        /// <summary>
        /// Calculate the LOD level for a chunk based on distance from player.
        /// </summary>
        private int CalculateLODLevel(Vector2Int chunkCoord)
        {
            int dx = chunkCoord.x - lastPlayerChunkCoord.x;
            int dz = chunkCoord.y - lastPlayerChunkCoord.y;
            float distance = Mathf.Sqrt(dx * dx + dz * dz);

            for (int i = 0; i < lodDistances.Length; i++)
            {
                if (distance <= lodDistances[i])
                {
                    return i;
                }
            }

            return lodDistances.Length;
        }

        /// <summary>
        /// Convert world position to chunk coordinate.
        /// </summary>
        private Vector2Int GetChunkCoordFromWorldPos(Vector3 worldPos)
        {
            return new Vector2Int(
                Mathf.FloorToInt(worldPos.x / chunkSize),
                Mathf.FloorToInt(worldPos.z / chunkSize)
            );
        }

        #region Object Pooling

        private void InitializePool()
        {
            for (int i = 0; i < initialPoolSize; i++)
            {
                IslandMeshGenerator chunk = CreateNewChunk();
                chunk.gameObject.SetActive(false);
                chunkPool.Enqueue(chunk);
            }
        }

        private IslandMeshGenerator CreateNewChunk()
        {
            GameObject chunkObj = new GameObject("TerrainChunk");
            chunkObj.transform.SetParent(transform);

            // Add required components
            MeshFilter meshFilter = chunkObj.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = chunkObj.AddComponent<MeshRenderer>();
            meshRenderer.material = terrainMaterial;

            if (generateColliders)
            {
                chunkObj.AddComponent<MeshCollider>();
            }

            IslandMeshGenerator chunk = chunkObj.AddComponent<IslandMeshGenerator>();
            
            return chunk;
        }

        private IslandMeshGenerator GetChunkFromPool()
        {
            if (chunkPool.Count > 0)
            {
                return chunkPool.Dequeue();
            }
            return null;
        }

        private void ReturnChunkToPool(IslandMeshGenerator chunk)
        {
            chunk.gameObject.SetActive(false);
            chunkPool.Enqueue(chunk);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Force regenerate all visible chunks.
        /// </summary>
        public void RegenerateAllChunks()
        {
            foreach (var chunk in activeChunks.Values)
            {
                chunk.GenerateMesh();
            }
        }

        /// <summary>
        /// Change the world seed and regenerate.
        /// </summary>
        public void SetWorldSeed(int newSeed)
        {
            noiseGenerator.SetSeed(newSeed);
            RegenerateAllChunks();
        }

        /// <summary>
        /// Get the terrain height at a world position.
        /// </summary>
        public float GetHeightAtWorldPos(Vector3 worldPos)
        {
            return noiseGenerator.GetHeight(worldPos.x, worldPos.z);
        }

        /// <summary>
        /// Check if a world position is on land.
        /// </summary>
        public bool IsLandAtWorldPos(Vector3 worldPos)
        {
            return noiseGenerator.IsLand(worldPos.x, worldPos.z);
        }

        /// <summary>
        /// Get the chunk at a world position.
        /// </summary>
        public IslandMeshGenerator GetChunkAtWorldPos(Vector3 worldPos)
        {
            Vector2Int coord = GetChunkCoordFromWorldPos(worldPos);
            activeChunks.TryGetValue(coord, out IslandMeshGenerator chunk);
            return chunk;
        }

        /// <summary>
        /// Set view distance in chunks.
        /// </summary>
        public void SetViewDistance(int distanceInChunks)
        {
            viewDistanceInChunks = distanceInChunks;
            UpdateChunks();
        }

        #endregion

        // Public accessors
        public int ActiveChunkCount => activeChunks.Count;
        public int PooledChunkCount => chunkPool.Count;
        public Vector2Int PlayerChunkCoord => lastPlayerChunkCoord;

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;

            // Draw chunk boundaries
            Gizmos.color = Color.yellow;
            foreach (var kvp in activeChunks)
            {
                Vector3 center = new Vector3(
                    kvp.Key.x * chunkSize + chunkSize / 2f,
                    0f,
                    kvp.Key.y * chunkSize + chunkSize / 2f
                );
                Gizmos.DrawWireCube(center, new Vector3(chunkSize, 1f, chunkSize));
            }

            // Draw player chunk
            Gizmos.color = Color.green;
            Vector3 playerChunkCenter = new Vector3(
                lastPlayerChunkCoord.x * chunkSize + chunkSize / 2f,
                0f,
                lastPlayerChunkCoord.y * chunkSize + chunkSize / 2f
            );
            Gizmos.DrawWireCube(playerChunkCenter, new Vector3(chunkSize, 5f, chunkSize));
        }
#endif
    }
}
