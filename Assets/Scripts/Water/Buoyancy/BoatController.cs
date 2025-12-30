using UnityEngine;

/// <summary>
/// Controlador de embarcação que integra com o sistema de Buoyancy.
/// Permite controle de movimento respeitando a física da água.
/// </summary>
[RequireComponent(typeof(Buoyancy))]
[RequireComponent(typeof(Rigidbody))]
public class BoatController : MonoBehaviour
{
    #region Serialized Fields
    [Header("Movement")]
    [Tooltip("Força de propulsão para frente")]
    public float thrustForce = 50f;

    [Tooltip("Velocidade máxima")]
    public float maxSpeed = 15f;

    [Tooltip("Força de rotação (torque)")]
    public float turnTorque = 30f;

    [Tooltip("Velocidade máxima de rotação")]
    public float maxTurnSpeed = 60f;

    [Header("Physics")]
    [Tooltip("Arrasto linear da água")]
    public float waterDrag = 2f;

    [Tooltip("Arrasto angular da água")]
    public float waterAngularDrag = 3f;

    [Tooltip("Força mínima de velocidade para permitir rotação")]
    public float minSpeedToTurn = 0.5f;

    [Header("Input")]
    [Tooltip("Eixo de thrust (vertical)")]
    public string thrustAxis = "Vertical";

    [Tooltip("Eixo de rotação (horizontal)")]
    public string turnAxis = "Horizontal";

    [Tooltip("Botão de aceleração extra")]
    public KeyCode boostKey = KeyCode.LeftShift;

    [Tooltip("Multiplicador de boost")]
    public float boostMultiplier = 1.5f;

    [Header("Audio/Visual Feedback")]
    [Tooltip("Partícula de spray (opcional)")]
    public ParticleSystem wakeParticles;

    [Tooltip("Velocidade mínima para ativar partículas")]
    public float particleSpeedThreshold = 2f;

    [Header("Interaction")]
    [Tooltip("Se o jogador pode controlar este barco atualmente")]
    public bool isControllable = true;

    [Tooltip("Transform onde o jogador fica ao pilotar")]
    public Transform pilotPosition;

    [Tooltip("Se requer estar na água para mover (desative para testes)")]
    public bool requireWaterForMovement = true;

    [Header("Debug")]
    public bool showDebugVectors = false;
    public bool showDebugInfo = false;
    #endregion

    #region Properties
    /// <summary>
    /// Velocidade atual do barco
    /// </summary>
    public float CurrentSpeed => rb.linearVelocity.magnitude;

    /// <summary>
    /// Direção atual de movimento normalizada
    /// </summary>
    public Vector3 MoveDirection => rb.linearVelocity.normalized;

    /// <summary>
    /// Referência ao sistema de buoyancy
    /// </summary>
    public Buoyancy Buoyancy => buoyancy;

    /// <summary>
    /// Se o barco está na água
    /// </summary>
    public bool IsInWater => buoyancy != null && buoyancy.IsInWater;
    #endregion

    #region Private Fields
    private Rigidbody rb;
    private Buoyancy buoyancy;
    private float currentThrust;
    private float currentTurn;
    private bool isBoosting;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        buoyancy = GetComponent<Buoyancy>();

        // Configura Rigidbody para física de barco
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    private void Start()
    {
        // Ajusta drag baseado nas configurações
        rb.linearDamping = waterDrag;
        rb.angularDamping = waterAngularDrag;
    }

    private void Update()
    {
        if (showDebugInfo)
        {
            Debug.Log($"[BoatController] Update: isControllable={isControllable}, enabled={enabled}");
        }
        
        if (!isControllable) return;

        // Lê input
        ReadInput();
        
        if (showDebugInfo)
        {
            Debug.Log($"[BoatController] Input: thrust={currentThrust}, turn={currentTurn}");
        }

        // Atualiza efeitos visuais
        UpdateVisualEffects();
    }

    private void FixedUpdate()
    {
        if (showDebugInfo)
        {
            Debug.Log($"[BoatController] FixedUpdate: isControllable={isControllable}, IsInWater={IsInWater}, requireWater={requireWaterForMovement}");
        }
        
        if (!isControllable) return;
        
        // Verifica se precisa estar na água
        bool canMove = !requireWaterForMovement || IsInWater;
        
        if (!canMove)
        {
            if (showDebugInfo) Debug.Log("[BoatController] Cannot move - water requirement not met");
            return;
        }

        ApplyThrust();
        ApplyTurn();
        ClampVelocity();
    }
    #endregion

    #region Input
    private void ReadInput()
    {
        currentThrust = Input.GetAxis(thrustAxis);
        currentTurn = Input.GetAxis(turnAxis);
        isBoosting = Input.GetKey(boostKey);
    }

