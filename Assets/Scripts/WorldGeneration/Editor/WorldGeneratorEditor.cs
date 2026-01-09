using UnityEngine;
using UnityEditor;

namespace WorldGeneration.Editor
{
    /// <summary>
    /// Custom editor for WorldGenerator with quick setup and testing tools.
    /// </summary>
    [CustomEditor(typeof(WorldGenerator))]
    public class WorldGeneratorEditor : UnityEditor.Editor
    {
        private WorldGenerator worldGen;
        private bool showDebugSection = true;
        private bool showQuickSetup = false;

        private void OnEnable()
        {
            worldGen = (WorldGenerator)target;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);

            // Quick actions
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Generate World", GUILayout.Height(30)))
            {
                if (Application.isPlaying)
                {
                    worldGen.GenerateWorld();
                }
                else
                {
                    EditorUtility.DisplayDialog("Play Mode Required", 
                        "World generation requires Play Mode. Press Play first.", "OK");
                }
            }

            if (GUILayout.Button("Random Seed", GUILayout.Height(30)))
            {
                Undo.RecordObject(worldGen, "Randomize Seed");
                worldGen.SetSeed(Random.Range(0, int.MaxValue));
                EditorUtility.SetDirty(worldGen);
            }
            
            EditorGUILayout.EndHorizontal();

            // Debug info
            showDebugSection = EditorGUILayout.Foldout(showDebugSection, "Debug Info");
            if (showDebugSection && Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    $"Seed: {worldGen.WorldSeed}\n" +
                    $"Initialized: {worldGen.IsInitialized}\n" +
                    $"Generating: {worldGen.IsGenerating}",
                    MessageType.Info
                );

                if (worldGen.Chunks != null)
                {
                    EditorGUILayout.LabelField($"Active Chunks: {worldGen.Chunks.ActiveChunkCount}");
                    EditorGUILayout.LabelField($"Pooled Chunks: {worldGen.Chunks.PooledChunkCount}");
                }

                if (worldGen.Objects != null)
                {
                    EditorGUILayout.LabelField($"Placed Objects: {worldGen.Objects.TotalPlacedObjects}");
                }
            }

            // Quick setup section
            EditorGUILayout.Space(10);
            showQuickSetup = EditorGUILayout.Foldout(showQuickSetup, "Quick Setup");
            if (showQuickSetup)
            {
                EditorGUILayout.HelpBox(
                    "Click 'Create Full Setup' to automatically create all required components " +
                    "and a test scene configuration.", MessageType.Info);

                if (GUILayout.Button("Create Full Setup"))
                {
                    CreateFullSetup();
                }

                if (GUILayout.Button("Create Terrain Material"))
                {
                    CreateTerrainMaterial();
                }
            }
        }

        private void CreateFullSetup()
        {
            // Ensure all child components exist
            Transform root = worldGen.transform;

            // NoiseGenerator
            if (root.Find("NoiseGenerator") == null)
            {
                GameObject noise = new GameObject("NoiseGenerator");
                noise.transform.SetParent(root);
                noise.AddComponent<NoiseGenerator>();
            }

            // ChunkManager
            if (root.Find("ChunkManager") == null)
            {
                GameObject chunks = new GameObject("ChunkManager");
                chunks.transform.SetParent(root);
                chunks.AddComponent<ChunkManager>();
            }

            // BiomeManager
            if (root.Find("BiomeManager") == null)
            {
                GameObject biomes = new GameObject("BiomeManager");
                biomes.transform.SetParent(root);
                biomes.AddComponent<BiomeManager>();
            }

            // ObjectPlacer
            if (root.Find("ObjectPlacer") == null)
            {
                GameObject placer = new GameObject("ObjectPlacer");
                placer.transform.SetParent(root);
                placer.AddComponent<ObjectPlacer>();
            }

            EditorUtility.DisplayDialog("Setup Complete", 
                "All subsystems have been created. Assign a terrain material to the ChunkManager.", "OK");
        }

        private void CreateTerrainMaterial()
        {
            Shader terrainShader = Shader.Find("Custom/ProceduralTerrainURP");
            if (terrainShader == null)
            {
                terrainShader = Shader.Find("Universal Render Pipeline/Lit");
            }

            Material mat = new Material(terrainShader);
            mat.name = "ProceduralTerrainMaterial";

            string path = EditorUtility.SaveFilePanelInProject(
                "Save Terrain Material",
                "ProceduralTerrainMaterial",
                "mat",
                "Save the terrain material"
            );

            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.CreateAsset(mat, path);
                AssetDatabase.SaveAssets();

                EditorUtility.DisplayDialog("Material Created", 
                    $"Material saved to: {path}\n\nAssign textures and configure biome colors in the material inspector.", "OK");

                Selection.activeObject = mat;
            }
        }
    }

    /// <summary>
    /// Menu items for quick creation of World Generation system.
    /// </summary>
    public static class WorldGenerationMenu
    {
        [MenuItem("GameObject/World Generation/Create World Generator", false, 10)]
        public static void CreateWorldGenerator()
        {
            GameObject root = new GameObject("WorldGenerator");
            root.AddComponent<WorldGenerator>();
            
            Selection.activeGameObject = root;
            Undo.RegisterCreatedObjectUndo(root, "Create World Generator");
        }

        [MenuItem("GameObject/World Generation/Create Complete Setup", false, 11)]
        public static void CreateCompleteSetup()
        {
            // Create root
            GameObject root = new GameObject("WorldGenerator");
            WorldGenerator worldGen = root.AddComponent<WorldGenerator>();

            // Create subsystems
            GameObject noise = new GameObject("NoiseGenerator");
            noise.transform.SetParent(root.transform);
            noise.AddComponent<NoiseGenerator>();

            GameObject chunks = new GameObject("ChunkManager");
            chunks.transform.SetParent(root.transform);
            chunks.AddComponent<ChunkManager>();

            GameObject biomes = new GameObject("BiomeManager");
            biomes.transform.SetParent(root.transform);
            biomes.AddComponent<BiomeManager>();

            GameObject placer = new GameObject("ObjectPlacer");
            placer.transform.SetParent(root.transform);
            placer.AddComponent<ObjectPlacer>();

            Selection.activeGameObject = root;
            Undo.RegisterCreatedObjectUndo(root, "Create Complete World Generator Setup");

            EditorUtility.DisplayDialog("Setup Created",
                "Complete World Generation system created!\n\n" +
                "Next steps:\n" +
                "1. Create a terrain material (Custom/ProceduralTerrainURP)\n" +
                "2. Assign material to ChunkManager\n" +
                "3. Configure NoiseGenerator parameters\n" +
                "4. Press Play to generate!", "Got it!");
        }
    }
}
