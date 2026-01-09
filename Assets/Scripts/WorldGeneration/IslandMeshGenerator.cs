using UnityEngine;

namespace WorldGeneration
{
    /// <summary>
    /// Generates terrain meshes from height data provided by NoiseGenerator.
    /// Handles mesh creation, UV mapping, and normals calculation.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class IslandMeshGenerator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private NoiseGenerator noiseGenerator;

        [Header("Mesh Settings")]
        [Tooltip("Size of a chunk in world units. Matches ChunkManager.chunkSize.")]
        [SerializeField] private int chunkSize = 100;
        [Tooltip("Vertex resolution per side for the chunk mesh. Higher = denser mesh.")]
        [SerializeField] private int resolution = 128; // Vertices per side
        [Tooltip("UV tiling scale for terrain textures on this mesh.")]
        [SerializeField] private float uvScale = 0.1f;

        [Header("LOD Settings")]
        [SerializeField] private int lodLevel = 0; // 0 = highest detail
        [SerializeField] private int maxLodLevel = 4;
        [Header("Skirt Settings")]
        [Tooltip("Depth (world units) for skirt outer vertices. Negative values go below water.")]
        [SerializeField] private float skirtDepth = -2f;
        [Tooltip("Enable geometry skirts around island edges to hide seams next to open ocean.")]
        [SerializeField] private bool generateSkirt = true;
        [Tooltip("If enabled, create a sloped skirt (inner + outer) instead of a vertical drop.")]
        [SerializeField] private bool slopedSkirt = true;
        [Tooltip("How much the inner skirt slides toward the outer depth. 0 = none, 1 = full.")]
        [SerializeField] [Range(0f, 1f)] private float skirtBlend = 0.5f; // 0 = vertical (original), 1 = full slope to skirtDepth
        [Tooltip("Texture tiling scale applied to skirt UVs; helps hide repeated texels.")]
        [SerializeField] private float skirtUVScale = 1f;

        // Cached components
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;

        // Chunk position in world
        private Vector2Int chunkCoord;