    /// <summary>
    /// Define input programaticamente (para IA ou controle remoto)
    /// </summary>
    public void SetInput(float thrust, float turn, bool boost = false)
    {
        currentThrust = Mathf.Clamp(thrust, -1f, 1f);
        currentTurn = Mathf.Clamp(turn, -1f, 1f);
        isBoosting = boost;
    }
    #endregion

    #region Movement
    private void ApplyThrust()
    {
        if (Mathf.Abs(currentThrust) < 0.01f) return;

        // Debug: verifica estado do Rigidbody
        if (showDebugInfo)
        {
            Debug.Log($"[BoatController] Thrust Input: {currentThrust}, isKinematic: {rb.isKinematic}, mass: {rb.mass}");
        }

        float force = thrustForce * currentThrust;
        
        // Aplica boost
        if (isBoosting && currentThrust > 0)
        {
            force *= boostMultiplier;
        }

        // Força na direção forward do barco (usando Acceleration para ser independente de massa e mais estável)
        Vector3 thrustVector = transform.forward * force;
        rb.AddForce(thrustVector, ForceMode.Acceleration);

        if (showDebugInfo)
        {
            Debug.Log($"[BoatController] Applied force: {thrustVector}, velocity: {rb.linearVelocity}");
        }

        if (showDebugVectors)
        {
            Debug.DrawRay(transform.position, thrustVector * 0.1f, Color.green);
        }
    }

    private void ApplyTurn()
    {
        if (Mathf.Abs(currentTurn) < 0.01f) return;

        // Só permite virar se estiver se movendo
        float speedFactor = Mathf.Clamp01(CurrentSpeed / minSpeedToTurn);
        if (speedFactor < 0.1f) return;

        float torque = turnTorque * currentTurn * speedFactor;

        // Inverte rotação quando ré
        if (currentThrust < 0)
        {
            torque = -torque;
        }

        // Usando Acceleration para rotação também
        rb.AddTorque(Vector3.up * torque, ForceMode.Acceleration);

        if (showDebugVectors)
        {
            Debug.DrawRay(transform.position, Vector3.up * torque * 0.1f, Color.yellow);
        }
    }

    private void ClampVelocity()
    {
        // Limita velocidade linear
        float currentMaxSpeed = isBoosting ? maxSpeed * boostMultiplier : maxSpeed;
        if (rb.linearVelocity.magnitude > currentMaxSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * currentMaxSpeed;
        }

        // Limita velocidade angular
        if (rb.angularVelocity.magnitude > maxTurnSpeed * Mathf.Deg2Rad)
        {
            rb.angularVelocity = rb.angularVelocity.normalized * maxTurnSpeed * Mathf.Deg2Rad;
        }
    }
    #endregion

    #region Visual Effects
    private void UpdateVisualEffects()
    {
        if (wakeParticles == null) return;

        bool shouldEmit = IsInWater && CurrentSpeed > particleSpeedThreshold;
        
        var emission = wakeParticles.emission;
        if (shouldEmit && !wakeParticles.isPlaying)
        {
            wakeParticles.Play();
        }
        else if (!shouldEmit && wakeParticles.isPlaying)
        {
            wakeParticles.Stop();
        }

        // Ajusta intensidade baseado na velocidade
        if (shouldEmit)
        {
            float speedRatio = CurrentSpeed / maxSpeed;
            emission.rateOverTimeMultiplier = speedRatio;
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Para o barco imediatamente
    /// </summary>
    public void Stop()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        currentThrust = 0;
        currentTurn = 0;
    }

    /// <summary>
    /// Define se o barco pode ser controlado
    /// </summary>
    public void SetControllable(bool controllable)
    {
        isControllable = controllable;
        if (!controllable)
        {
            currentThrust = 0;
            currentTurn = 0;
        }
    }

    /// <summary>
    /// Teleporta o barco para uma posição
    /// </summary>
    public void TeleportTo(Vector3 position, Quaternion rotation)
    {
        rb.position = position;
        rb.rotation = rotation;
        Stop();
    }

    /// <summary>
    /// Aplica dano/impacto ao barco
    /// </summary>
    public void ApplyImpact(Vector3 force, Vector3 point)
    {
        rb.AddForceAtPosition(force, point, ForceMode.Impulse);
    }
    #endregion

    #region Gizmos
    private void OnDrawGizmosSelected()
    {
        // Desenha direção forward
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, transform.forward * 3f);

        // Desenha posição do piloto
        if (pilotPosition != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(pilotPosition.position, 0.5f);
        }

        // Desenha velocidade atual (em play mode)
        if (Application.isPlaying && rb != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, rb.linearVelocity);
        }
    }
    #endregion
}
