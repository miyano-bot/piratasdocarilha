using UnityEngine;
using UnityEngine.Events;

namespace WorldGeneration
{
    /// <summary>
    /// Main controller for the procedural island world generation system.
    /// Coordinates all subsystems: noise, chunks, biomes, and object placement.
    /// </summary>
    public class WorldGenerator : MonoBehaviour
    {
        [Header("World Settings")]
        [SerializeField] private int worldSeed = 42;
        [SerializeField] private bool randomSeedOnStart = false;

        [Header("Subsystem References")]
        [SerializeField] private NoiseGenerator noiseGenerator;
        [SerializeField] private ChunkManager chunkManager;
        [SerializeField] private BiomeManager biomeManager;
        [SerializeField] private ObjectPlacer objectPlacer;

        [Header("Generation Settings")]
        [SerializeField] private bool generateOnStart = true;
        [SerializeField] private bool placeObjectsAutomatically = true;

        [Header("Events")]
        public UnityEvent OnWorldGenerationStarted;
        public UnityEvent OnWorldGenerationCompleted;
        public UnityEvent<Vector2Int> OnChunkGenerated;

        // State tracking
        private bool isGenerating = false;
        private bool isInitialized = false;

        public static WorldGenerator Instance { get; private set; }

        private void Awake()
        {
            // Singleton pattern
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Find or create subsystems
            SetupSubsystems();
        }

        private void Start()
        {
            if (generateOnStart)
            {
                GenerateWorld();
            }
        }

        /// <summary>
        /// Set up all subsystem references.
        /// </summary>
        private void SetupSubsystems()
        {
            // Find existing components or create new ones
            if (noiseGenerator == null)
            {
                noiseGenerator = GetComponentInChildren<NoiseGenerator>();
                if (noiseGenerator == null)
                {
                    GameObject noiseObj = new GameObject("NoiseGenerator");
                    noiseObj.transform.SetParent(transform);
                    noiseGenerator = noiseObj.AddComponent<NoiseGenerator>();
                }
            }

            if (chunkManager == null)
            {
                chunkManager = GetComponentInChildren<ChunkManager>();
                if (chunkManager == null)
                {
                    GameObject chunkObj = new GameObject("ChunkManager");
                    chunkObj.transform.SetParent(transform);
                    chunkManager = chunkObj.AddComponent<ChunkManager>();
                }
            }

            if (biomeManager == null)
            {
                biomeManager = GetComponentInChildren<BiomeManager>();
                if (biomeManager == null)
                {
                    GameObject biomeObj = new GameObject("BiomeManager");
                    biomeObj.transform.SetParent(transform);
                    biomeManager = biomeObj.AddComponent<BiomeManager>();
                }
            }

            if (objectPlacer == null)
            {
                objectPlacer = GetComponentInChildren<ObjectPlacer>();
                if (objectPlacer == null)
                {
                    GameObject placerObj = new GameObject("ObjectPlacer");
                    placerObj.transform.SetParent(transform);
                    objectPlacer = placerObj.AddComponent<ObjectPlacer>();
                }
            }
        }

        /// <summary>
        /// Generate the world with the current seed.
        /// </summary>
        public void GenerateWorld()
        {
            if (isGenerating)
            {
                Debug.LogWarning("WorldGenerator: Already generating!");
                return;
            }

            isGenerating = true;
            OnWorldGenerationStarted?.Invoke();

            // Set random seed if enabled
            if (randomSeedOnStart && !isInitialized)
            {
                worldSeed = System.DateTime.Now.GetHashCode();
            }

            // Initialize noise generator with seed
            noiseGenerator.Initialize(worldSeed);

            // Initialize chunk manager (will generate initial chunks)
            chunkManager.Initialize();

            isInitialized = true;
            isGenerating = false;
            
            OnWorldGenerationCompleted?.Invoke();

            Debug.Log($"WorldGenerator: World generated with seed {worldSeed}");
        }

        /// <summary>
        /// Regenerate the world with a new seed.
        /// </summary>
        public void RegenerateWorld(int newSeed)
        {
            worldSeed = newSeed;
            isInitialized = false;
            
            // Clear existing objects
            if (objectPlacer != null)
            {
                // Object placer will handle clearing
            }

            GenerateWorld();
        }

