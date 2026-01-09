using UnityEngine;

namespace WorldGeneration
{
    /// <summary>
    /// Defines a biome with its texture and height/slope requirements.
    /// </summary>
    [System.Serializable]
    public class BiomeLayer
    {
        public string name = "Biome";
        public Texture2D albedoTexture;
        public Texture2D normalTexture;
        public Color tintColor = Color.white;
        
        [Range(0f, 1f)]
        public float minHeight = 0f;
        [Range(0f, 1f)]
        public float maxHeight = 1f;
        
        [Range(0f, 1f)]
        public float minSlope = 0f;
        [Range(0f, 1f)]
        public float maxSlope = 1f;
        
        [Range(0f, 1f)]
        public float blendStrength = 0.1f;
        
        public float textureScale = 10f;
    }

    /// <summary>
    /// Manages biome texturing for terrain based on height and slope.
    /// Generates a terrain material with layered textures.
    /// </summary>
    public class BiomeManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private NoiseGenerator noiseGenerator;
        [SerializeField] private ChunkManager chunkManager;

        [Header("Biome Layers")]
        [SerializeField] private BiomeLayer[] biomeLayers = new BiomeLayer[]
        {
            new BiomeLayer { name = "Sand", minHeight = 0f, maxHeight = 0.1f, minSlope = 0f, maxSlope = 0.3f },
            new BiomeLayer { name = "Grass", minHeight = 0.05f, maxHeight = 0.5f, minSlope = 0f, maxSlope = 0.4f },
            new BiomeLayer { name = "Rock", minHeight = 0.3f, maxHeight = 1f, minSlope = 0.35f, maxSlope = 1f },
            new BiomeLayer { name = "Snow", minHeight = 0.7f, maxHeight = 1f, minSlope = 0f, maxSlope = 0.5f }
        };

        [Header("Material Settings")]
        [SerializeField] private Shader terrainShader;
        [SerializeField] private Material generatedMaterial;

        [Header("Splatmap Settings")]
        [SerializeField] private int splatmapResolution = 512;
        [SerializeField] private bool generateSplatmaps = true;

        // Shader property IDs
        private static readonly int BaseColorMap = Shader.PropertyToID("_BaseColorMap");
        private static readonly int NormalMap = Shader.PropertyToID("_NormalMap");
        private static readonly int HeightMinMax = Shader.PropertyToID("_HeightMinMax");
        private static readonly int SlopeMinMax = Shader.PropertyToID("_SlopeMinMax");
        private static readonly int TextureScale = Shader.PropertyToID("_TextureScale");
        private static readonly int BlendStrength = Shader.PropertyToID("_BlendStrength");
        private static readonly int TintColor = Shader.PropertyToID("_TintColor");

        private void Awake()
        {
            if (noiseGenerator == null)
            {
#if UNITY_2023_1_OR_NEWER
                noiseGenerator = Object.FindFirstObjectByType<NoiseGenerator>();
#else
                noiseGenerator = FindObjectOfType<NoiseGenerator>();
#endif
            }
        }

        private void Start()
        {
            if (generateSplatmaps)
            {
                CreateTerrainMaterial();
            }
        }

        /// <summary>
        /// Create the terrain material with biome layer support.
        /// </summary>
        public void CreateTerrainMaterial()
        {
            if (terrainShader == null)
            {
                // Use URP Lit as fallback
                terrainShader = Shader.Find("Universal Render Pipeline/Lit");
            }

            generatedMaterial = new Material(terrainShader);
            generatedMaterial.name = "ProceduralTerrainMaterial";

            // Apply biome textures
            ApplyBiomeTextures();

            // Assign to chunk manager
            if (chunkManager != null)
            {
                // Material will be used by new chunks
            }
        }

        /// <summary>
        /// Apply biome layer textures to the material.
        /// </summary>
        private void ApplyBiomeTextures()
        {
            if (generatedMaterial == null) return;

            // For a simple setup, use the first biome as base
            // Full implementation would use a custom shader with texture arrays
            if (biomeLayers.Length > 0 && biomeLayers[0].albedoTexture != null)
            {
                generatedMaterial.SetTexture("_BaseMap", biomeLayers[0].albedoTexture);
                generatedMaterial.SetColor("_BaseColor", biomeLayers[0].tintColor);
            }
        }

        /// <summary>
        /// Get the dominant biome at a world position.
        /// </summary>
        public BiomeLayer GetBiomeAtPosition(float x, float z)
        {
            if (noiseGenerator == null) return null;

            float normalizedHeight = noiseGenerator.GetNormalizedHeight(x, z);
            float slope = noiseGenerator.GetSlope(x, z);

            BiomeLayer bestBiome = null;
            float bestWeight = 0f;

            foreach (var biome in biomeLayers)
            {
                float weight = CalculateBiomeWeight(biome, normalizedHeight, slope);
                if (weight > bestWeight)
                {
                    bestWeight = weight;
                    bestBiome = biome;
                }
            }

            return bestBiome;
        }

