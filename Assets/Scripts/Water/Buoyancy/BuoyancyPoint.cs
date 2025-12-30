using UnityEngine;

/// <summary>
/// Representa um ponto de amostragem para o sistema de buoyancy.
/// Múltiplos pontos permitem orientação realista com as ondas.
/// </summary>
public class BuoyancyPoint : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("Influência deste ponto na força total (0-1)")]
    [Range(0f, 1f)]
    public float weight = 1f;

    [Tooltip("Offset vertical para ajuste fino")]
    public float verticalOffset = 0f;

    [Header("Debug")]
    public bool showGizmo = true;
    public Color gizmoColor = Color.cyan;

    /// <summary>
    /// Posição de amostragem ajustada
    /// </summary>
    public Vector3 SamplePosition => transform.position + Vector3.up * verticalOffset;

    /// <summary>
    /// Profundidade atual neste ponto (positivo = submerso)
    /// </summary>
    public float CurrentDepth { get; private set; }

    /// <summary>
    /// Altura da água neste ponto
    /// </summary>
    public float WaterHeight { get; private set; }

    /// <summary>
    /// Se este ponto está submerso
    /// </summary>
    public bool IsSubmerged => CurrentDepth > 0;

    /// <summary>
    /// Atualiza a detecção de água neste ponto
    /// </summary>
    public void UpdateWaterDetection(WaterSurface waterSurface)
    {
        if (waterSurface == null) return;

        Vector3 pos = SamplePosition;
        WaterHeight = waterSurface.GetWaterHeight(pos);
        CurrentDepth = WaterHeight - pos.y;
    }

    private void OnDrawGizmos()
    {
        if (!showGizmo) return;

        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(SamplePosition, 0.15f);

        // Linha indicando direção para cima
        Gizmos.DrawLine(SamplePosition, SamplePosition + Vector3.up * 0.3f);
    }

    private void OnDrawGizmosSelected()
    {
        // Desenha esfera maior quando selecionado
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.5f);
        Gizmos.DrawSphere(SamplePosition, 0.2f);
    }
}
