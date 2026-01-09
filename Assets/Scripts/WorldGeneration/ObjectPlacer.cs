using System.Collections.Generic;
using UnityEngine;

namespace WorldGeneration
{
    /// <summary>
    /// Defines a placeable object type with its spawning rules.
    /// </summary>
    [System.Serializable]
    public class PlaceableObject
    {
        public string name = "Object";
        public GameObject[] prefabVariants;
        
        [Header("Placement Rules")]
        [Range(0f, 1f)]
        public float minHeight = 0.05f;
        [Range(0f, 1f)]
        public float maxHeight = 0.5f;
        
        [Range(0f, 1f)]
        public float maxSlope = 0.3f;
        
        public float density = 0.5f; // Objects per unit area
        public float minSpacing = 2f; // Minimum distance between objects
        
        [Header("Transform Variation")]
        public Vector2 scaleRange = new Vector2(0.8f, 1.2f);
        public bool randomRotationY = true;
        public float maxTiltAngle = 5f;
        
        [Header("Biome Restrictions")]
        public string[] allowedBiomes; // Empty = all biomes
    }

    /// <summary>
    /// Handles placement of objects (trees, rocks, etc.) on terrain using
    /// Poisson disk sampling for natural distribution.
    /// </summary>
    public class ObjectPlacer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private NoiseGenerator noiseGenerator;
        [SerializeField] private BiomeManager biomeManager;
        [SerializeField] private ChunkManager chunkManager;

        [Header("Object Types")]
        [SerializeField] private PlaceableObject[] placeableObjects;

        [Header("Placement Settings")]
        [SerializeField] private int poissonSampleAttempts = 30;
        [SerializeField] private int maxObjectsPerChunk = 500;
        [SerializeField] private bool useObjectPooling = true;
        [SerializeField] private int poolSizePerType = 100;

        // Object pools
        private Dictionary<string, Queue<GameObject>> objectPools;
        private Dictionary<Vector2Int, List<GameObject>> chunkObjects;
        
        // Parent transform for spawned objects
        private Transform objectParent;

        private void Awake()
        {
            objectPools = new Dictionary<string, Queue<GameObject>>();
            chunkObjects = new Dictionary<Vector2Int, List<GameObject>>();
            
            objectParent = new GameObject("PlacedObjects").transform;
            objectParent.SetParent(transform);

            if (useObjectPooling)
            {
                InitializePools();
            }
        }

        private void Start()
        {
            if (noiseGenerator == null)
            {
                noiseGenerator = FindObjectOfType<NoiseGenerator>();
            }
            if (biomeManager == null)
            {
                biomeManager = FindObjectOfType<BiomeManager>();
            }
            if (chunkManager == null)
            {
                chunkManager = FindObjectOfType<ChunkManager>();
            }
        }

        /// <summary>
        /// Initialize object pools for each placeable type.
        /// </summary>
        private void InitializePools()
        {
            foreach (var objType in placeableObjects)
            {
                if (objType.prefabVariants == null || objType.prefabVariants.Length == 0) continue;

                Queue<GameObject> pool = new Queue<GameObject>();
                
                for (int i = 0; i < poolSizePerType; i++)
                {
                    int variantIndex = i % objType.prefabVariants.Length;
                    if (objType.prefabVariants[variantIndex] == null) continue;

                    GameObject obj = Instantiate(objType.prefabVariants[variantIndex], objectParent);
                    obj.SetActive(false);
                    pool.Enqueue(obj);
                }

                objectPools[objType.name] = pool;
            }
        }

