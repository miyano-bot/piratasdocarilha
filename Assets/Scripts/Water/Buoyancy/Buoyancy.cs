using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Sistema de empuxo (buoyancy) para objetos flutuantes.
/// Usa pontos de amostragem para calcular força e orientação.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Buoyancy : MonoBehaviour
{
    #region Serialized Fields
    [Header("Buoyancy Settings")]
    [Tooltip("Força de empuxo por ponto submerso")]
    public float buoyancyForce = 20f;

    [Tooltip("Densidade relativa do objeto (menor = mais flutuante)")]
    [Range(0.1f, 2f)]
    public float objectDensity = 0.5f;

    [Tooltip("Altura alvo acima da água (offset de flutuação)")]
    public float floatHeight = 0.5f;

    [Header("Damping")]
    [Tooltip("Amortecimento vertical para evitar oscilação")]
    [Range(0f, 10f)]
    public float verticalDamping = 3f;

    [Tooltip("Amortecimento angular para estabilidade")]
    [Range(0f, 10f)]
    public float angularDamping = 4f;

    [Header("Wave Response")]
    [Tooltip("Quanto o objeto responde às ondas (0 = ignora ondas)")]
    [Range(0f, 1f)]
    public float waveResponse = 0.5f;

    [Tooltip("Velocidade de alinhamento com a superfície")]
    [Range(0.1f, 20f)]
    public float alignmentSpeed = 5f;

    [Header("Buoyancy Points")]
    [Tooltip("Pontos de amostragem para buoyancy. Se vazio, usa posição central.")]
    public List<BuoyancyPoint> buoyancyPoints = new List<BuoyancyPoint>();

    [Tooltip("Auto-detectar pontos filhos com BuoyancyPoint")]
    public bool autoDetectPoints = true;

    [Header("Performance")]
    [Tooltip("Atualizar a cada N FixedUpdates (1 = todo frame)")]
    [Range(1, 4)]
    public int updateInterval = 1;

    [Header("Debug")]
    public bool showDebugForces = false;
    #endregion

    #region Properties
    /// <summary>
    /// Se o objeto está atualmente na água
    /// </summary>
    public bool IsInWater { get; private set; }

    /// <summary>
    /// Percentual de submersão (0-1)
    /// </summary>
    public float SubmersionPercent { get; private set; }

    /// <summary>
    /// Normal média da superfície sob o objeto
    /// </summary>
    public Vector3 AverageSurfaceNormal { get; private set; } = Vector3.up;
    #endregion

    #region Private Fields
    private Rigidbody rb;
    private WaterSurface waterSurface;
    private int frameCounter = 0;
    private Vector3 lastForce;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        
        // Auto-detecta pontos de buoyancy nos filhos
        if (autoDetectPoints)
        {
            var childPoints = GetComponentsInChildren<BuoyancyPoint>();
            foreach (var point in childPoints)
            {
                if (!buoyancyPoints.Contains(point))
                {
                    buoyancyPoints.Add(point);
                }
            }
        }

        // Se não há pontos, cria um central
        if (buoyancyPoints.Count == 0)
        {
            CreateDefaultBuoyancyPoints();
        }
    }

    private void Start()
    {
        waterSurface = WaterSurface.Instance;
    }

    private void FixedUpdate()
    {
        if (waterSurface == null || waterSurface.waveSettings == null) return;

        // Otimização: atualiza a cada N frames
        frameCounter++;
        if (frameCounter < updateInterval) return;
        frameCounter = 0;

        UpdateBuoyancy();
    }
    #endregion

    #region Buoyancy Calculation
    private void UpdateBuoyancy()
    {
        int submergedCount = 0;
        Vector3 totalForce = Vector3.zero;
        Vector3 totalNormal = Vector3.zero;
        float totalWeight = 0f;

        foreach (var point in buoyancyPoints)
        {
            if (point == null) continue;

            // Atualiza detecção de água no ponto
            point.UpdateWaterDetection(waterSurface);

            if (point.IsSubmerged)
            {
                submergedCount++;
                
                // Calcula força de empuxo proporcional à submersão
                float submersion = Mathf.Clamp01(point.CurrentDepth / floatHeight);
                float force = buoyancyForce * submersion * point.weight / objectDensity;

                // Aplica força no ponto
                Vector3 forceVector = Vector3.up * force;
                rb.AddForceAtPosition(forceVector, point.SamplePosition, ForceMode.Force);
                
                totalForce += forceVector;

                // Acumula normal para orientação
                var (_, normal) = waterSurface.GetWaterData(point.SamplePosition);
                totalNormal += normal * point.weight;
                totalWeight += point.weight;

                if (showDebugForces)
                {
                    Debug.DrawRay(point.SamplePosition, forceVector * 0.1f, Color.green);
                }
            }
        }

        // Atualiza estado
        IsInWater = submergedCount > 0;
        SubmersionPercent = buoyancyPoints.Count > 0 ? (float)submergedCount / buoyancyPoints.Count : 0f;
        
        if (totalWeight > 0)
        {
            AverageSurfaceNormal = (totalNormal / totalWeight).normalized;
        }

        lastForce = totalForce;

        // Aplica damping e alinhamento
        if (IsInWater)
        {
            ApplyDamping();
            ApplyWaveAlignment();
        }
    }

    private void ApplyDamping()
    {
        // Damping vertical para evitar "pular" na água
        Vector3 velocity = rb.linearVelocity;
        float verticalVel = velocity.y;
        rb.AddForce(Vector3.down * verticalVel * verticalDamping, ForceMode.Acceleration);

        // Damping angular para estabilidade
        rb.AddTorque(-rb.angularVelocity * angularDamping, ForceMode.Acceleration);
    }

    private void ApplyWaveAlignment()
    {
        if (waveResponse <= 0) return;

        // Calcula a rotação alvo baseada na normal da superfície
        Quaternion targetRotation = Quaternion.FromToRotation(transform.up, AverageSurfaceNormal) * transform.rotation;
        
        // Mantém o yaw (rotação horizontal) original
        Vector3 targetEuler = targetRotation.eulerAngles;
        targetEuler.y = transform.eulerAngles.y;
        targetRotation = Quaternion.Euler(targetEuler);

        // Usa torque para alinhar em vez de MoveRotation (mais estável com AddForce)
        Quaternion rotationDelta = targetRotation * Quaternion.Inverse(transform.rotation);
        rotationDelta.ToAngleAxis(out float angle, out Vector3 axis);

        if (angle > 180) angle -= 360;

        if (Mathf.Abs(angle) > 0.01f)
        {
            Vector3 torque = axis.normalized * angle * alignmentSpeed * waveResponse;
            rb.AddTorque(torque - rb.angularVelocity * angularDamping, ForceMode.Acceleration);
        }
    }

    private void CreateDefaultBuoyancyPoints()
    {
        // Cria 4 pontos nos cantos para objetos genéricos
        float offset = 1f;
        Vector3[] positions = new Vector3[]
        {
            new Vector3(-offset, 0, -offset),
            new Vector3(-offset, 0, offset),
            new Vector3(offset, 0, -offset),
            new Vector3(offset, 0, offset)
        };

        foreach (var pos in positions)
        {
            GameObject pointObj = new GameObject("BuoyancyPoint");
            pointObj.transform.SetParent(transform);
            pointObj.transform.localPosition = pos;
            
            BuoyancyPoint point = pointObj.AddComponent<BuoyancyPoint>();
            point.weight = 0.25f;
            buoyancyPoints.Add(point);
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Adiciona uma força externa respeitando a física da água
    /// </summary>
    public void AddWaterForce(Vector3 force, ForceMode mode = ForceMode.Force)
    {
        if (IsInWater)
        {
            // Reduz força horizontal quando na água
            force.x *= 0.5f;
            force.z *= 0.5f;
        }
        rb.AddForce(force, mode);
    }

    /// <summary>
    /// Reconfigura os pontos de buoyancy em runtime
    /// </summary>
    public void RefreshBuoyancyPoints()
    {
        buoyancyPoints.Clear();
        var childPoints = GetComponentsInChildren<BuoyancyPoint>();
        buoyancyPoints.AddRange(childPoints);
    }
    #endregion

    #region Gizmos
    private void OnDrawGizmosSelected()
    {
        if (buoyancyPoints == null) return;

        // Desenha conexões entre pontos
        Gizmos.color = Color.yellow;
        for (int i = 0; i < buoyancyPoints.Count; i++)
        {
            if (buoyancyPoints[i] == null) continue;
            
            for (int j = i + 1; j < buoyancyPoints.Count; j++)
            {
                if (buoyancyPoints[j] == null) continue;
                Gizmos.DrawLine(buoyancyPoints[i].SamplePosition, buoyancyPoints[j].SamplePosition);
            }
        }

        // Desenha centro de massa
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
    }
    #endregion
}