        /// <summary>
        /// Get terrain height at a world position.
        /// </summary>
        public float GetTerrainHeight(Vector3 worldPosition)
        {
            if (noiseGenerator == null) return 0f;
            return noiseGenerator.GetHeight(worldPosition.x, worldPosition.z);
        }

        /// <summary>
        /// Check if a world position is on land.
        /// </summary>
        public bool IsPositionOnLand(Vector3 worldPosition)
        {
            if (noiseGenerator == null) return false;
            return noiseGenerator.IsLand(worldPosition.x, worldPosition.z);
        }

        /// <summary>
        /// Get the biome at a world position.
        /// </summary>
        public BiomeLayer GetBiomeAtPosition(Vector3 worldPosition)
        {
            if (biomeManager == null) return null;
            return biomeManager.GetBiomeAtPosition(worldPosition.x, worldPosition.z);
        }

        /// <summary>
        /// Snap an object to the terrain surface.
        /// </summary>
        public void SnapToTerrain(Transform objectTransform, float heightOffset = 0f)
        {
            float terrainHeight = GetTerrainHeight(objectTransform.position);
            Vector3 pos = objectTransform.position;
            pos.y = terrainHeight + heightOffset;
            objectTransform.position = pos;
        }

        /// <summary>
        /// Find the nearest land position from a given point.
        /// </summary>
        public Vector3 FindNearestLandPosition(Vector3 fromPosition, float searchRadius = 100f, float stepSize = 5f)
        {
            if (IsPositionOnLand(fromPosition))
            {
                return new Vector3(
                    fromPosition.x,
                    GetTerrainHeight(fromPosition),
                    fromPosition.z
                );
            }

            // Spiral search pattern
            float currentRadius = stepSize;
            int segments = 8;

            while (currentRadius <= searchRadius)
            {
                for (int i = 0; i < segments; i++)
                {
                    float angle = (float)i / segments * Mathf.PI * 2f;
                    Vector3 checkPos = fromPosition + new Vector3(
                        Mathf.Cos(angle) * currentRadius,
                        0f,
                        Mathf.Sin(angle) * currentRadius
                    );

                    if (IsPositionOnLand(checkPos))
                    {
                        return new Vector3(
                            checkPos.x,
                            GetTerrainHeight(checkPos),
                            checkPos.z
                        );
                    }
                }

                currentRadius += stepSize;
                segments += 4; // More samples at larger radii
            }

            Debug.LogWarning("WorldGenerator: No land found within search radius!");
            return fromPosition;
        }

        /// <summary>
        /// Get a random position on land.
        /// </summary>
        public Vector3 GetRandomLandPosition(float searchAreaSize = 1000f, int maxAttempts = 100)
        {
            for (int i = 0; i < maxAttempts; i++)
            {
                Vector3 randomPos = new Vector3(
                    Random.Range(-searchAreaSize, searchAreaSize),
                    0f,
                    Random.Range(-searchAreaSize, searchAreaSize)
                );

                if (IsPositionOnLand(randomPos))
                {
                    return new Vector3(
                        randomPos.x,
                        GetTerrainHeight(randomPos),
                        randomPos.z
                    );
                }
            }

            Debug.LogWarning("WorldGenerator: Could not find random land position!");
            return Vector3.zero;
        }

        // Public accessors
        public int WorldSeed => worldSeed;
        public bool IsGenerating => isGenerating;
        public bool IsInitialized => isInitialized;
        public NoiseGenerator Noise => noiseGenerator;
        public ChunkManager Chunks => chunkManager;
        public BiomeManager Biomes => biomeManager;
        public ObjectPlacer Objects => objectPlacer;

        public void SetSeed(int seed)
        {
            worldSeed = seed;
        }

#if UNITY_EDITOR
        [ContextMenu("Generate World")]
        private void EditorGenerateWorld()
        {
            SetupSubsystems();
            GenerateWorld();
        }

        [ContextMenu("Generate With Random Seed")]
        private void EditorGenerateRandomWorld()
        {
            worldSeed = Random.Range(0, int.MaxValue);
            SetupSubsystems();
            GenerateWorld();
        }

        private void OnValidate()
        {
            if (Application.isPlaying && isInitialized)
            {
                // Could regenerate on inspector changes
            }
        }
#endif
    }
}