        /// <summary>
        /// Place objects on a chunk using Poisson disk sampling.
        /// </summary>
        public void PlaceObjectsOnChunk(Vector2Int chunkCoord, int chunkSize)
        {
            if (chunkObjects.ContainsKey(chunkCoord))
            {
                // Already populated
                return;
            }

            List<GameObject> placedObjects = new List<GameObject>();
            float worldOffsetX = chunkCoord.x * chunkSize;
            float worldOffsetZ = chunkCoord.y * chunkSize;

            foreach (var objType in placeableObjects)
            {
                if (objType.prefabVariants == null || objType.prefabVariants.Length == 0) continue;

                // Generate Poisson disk sample points
                List<Vector2> points = GeneratePoissonPoints(
                    chunkSize,
                    chunkSize,
                    objType.minSpacing,
                    objType.density
                );

                foreach (var point in points)
                {
                    if (placedObjects.Count >= maxObjectsPerChunk) break;

                    float worldX = worldOffsetX + point.x;
                    float worldZ = worldOffsetZ + point.y;

                    // Check placement validity
                    if (!CanPlaceObject(objType, worldX, worldZ)) continue;

                    // Get height at position
                    float height = noiseGenerator.GetHeight(worldX, worldZ);
                    
                    // Place object
                    GameObject obj = SpawnObject(objType, worldX, height, worldZ);
                    if (obj != null)
                    {
                        placedObjects.Add(obj);
                    }
                }
            }

            chunkObjects[chunkCoord] = placedObjects;
        }

        /// <summary>
        /// Remove all objects from a chunk.
        /// </summary>
        public void ClearObjectsFromChunk(Vector2Int chunkCoord)
        {
            if (!chunkObjects.TryGetValue(chunkCoord, out List<GameObject> objects)) return;

            foreach (var obj in objects)
            {
                if (obj == null) continue;

                if (useObjectPooling)
                {
                    obj.SetActive(false);
                    // Return to appropriate pool (simplified - returns to first matching pool)
                    foreach (var pool in objectPools.Values)
                    {
                        pool.Enqueue(obj);
                        break;
                    }
                }
                else
                {
                    Destroy(obj);
                }
            }

            chunkObjects.Remove(chunkCoord);
        }