        /// <summary>
        /// Get biome blend weights at a world position.
        /// </summary>
        public float[] GetBiomeWeightsAtPosition(float x, float z)
        {
            if (noiseGenerator == null) return new float[biomeLayers.Length];

            float normalizedHeight = noiseGenerator.GetNormalizedHeight(x, z);
            float slope = noiseGenerator.GetSlope(x, z);

            float[] weights = new float[biomeLayers.Length];
            float totalWeight = 0f;

            for (int i = 0; i < biomeLayers.Length; i++)
            {
                weights[i] = CalculateBiomeWeight(biomeLayers[i], normalizedHeight, slope);
                totalWeight += weights[i];
            }

            // Normalize weights
            if (totalWeight > 0f)
            {
                for (int i = 0; i < weights.Length; i++)
                {
                    weights[i] /= totalWeight;
                }
            }

            return weights;
        }

        /// <summary>
        /// Calculate the blend weight for a biome at given height and slope.
        /// </summary>
        private float CalculateBiomeWeight(BiomeLayer biome, float normalizedHeight, float slope)
        {
            // Height weight
            float heightWeight = CalculateBlendWeight(
                normalizedHeight,
                biome.minHeight,
                biome.maxHeight,
                biome.blendStrength
            );

            // Slope weight
            float slopeWeight = CalculateBlendWeight(
                slope,
                biome.minSlope,
                biome.maxSlope,
                biome.blendStrength
            );

            return heightWeight * slopeWeight;
        }

        /// <summary>
        /// Calculate a smooth blend weight with falloff at edges.
        /// </summary>
        private float CalculateBlendWeight(float value, float min, float max, float blendStrength)
        {
            if (value < min - blendStrength || value > max + blendStrength)
            {
                return 0f;
            }

            float weight = 1f;

            // Fade in at min boundary
            if (value < min + blendStrength)
            {
                float t = (value - (min - blendStrength)) / (2f * blendStrength);
                weight *= Mathf.SmoothStep(0f, 1f, t);
            }

            // Fade out at max boundary
            if (value > max - blendStrength)
            {
                float t = ((max + blendStrength) - value) / (2f * blendStrength);
                weight *= Mathf.SmoothStep(0f, 1f, t);
            }

            return weight;
        }

        /// <summary>
        /// Generate a splatmap texture for a chunk.
        /// </summary>
        public Texture2D GenerateSplatmap(Vector2Int chunkCoord, int chunkSize)
        {
            Texture2D splatmap = new Texture2D(splatmapResolution, splatmapResolution, TextureFormat.RGBA32, false);
            splatmap.wrapMode = TextureWrapMode.Clamp;

            Color[] colors = new Color[splatmapResolution * splatmapResolution];

            float worldOffsetX = chunkCoord.x * chunkSize;
            float worldOffsetZ = chunkCoord.y * chunkSize;
            float stepSize = (float)chunkSize / splatmapResolution;

            for (int z = 0; z < splatmapResolution; z++)
            {
                for (int x = 0; x < splatmapResolution; x++)
                {
                    float worldX = worldOffsetX + x * stepSize;
                    float worldZ = worldOffsetZ + z * stepSize;

                    float[] weights = GetBiomeWeightsAtPosition(worldX, worldZ);

                    // Pack first 4 biomes into RGBA
                    Color color = new Color(
                        weights.Length > 0 ? weights[0] : 0f,
                        weights.Length > 1 ? weights[1] : 0f,
                        weights.Length > 2 ? weights[2] : 0f,
                        weights.Length > 3 ? weights[3] : 0f
                    );

                    colors[z * splatmapResolution + x] = color;
                }
            }

            splatmap.SetPixels(colors);
            splatmap.Apply();

            return splatmap;
        }

        /// <summary>
        /// Get the biome color at a position (for debugging/minimap).
        /// </summary>
        public Color GetBiomeColorAtPosition(float x, float z)
        {
            float[] weights = GetBiomeWeightsAtPosition(x, z);
            Color result = Color.black;

            for (int i = 0; i < biomeLayers.Length && i < weights.Length; i++)
            {
                result += biomeLayers[i].tintColor * weights[i];
            }

            return result;
        }

        // Public accessors
        public Material GeneratedMaterial => generatedMaterial;
        public BiomeLayer[] BiomeLayers => biomeLayers;
        public int BiomeCount => biomeLayers.Length;

#if UNITY_EDITOR
        [ContextMenu("Regenerate Material")]
        private void RegenerateMaterial()
        {
            CreateTerrainMaterial();
        }
#endif
    }
}
