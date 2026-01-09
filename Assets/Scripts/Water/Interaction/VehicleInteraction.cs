#if ENABLE_INPUT_SYSTEM
#define INPUT_SYSTEM_AVAILABLE
#endif

using UnityEngine;
using UnityEngine.Events;
#if INPUT_SYSTEM_AVAILABLE
using UnityEngine.InputSystem;
#endif
using Crest;

/// <summary>
/// Sistema de interação para entrar/sair de veículos (barcos).
/// Adicione este componente ao Player.
/// </summary>
public class VehicleInteraction : MonoBehaviour
{
    [Header("Detection")]
    [Tooltip("Distância máxima para detectar veículos")]
    public float interactionRange = 3f;
    
    [Tooltip("Layer dos veículos")]
    public LayerMask vehicleLayer;
    
    [Tooltip("Tecla para entrar/sair")]
    public KeyCode interactionKey = KeyCode.E;

    [Header("References")]
    [Tooltip("PlayerController a desativar quando pilotando")]
    public PlayerController playerController;
    
    [Tooltip("CharacterController a desativar quando pilotando")]
    public CharacterController characterController;
    
    [Tooltip("Mesh/modelo visual do player")]
    public GameObject playerVisual;

    [Header("Camera")]
    [Tooltip("Se deve mover a câmera para seguir o veículo")]
    public bool moveCameraToVehicle = true;
    
    [Tooltip("Offset da câmera quando pilotando")]
    public Vector3 vehicleCameraOffset = new Vector3(0, 8, -12);

    [Header("Events")]
    public UnityEvent<BoatProbes> onEnterVehicle;
    public UnityEvent onExitVehicle;

    [Header("Debug")]
    public bool showDebugInfo = true;

    // Estado
    private BoatProbes currentVehicle;
    private Transform currentPilotTransform;
    private Transform originalCameraParent;
    private Vector3 originalCameraLocalPos;
    private Quaternion originalCameraLocalRot;
    private Camera mainCamera;

    /// <summary>
    /// Se o player está atualmente pilotando um veículo
    /// </summary>
    public bool IsDriving => currentVehicle != null;

    /// <summary>
    /// Veículo atual sendo pilotado
    /// </summary>
    public BoatProbes CurrentVehicle => currentVehicle;

    private void Start()
    {
        // Auto-detecta componentes se não atribuídos
        if (playerController == null)
            playerController = GetComponent<PlayerController>();
        
        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        mainCamera = Camera.main;
    }

    private void Update()
    {
        bool interactionPressed = false;
#if INPUT_SYSTEM_AVAILABLE
        if (Keyboard.current != null)
        {
            interactionPressed = Keyboard.current.eKey.wasPressedThisFrame;
        }
        else
        {
            // Fallback para input legado se o teclado não for detectado
            interactionPressed = Input.GetKeyDown(interactionKey);
        }
#else
        interactionPressed = Input.GetKeyDown(interactionKey);
#endif

        if (interactionPressed)
        {
            if (IsDriving)
            {
                ExitVehicle();
            }
            else
            {
                TryEnterNearbyVehicle();
            }
        }

        // Atualiza posição do player quando pilotando
        if (IsDriving && currentPilotTransform != null)
        {
            transform.position = currentPilotTransform.position;
            transform.rotation = currentPilotTransform.rotation;
        }
    }

    private void TryEnterNearbyVehicle()
    {
        if (showDebugInfo) Debug.Log("[VehicleInteraction] Tentando entrar em veículo...");

        // Busca veículos próximos
        Collider[] colliders = Physics.OverlapSphere(transform.position, interactionRange, vehicleLayer);
        
        if (showDebugInfo) Debug.Log($"[VehicleInteraction] Encontrados {colliders.Length} colliders na layer {vehicleLayer.value}");

        BoatProbes nearestBoat = null;
        float nearestDistance = float.MaxValue;

        foreach (var col in colliders)
        {
            BoatProbes boat = col.GetComponentInParent<BoatProbes>();
            if (boat != null)
            {
                float dist = Vector3.Distance(transform.position, boat.transform.position);
                if (showDebugInfo) Debug.Log($"[VehicleInteraction] Barco encontrado: {boat.name} a {dist}m");
                if (dist < nearestDistance)
                {
                    nearestDistance = dist;
                    nearestBoat = boat;
                }
            }
            else if (showDebugInfo)
            {
                Debug.Log($"[VehicleInteraction] Collider {col.name} não tem BoatProbes no pai.");
            }
        }

        if (nearestBoat != null)
        {
            EnterVehicle(nearestBoat);
        }
        else
        {
            if (showDebugInfo) Debug.Log("[VehicleInteraction] Nenhum veículo próximo com BoatProbes encontrado.");
        }
    }

