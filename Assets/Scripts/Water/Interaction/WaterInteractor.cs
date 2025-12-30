using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Componente que detecta a interação de um objeto (personagem) com a água.
/// Gerencia estados e modifica comportamentos baseado na profundidade.
/// </summary>
public class WaterInteractor : MonoBehaviour
{
    #region Enums
    /// <summary>
    /// Estados possíveis de interação com água
    /// </summary>
    public enum WaterState
    {
        OnLand,         // Fora da água
        Wading,         // Água rasa (andando)
        Swimming,       // Água profunda (nadando)
        Submerged       // Completamente submerso
    }
    #endregion

    #region Serialized Fields
    [Header("Detection Points")]
    [Tooltip("Ponto para detectar os pés (se null, usa transform.position)")]
    public Transform feetPoint;
    
    [Tooltip("Ponto para detectar a cintura")]
    public Transform waistPoint;
    
    [Tooltip("Ponto para detectar a cabeça")]
    public Transform headPoint;

    [Header("Depth Thresholds")]
    [Tooltip("Profundidade mínima para considerar água rasa")]
    [Range(0f, 1f)]
    public float wadingDepth = 0.1f;
    
    [Tooltip("Profundidade para iniciar natação")]
    [Range(0.5f, 2f)]
    public float swimmingDepth = 1.2f;

    [Header("Movement Modifiers")]
    [Tooltip("Multiplicador de velocidade em água rasa")]
    [Range(0.3f, 1f)]
    public float wadingSpeedMultiplier = 0.6f;
    
    [Tooltip("Multiplicador de velocidade nadando")]
    [Range(0.2f, 0.8f)]
    public float swimmingSpeedMultiplier = 0.4f;

    [Header("Physics")]
    [Tooltip("Força de empuxo quando submerso")]
    public float buoyancyForce = 15f;
    
    [Tooltip("Arrasto na água")]
    public float waterDrag = 3f;

    [Header("Events")]
    public UnityEvent<WaterState> onWaterStateChanged;
    public UnityEvent onEnterWater;
    public UnityEvent onExitWater;

    [Header("Debug")]
    public bool showDebugInfo = false;
    #endregion

    #region Properties
    /// <summary>
    /// Estado atual de interação com água
    /// </summary>
    public WaterState CurrentState { get; private set; } = WaterState.OnLand;

    /// <summary>
    /// Profundidade atual (negativo = acima da água)
    /// </summary>
    public float CurrentDepth { get; private set; }

    /// <summary>
    /// Altura atual da superfície da água na posição do objeto
    /// </summary>
    public float CurrentWaterHeight { get; private set; }

    /// <summary>
    /// Multiplicador de velocidade atual baseado no estado
    /// </summary>
    public float CurrentSpeedMultiplier { get; private set; } = 1f;

    /// <summary>
    /// Se está em qualquer tipo de água
    /// </summary>
    public bool IsInWater => CurrentState != WaterState.OnLand;

    /// <summary>
    /// Se está nadando ou submerso
    /// </summary>
    public bool IsSwimming => CurrentState == WaterState.Swimming || CurrentState == WaterState.Submerged;
    #endregion

    #region Private Fields
    private WaterSurface waterSurface;
    private Rigidbody rb;
    private CharacterController characterController;
    private PlayerController playerController;
    private WaterState previousState = WaterState.OnLand;
    private float originalDrag;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        // Busca referências
        waterSurface = WaterSurface.Instance;
        rb = GetComponent<Rigidbody>();
        characterController = GetComponent<CharacterController>();
        playerController = GetComponent<PlayerController>();

        if (rb != null)
        {
            originalDrag = rb.linearDamping;
        }