        /// <summary>
        /// Check if an object can be placed at a position.
        /// </summary>
        private bool CanPlaceObject(PlaceableObject objType, float worldX, float worldZ)
        {
            // Check if on land
            if (!noiseGenerator.IsLand(worldX, worldZ)) return false;

            // Check height constraints
            float normalizedHeight = noiseGenerator.GetNormalizedHeight(worldX, worldZ);
            if (normalizedHeight < objType.minHeight || normalizedHeight > objType.maxHeight) return false;

            // Check slope constraints
            float slope = noiseGenerator.GetSlope(worldX, worldZ);
            if (slope > objType.maxSlope) return false;

            // Check biome constraints
            if (objType.allowedBiomes != null && objType.allowedBiomes.Length > 0 && biomeManager != null)
            {
                BiomeLayer currentBiome = biomeManager.GetBiomeAtPosition(worldX, worldZ);
                if (currentBiome != null)
                {
                    bool biomeAllowed = false;
                    foreach (var biomeName in objType.allowedBiomes)
                    {
                        if (currentBiome.name == biomeName)
                        {
                            biomeAllowed = true;
                            break;
                        }
                    }
                    if (!biomeAllowed) return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Spawn an object at the specified position.
        /// </summary>
        private GameObject SpawnObject(PlaceableObject objType, float worldX, float height, float worldZ)
        {
            GameObject obj = null;

            if (useObjectPooling && objectPools.TryGetValue(objType.name, out Queue<GameObject> pool) && pool.Count > 0)
            {
                obj = pool.Dequeue();
                obj.SetActive(true);
            }
            else if (objType.prefabVariants.Length > 0)
            {
                // Pick random variant
                int variantIndex = Random.Range(0, objType.prefabVariants.Length);
                if (objType.prefabVariants[variantIndex] != null)
                {
                    obj = Instantiate(objType.prefabVariants[variantIndex], objectParent);
                }
            }

            if (obj == null) return null;

            // Set position
            obj.transform.position = new Vector3(worldX, height, worldZ);

            // Apply random scale
            float scale = Random.Range(objType.scaleRange.x, objType.scaleRange.y);
            obj.transform.localScale = Vector3.one * scale;

            // Apply random rotation
            float rotY = objType.randomRotationY ? Random.Range(0f, 360f) : 0f;
            float tiltX = Random.Range(-objType.maxTiltAngle, objType.maxTiltAngle);
            float tiltZ = Random.Range(-objType.maxTiltAngle, objType.maxTiltAngle);
            obj.transform.rotation = Quaternion.Euler(tiltX, rotY, tiltZ);

            return obj;
        }

        /// <summary>
        /// Generate Poisson disk sample points for natural distribution.
        /// </summary>
        private List<Vector2> GeneratePoissonPoints(float width, float height, float minDist, float density)
        {
            List<Vector2> points = new List<Vector2>();
            List<Vector2> activeList = new List<Vector2>();

            // Adjust sample count based on density
            float cellSize = minDist / Mathf.Sqrt(2);
            int gridWidth = Mathf.CeilToInt(width / cellSize);
            int gridHeight = Mathf.CeilToInt(height / cellSize);
            
            Vector2?[,] grid = new Vector2?[gridWidth, gridHeight];

            // Start with a random point
            Vector2 firstPoint = new Vector2(
                Random.Range(0f, width),
                Random.Range(0f, height)
            );
            points.Add(firstPoint);
            activeList.Add(firstPoint);

            int gridX = Mathf.FloorToInt(firstPoint.x / cellSize);
            int gridY = Mathf.FloorToInt(firstPoint.y / cellSize);
            if (gridX >= 0 && gridX < gridWidth && gridY >= 0 && gridY < gridHeight)
            {
                grid[gridX, gridY] = firstPoint;
            }

            // Generate points
            int maxPoints = Mathf.FloorToInt(width * height * density);
            
            while (activeList.Count > 0 && points.Count < maxPoints)
            {
                int randomIndex = Random.Range(0, activeList.Count);
                Vector2 currentPoint = activeList[randomIndex];

                bool foundValidPoint = false;

                for (int i = 0; i < poissonSampleAttempts; i++)
                {
                    // Generate random point in annulus
                    float angle = Random.Range(0f, Mathf.PI * 2f);
                    float distance = Random.Range(minDist, minDist * 2f);
                    
                    Vector2 newPoint = new Vector2(
                        currentPoint.x + Mathf.Cos(angle) * distance,
                        currentPoint.y + Mathf.Sin(angle) * distance
                    );

                    // Check bounds
                    if (newPoint.x < 0 || newPoint.x >= width ||
                        newPoint.y < 0 || newPoint.y >= height)
                    {
                        continue;
                    }

                    // Check neighbors
                    if (IsValidPoint(newPoint, grid, gridWidth, gridHeight, cellSize, minDist))
                    {
                        points.Add(newPoint);
                        activeList.Add(newPoint);

                        int newGridX = Mathf.FloorToInt(newPoint.x / cellSize);
                        int newGridY = Mathf.FloorToInt(newPoint.y / cellSize);
                        if (newGridX >= 0 && newGridX < gridWidth && newGridY >= 0 && newGridY < gridHeight)
                        {
                            grid[newGridX, newGridY] = newPoint;
                        }

                        foundValidPoint = true;
                        break;
                    }
                }

                if (!foundValidPoint)
                {
                    activeList.RemoveAt(randomIndex);
                }
            }

            return points;
        }

        /// <summary>
        /// Check if a point is valid (far enough from existing points).
        /// </summary>
        private bool IsValidPoint(Vector2 point, Vector2?[,] grid, int gridWidth, int gridHeight, float cellSize, float minDist)
        {
            int gridX = Mathf.FloorToInt(point.x / cellSize);
            int gridY = Mathf.FloorToInt(point.y / cellSize);

            // Check 5x5 neighborhood
            for (int x = -2; x <= 2; x++)
            {
                for (int y = -2; y <= 2; y++)
                {
                    int checkX = gridX + x;
                    int checkY = gridY + y;

                    if (checkX < 0 || checkX >= gridWidth || checkY < 0 || checkY >= gridHeight)
                    {
                        continue;
                    }

                    if (grid[checkX, checkY].HasValue)
                    {
                        float dist = Vector2.Distance(point, grid[checkX, checkY].Value);
                        if (dist < minDist)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        // Public accessors
        public int TotalPlacedObjects
        {
            get
            {
                int count = 0;
                foreach (var list in chunkObjects.Values)
                {
                    count += list.Count;
                }
                return count;
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Clear All Objects")]
        private void ClearAllObjects()
        {
            foreach (var coord in new List<Vector2Int>(chunkObjects.Keys))
            {
                ClearObjectsFromChunk(coord);
            }
        }
#endif
    }
}