        private void Awake()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
        }

        /// <summary>
        /// Initialize and generate mesh for a specific chunk coordinate.
        /// </summary>
        public void Initialize(NoiseGenerator generator, Vector2Int coord, int size, int res, int lod = 0)
        {
            noiseGenerator = generator;
            chunkCoord = coord;
            chunkSize = size;
            resolution = res;
            lodLevel = Mathf.Clamp(lod, 0, maxLodLevel);

            GenerateMesh();
        }

        /// <summary>
        /// Generate the terrain mesh for this chunk.
        /// </summary>
        public void GenerateMesh()
        {
            if (noiseGenerator == null)
            {
                Debug.LogError("NoiseGenerator reference is missing!");
                return;
            }

            // Calculate actual resolution based on LOD
            int lodMultiplier = (int)Mathf.Pow(2, lodLevel);
            int actualResolution = Mathf.Max(2, resolution / lodMultiplier);

            // Create mesh data
            Vector3[] vertices = new Vector3[actualResolution * actualResolution];
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] triangles = new int[(actualResolution - 1) * (actualResolution - 1) * 6];

            float stepSize = (float)chunkSize / (actualResolution - 1);
            float worldOffsetX = chunkCoord.x * chunkSize;
            float worldOffsetZ = chunkCoord.y * chunkSize;

            // Generate vertices and UVs
            int vertexIndex = 0;
            for (int z = 0; z < actualResolution; z++)
            {
                for (int x = 0; x < actualResolution; x++)
                {
                    float localX = x * stepSize;
                    float localZ = z * stepSize;
                    float worldX = worldOffsetX + localX;
                    float worldZ = worldOffsetZ + localZ;

                    float height = noiseGenerator.GetHeight(worldX, worldZ);

                    vertices[vertexIndex] = new Vector3(localX, height, localZ);
                    uvs[vertexIndex] = new Vector2(localX * uvScale, localZ * uvScale);

                    vertexIndex++;
                }
            }

            // Generate triangles but skip triangles that are deep underwater (far below sea level)
            // Allow shoreline triangles (slightly negative heights) to create beach/slope
            float deepWaterThreshold = -5f; // Only skip if ALL vertices are below this depth
            var triangleList = new System.Collections.Generic.List<int>((actualResolution - 1) * (actualResolution - 1) * 6);
            for (int z = 0; z < actualResolution - 1; z++)
            {
                for (int x = 0; x < actualResolution - 1; x++)
                {
                    int bottomLeft = z * actualResolution + x;
                    int bottomRight = bottomLeft + 1;
                    int topLeft = (z + 1) * actualResolution + x;
                    int topRight = topLeft + 1;

                    float hBL = vertices[bottomLeft].y;
                    float hBR = vertices[bottomRight].y;
                    float hTL = vertices[topLeft].y;
                    float hTR = vertices[topRight].y;

                    // First triangle (bottom-left, top-left, top-right)
                    // Only skip if all vertices are deep underwater
                    if (!(hBL < deepWaterThreshold && hTL < deepWaterThreshold && hTR < deepWaterThreshold))
                    {
                        triangleList.Add(bottomLeft);
                        triangleList.Add(topLeft);
                        triangleList.Add(topRight);
                    }

                    // Second triangle (bottom-left, top-right, bottom-right)
                    if (!(hBL < deepWaterThreshold && hTR < deepWaterThreshold && hBR < deepWaterThreshold))
                    {
                        triangleList.Add(bottomLeft);
                        triangleList.Add(topRight);
                        triangleList.Add(bottomRight);
                    }
                }
            }

            triangles = triangleList.ToArray();

            // Create or update mesh
            if (mesh == null)
            {
                mesh = new Mesh();
                mesh.name = $"Chunk_{chunkCoord.x}_{chunkCoord.y}";
            }
            else
            {
                mesh.Clear();
            }

            // Optional: generate skirt to hide seams next to open ocean
            if (generateSkirt)
            {
                GenerateSkirt(ref vertices, ref uvs, ref triangles);
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;

            // Calculate normals for proper lighting
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            // Optional: Generate tangents for normal mapping
            CalculateTangents(mesh);

            meshFilter.mesh = mesh;

            // Update collider if present
            MeshCollider collider = GetComponent<MeshCollider>();
            if (collider != null)
            {
                collider.sharedMesh = mesh;
            }
        }

        /// <summary>
        /// Update mesh with a new LOD level.
        /// </summary>
        public void SetLOD(int newLodLevel)
        {
            int clampedLod = Mathf.Clamp(newLodLevel, 0, maxLodLevel);
            if (clampedLod != lodLevel)
            {
                lodLevel = clampedLod;
                GenerateMesh();
            }
        }

        /// <summary>
        /// Calculate tangents for the mesh (required for normal mapping).
        /// </summary>
        private void CalculateTangents(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            Vector2[] uvs = mesh.uv;
            int[] triangles = mesh.triangles;
            Vector3[] normals = mesh.normals;

            Vector4[] tangents = new Vector4[vertices.Length];
            Vector3[] tan1 = new Vector3[vertices.Length];
            Vector3[] tan2 = new Vector3[vertices.Length];

            for (int i = 0; i < triangles.Length; i += 3)
            {
                int i1 = triangles[i];
                int i2 = triangles[i + 1];
                int i3 = triangles[i + 2];

                Vector3 v1 = vertices[i1];
                Vector3 v2 = vertices[i2];
                Vector3 v3 = vertices[i3];

                Vector2 w1 = uvs[i1];
                Vector2 w2 = uvs[i2];
                Vector2 w3 = uvs[i3];

                float x1 = v2.x - v1.x;
                float x2 = v3.x - v1.x;
                float y1 = v2.y - v1.y;
                float y2 = v3.y - v1.y;
                float z1 = v2.z - v1.z;
                float z2 = v3.z - v1.z;

                float s1 = w2.x - w1.x;
                float s2 = w3.x - w1.x;
                float t1 = w2.y - w1.y;
                float t2 = w3.y - w1.y;

                float div = s1 * t2 - s2 * t1;
                float r = div == 0f ? 0f : 1f / div;

                Vector3 sdir = new Vector3(
                    (t2 * x1 - t1 * x2) * r,
                    (t2 * y1 - t1 * y2) * r,
                    (t2 * z1 - t1 * z2) * r
                );

                Vector3 tdir = new Vector3(
                    (s1 * x2 - s2 * x1) * r,
                    (s1 * y2 - s2 * y1) * r,
                    (s1 * z2 - s2 * z1) * r
                );

                tan1[i1] += sdir;
                tan1[i2] += sdir;
                tan1[i3] += sdir;

                tan2[i1] += tdir;
                tan2[i2] += tdir;
                tan2[i3] += tdir;
            }

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 n = normals[i];
                Vector3 t = tan1[i];

                Vector3 tmp = (t - n * Vector3.Dot(n, t)).normalized;
                float w = (Vector3.Dot(Vector3.Cross(n, t), tan2[i]) < 0f) ? -1f : 1f;

                tangents[i] = new Vector4(tmp.x, tmp.y, tmp.z, w);
            }

            mesh.tangents = tangents;
        }

        /// <summary>
        /// Generate a vertical skirt along boundary edges where triangles were omitted
        /// due to being fully ocean. This hides jagged T-junctions and seams.
        /// </summary>
        private void GenerateSkirt(ref Vector3[] vertices, ref Vector2[] uvs, ref int[] triangles)
        {
            int vertCount = vertices.Length;

            // Build edge usage map
            var edgeCount = new System.Collections.Generic.Dictionary<(int, int), int>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];

                void AddEdge(int v1, int v2)
                {
                    var key = v1 < v2 ? (v1, v2) : (v2, v1);
                    if (edgeCount.ContainsKey(key)) edgeCount[key]++;
                    else edgeCount[key] = 1;
                }

                AddEdge(a, b);
                AddEdge(b, c);
                AddEdge(c, a);
            }

            var boundaryEdges = new System.Collections.Generic.List<(int, int)>();
            foreach (var kv in edgeCount)
            {
                if (kv.Value == 1)
                {
                    boundaryEdges.Add(kv.Key);
                }
            }

            if (boundaryEdges.Count == 0) return;

            var vertList = new System.Collections.Generic.List<Vector3>(vertices);
            var uvList = new System.Collections.Generic.List<Vector2>(uvs);
            var triList = new System.Collections.Generic.List<int>(triangles);

            var innerSkirtMap = new System.Collections.Generic.Dictionary<int, int>();
            var outerSkirtMap = new System.Collections.Generic.Dictionary<int, int>();

            foreach (var edge in boundaryEdges)
            {
                int v1 = edge.Item1;
                int v2 = edge.Item2;

                // Only create skirt for edges near water surface (not deep underwater)
                // This creates beach/shore transitions
                float h1 = vertices[v1].y;
                float h2 = vertices[v2].y;
                float maxHeight = Mathf.Max(h1, h2);
                // Skip if both vertices are deep underwater
                if (maxHeight < -5f) continue;

                // Create inner skirt vertex (slightly lowered)
                if (!innerSkirtMap.ContainsKey(v1))
                {
                    Vector3 s = vertList[v1];
                    float target = skirtDepth;
                    if (slopedSkirt)
                    {
                        s.y = Mathf.Lerp(vertList[v1].y, target, skirtBlend);
                    }
                    else
                    {
                        s.y = skirtDepth;
                    }
                    innerSkirtMap[v1] = vertList.Count;
                    vertList.Add(s);
                    uvList.Add(new Vector2(uvList[v1].x * skirtUVScale, uvList[v1].y * skirtUVScale));
                }
                if (!innerSkirtMap.ContainsKey(v2))
                {
                    Vector3 s = vertList[v2];
                    float target = skirtDepth;
                    if (slopedSkirt)
                    {
                        s.y = Mathf.Lerp(vertList[v2].y, target, skirtBlend);
                    }
                    else
                    {
                        s.y = skirtDepth;
                    }
                    innerSkirtMap[v2] = vertList.Count;
                    vertList.Add(s);
                    uvList.Add(new Vector2(uvList[v2].x * skirtUVScale, uvList[v2].y * skirtUVScale));
                }

                // Create outer skirt vertex (full depth)
                if (!outerSkirtMap.ContainsKey(v1))
                {
                    Vector3 s = vertList[v1];
                    s.y = skirtDepth;
                    outerSkirtMap[v1] = vertList.Count;
                    vertList.Add(s);
                    uvList.Add(new Vector2(uvList[v1].x * skirtUVScale, uvList[v1].y * skirtUVScale));
                }
                if (!outerSkirtMap.ContainsKey(v2))
                {
                    Vector3 s = vertList[v2];
                    s.y = skirtDepth;
                    outerSkirtMap[v2] = vertList.Count;
                    vertList.Add(s);
                    uvList.Add(new Vector2(uvList[v2].x * skirtUVScale, uvList[v2].y * skirtUVScale));
                }

                int iInner1 = innerSkirtMap[v1];
                int iInner2 = innerSkirtMap[v2];
                int iOuter1 = outerSkirtMap[v1];
                int iOuter2 = outerSkirtMap[v2];

                // Create triangles: edge -> inner -> outer (two quads stacked)
                // First quad: edge to inner skirt
                triList.Add(v1);
                triList.Add(v2);
                triList.Add(iInner2);

                triList.Add(v1);
                triList.Add(iInner2);
                triList.Add(iInner1);

                // Second quad: inner to outer
                triList.Add(iInner1);
                triList.Add(iInner2);
                triList.Add(iOuter2);

                triList.Add(iInner1);
                triList.Add(iOuter2);
                triList.Add(iOuter1);
            }

            vertices = vertList.ToArray();
            uvs = uvList.ToArray();
            triangles = triList.ToArray();
        }

        /// <summary>
        /// Get height at a local position within this chunk.
        /// </summary>
        public float GetLocalHeight(float localX, float localZ)
        {
            float worldX = chunkCoord.x * chunkSize + localX;
            float worldZ = chunkCoord.y * chunkSize + localZ;
            return noiseGenerator.GetHeight(worldX, worldZ);
        }

        /// <summary>
        /// Check if a local position is on land.
        /// </summary>
        public bool IsLocalPositionLand(float localX, float localZ)
        {
            float worldX = chunkCoord.x * chunkSize + localX;
            float worldZ = chunkCoord.y * chunkSize + localZ;
            return noiseGenerator.IsLand(worldX, worldZ);
        }

        // Public accessors
        public Vector2Int ChunkCoord => chunkCoord;
        public int ChunkSize => chunkSize;
        public int Resolution => resolution;
        public int CurrentLOD => lodLevel;
        public Mesh Mesh => mesh;

        private void OnDestroy()
        {
            if (mesh != null)
            {
                Destroy(mesh);
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor utility to regenerate mesh.
        /// </summary>
        [ContextMenu("Regenerate Mesh")]
        private void RegenerateMesh()
        {
            if (noiseGenerator == null)
            {
                noiseGenerator = FindObjectOfType<NoiseGenerator>();
            }
            GenerateMesh();
        }
#endif
    }
}