        // Configura pontos de detecção padrão se não definidos
        SetupDefaultDetectionPoints();
    }

    private void Update()
    {
        if (waterSurface == null || waterSurface.waveSettings == null) return;

        UpdateWaterDetection();
        UpdateState();
        UpdateSpeedMultiplier();
        
        // Sincroniza com PlayerController
        if (playerController != null)
        {
            playerController.speedMultiplier = CurrentSpeedMultiplier;
        }
    }

    private void FixedUpdate()
    {
        if (rb != null && IsInWater)
        {
            ApplyWaterPhysics();
        }
    }
    #endregion

    #region Detection
    private void SetupDefaultDetectionPoints()
    {
        if (feetPoint == null)
        {
            feetPoint = transform;
        }

        if (waistPoint == null)
        {
            GameObject waist = new GameObject("WaistPoint");
            waist.transform.SetParent(transform);
            waist.transform.localPosition = new Vector3(0, 1f, 0);
            waistPoint = waist.transform;
        }

        if (headPoint == null)
        {
            GameObject head = new GameObject("HeadPoint");
            head.transform.SetParent(transform);
            head.transform.localPosition = new Vector3(0, 1.7f, 0);
            headPoint = head.transform;
        }
    }

    private void UpdateWaterDetection()
    {
        Vector3 feetPos = feetPoint.position;
        CurrentWaterHeight = waterSurface.GetWaterHeight(feetPos);
        CurrentDepth = CurrentWaterHeight - feetPos.y;
    }

    private void UpdateState()
    {
        WaterState newState;

        // Verifica se a cabeça está submersa
        if (waterSurface.IsUnderwater(headPoint.position))
        {
            newState = WaterState.Submerged;
        }
        // Verifica se a cintura está na água (natação)
        else if (waterSurface.IsUnderwater(waistPoint.position) || CurrentDepth >= swimmingDepth)
        {
            newState = WaterState.Swimming;
        }
        // Verifica água rasa
        else if (CurrentDepth >= wadingDepth)
        {
            newState = WaterState.Wading;
        }
        else
        {
            newState = WaterState.OnLand;
        }

        // Dispara eventos se o estado mudou
        if (newState != CurrentState)
        {
            WaterState oldState = CurrentState;
            CurrentState = newState;
            
            OnStateChanged(oldState, newState);
        }

        previousState = CurrentState;
    }

    private void OnStateChanged(WaterState oldState, WaterState newState)
    {
        // Evento genérico de mudança de estado
        onWaterStateChanged?.Invoke(newState);

        // Entrou na água
        if (oldState == WaterState.OnLand && newState != WaterState.OnLand)
        {
            onEnterWater?.Invoke();
            if (rb != null) rb.linearDamping = waterDrag;
        }

        // Saiu da água
        if (oldState != WaterState.OnLand && newState == WaterState.OnLand)
        {
            onExitWater?.Invoke();
            if (rb != null) rb.linearDamping = originalDrag;
        }

        if (showDebugInfo)
        {
            Debug.Log($"[WaterInteractor] Estado: {oldState} -> {newState}");
        }
    }
    #endregion

    #region Speed Modification
    private void UpdateSpeedMultiplier()
    {
        switch (CurrentState)
        {
            case WaterState.OnLand:
                CurrentSpeedMultiplier = 1f;
                break;
            case WaterState.Wading:
                // Interpolação suave baseada na profundidade
                float wadingFactor = Mathf.InverseLerp(wadingDepth, swimmingDepth, CurrentDepth);
                CurrentSpeedMultiplier = Mathf.Lerp(1f, wadingSpeedMultiplier, wadingFactor);
                break;
            case WaterState.Swimming:
            case WaterState.Submerged:
                CurrentSpeedMultiplier = swimmingSpeedMultiplier;
                break;
        }
    }
    #endregion

    #region Physics
    private void ApplyWaterPhysics()
    {
        if (rb == null) return;

        // Força de empuxo quando submerso
        if (CurrentDepth > 0)
        {
            // Empuxo proporcional à submersão
            float submersionFactor = Mathf.Clamp01(CurrentDepth / swimmingDepth);
            Vector3 buoyancy = Vector3.up * buoyancyForce * submersionFactor;
            rb.AddForce(buoyancy, ForceMode.Acceleration);
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Força uma atualização de estado (útil após teleporte)
    /// </summary>
    public void ForceUpdateState()
    {
        if (waterSurface != null)
        {
            UpdateWaterDetection();
            UpdateState();
            UpdateSpeedMultiplier();
        }
    }

    /// <summary>
    /// Retorna a posição na superfície da água acima/abaixo do personagem
    /// </summary>
    public Vector3 GetSurfacePosition()
    {
        Vector3 pos = feetPoint.position;
        pos.y = CurrentWaterHeight;
        return pos;
    }
    #endregion

    #region Gizmos
    private void OnDrawGizmosSelected()
    {
        // Desenha pontos de detecção
        Gizmos.color = Color.cyan;
        if (feetPoint != null) Gizmos.DrawWireSphere(feetPoint.position, 0.1f);
        
        Gizmos.color = Color.blue;
        if (waistPoint != null) Gizmos.DrawWireSphere(waistPoint.position, 0.1f);
        
        Gizmos.color = Color.magenta;
        if (headPoint != null) Gizmos.DrawWireSphere(headPoint.position, 0.1f);

        // Desenha linha até a superfície da água (em play mode)
        if (Application.isPlaying && waterSurface != null)
        {
            Gizmos.color = IsInWater ? Color.blue : Color.green;
            Vector3 surfacePos = GetSurfacePosition();
            Gizmos.DrawLine(feetPoint.position, surfacePos);
            Gizmos.DrawWireSphere(surfacePos, 0.2f);
        }
    }
    #endregion
}
