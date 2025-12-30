using UnityEngine;

/// <summary>
/// Gera uma malha de plano altamente subdividida para melhor deslocamento de ondas.
/// Pode ser usado em runtime ou via menu de contexto no Editor.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class WaterMeshGenerator : MonoBehaviour
{
    [Header("Mesh Settings")]
    [Tooltip("Tamanho total do plano (metros)")]
    public float size = 100f;
    
    [Tooltip("Número de subdivisões (máximo ~250 para evitar limite de 65k vértices por malha)")]
    [Range(2, 254)]
    public int resolution = 50;

    [Header("Auto Setup")]
    public bool generateOnStart = true;

    private void Start()
    {
        if (generateOnStart)
        {
            GenerateMesh();
        }
    }

    [ContextMenu("Generate Mesh")]
    public void GenerateMesh()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        Mesh mesh = new Mesh();
        mesh.name = "WaterPlane_" + resolution;

        // Limite da Unity para malhas de 16-bit (65535 vértices)
        // resolution * resolution é o número de vértices
        if (resolution > 255)
        {
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        int vCount = resolution * resolution;
        Vector3[] vertices = new Vector3[vCount];
        Vector2[] uvs = new Vector2[vCount];
        int[] triangles = new int[(resolution - 1) * (resolution - 1) * 6];

        float step = size / (resolution - 1);
        float offset = size * 0.5f;

        for (int i = 0; i < resolution; i++)
        {
            for (int j = 0; j < resolution; j++)
            {
                int index = i * resolution + j;
                vertices[index] = new Vector3(j * step - offset, 0, i * step - offset);
                uvs[index] = new Vector2((float)j / (resolution - 1), (float)i / (resolution - 1));
            }
        }

        int triIndex = 0;
        for (int i = 0; i < resolution - 1; i++)
        {
            for (int j = 0; j < resolution - 1; j++)
            {
                int start = i * resolution + j;
                
                triangles[triIndex++] = start;
                triangles[triIndex++] = start + resolution;
                triangles[triIndex++] = start + 1;

                triangles[triIndex++] = start + 1;
                triangles[triIndex++] = start + resolution;
                triangles[triIndex++] = start + resolution + 1;
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshFilter.mesh = mesh;
        
        Debug.Log($"[WaterMeshGenerator] Malha gerada com {vCount} vértices.");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, new Vector3(size, 0.1f, size));
    }
}