    /// <summary>
    /// Entra em um veículo específico
    /// </summary>
    public void EnterVehicle(BoatProbes vehicle)
    {
        if (vehicle == null || IsDriving) return;

        currentVehicle = vehicle;

        // Desativa controles do player
        if (playerController != null)
            playerController.enabled = false;
        
        if (characterController != null)
            characterController.enabled = false;

        // Esconde visual do player (opcional)
        if (playerVisual != null)
            playerVisual.SetActive(false);

        // Busca o pilotPosition (muitos barcos do Crest usam um Transform filho para isso)
        currentPilotTransform = vehicle.transform.Find("PilotPosition");
        if (currentPilotTransform == null) currentPilotTransform = vehicle.transform;

        // Move player para posição do piloto
        transform.position = currentPilotTransform.position;
        transform.rotation = currentPilotTransform.rotation;
        transform.SetParent(vehicle.transform);

        // Ativa controle do veículo no Crest
        vehicle._playerControlled = true;

        // Move câmera para seguir veículo
        if (moveCameraToVehicle && mainCamera != null)
        {
            originalCameraParent = mainCamera.transform.parent;
            originalCameraLocalPos = mainCamera.transform.localPosition;
            originalCameraLocalRot = mainCamera.transform.localRotation;

            mainCamera.transform.SetParent(vehicle.transform);
            mainCamera.transform.localPosition = vehicleCameraOffset;
            mainCamera.transform.LookAt(vehicle.transform);
        }

        onEnterVehicle?.Invoke(vehicle);

        if (showDebugInfo)
            Debug.Log($"[VehicleInteraction] Entrou no veículo: {vehicle.name}");
    }

    /// <summary>
    /// Sai do veículo atual
    /// </summary>
    public void ExitVehicle()
    {
        if (!IsDriving) return;

        BoatProbes vehicle = currentVehicle;

        // Desativa controle do veículo no Crest
        vehicle._playerControlled = false;

        // Calcula posição de saída (ao lado do barco)
        Vector3 exitPosition = vehicle.transform.position + vehicle.transform.right * 3f;
        exitPosition.y = vehicle.transform.position.y + 1f;

        // Desvincula player do veículo
        transform.SetParent(null);
        transform.position = exitPosition;
        transform.rotation = Quaternion.LookRotation(-vehicle.transform.right);

        // Restaura câmera
        if (moveCameraToVehicle && mainCamera != null && originalCameraParent != null)
        {
            mainCamera.transform.SetParent(originalCameraParent);
            mainCamera.transform.localPosition = originalCameraLocalPos;
            mainCamera.transform.localRotation = originalCameraLocalRot;
        }

        // Reativa controles do player
        if (characterController != null)
            characterController.enabled = true;
        
        if (playerController != null)
            playerController.enabled = true;

        // Mostra visual do player
        if (playerVisual != null)
            playerVisual.SetActive(true);

        currentVehicle = null;
        currentPilotTransform = null;

        onExitVehicle?.Invoke();

        if (showDebugInfo)
            Debug.Log($"[VehicleInteraction] Saiu do veículo: {vehicle.name}");
    }

    /// <summary>
    /// Força saída do veículo (para uso externo)
    /// </summary>
    public void ForceExit()
    {
        if (IsDriving)
            ExitVehicle();
    }

    private void OnDrawGizmos()
    {
        // Desenha range de interação (sempre visível em amarelo claro para Debug)
        if (showDebugInfo)
        {
            Gizmos.color = new Color(1, 1, 0, 0.3f);
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Desenha range de interação quando selecionado
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
