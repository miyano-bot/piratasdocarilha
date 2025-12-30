using UnityEngine;
using UnityEngine.Events;

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
    public UnityEvent<BoatController> onEnterVehicle;
    public UnityEvent onExitVehicle;

    [Header("Debug")]
    public bool showDebugInfo = false;

    // Estado
    private BoatController currentVehicle;
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
    public BoatController CurrentVehicle => currentVehicle;

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
        if (Input.GetKeyDown(interactionKey))
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
        if (IsDriving && currentVehicle.pilotPosition != null)
        {
            transform.position = currentVehicle.pilotPosition.position;
            transform.rotation = currentVehicle.pilotPosition.rotation;
        }
    }

    private void TryEnterNearbyVehicle()
    {
        // Busca veículos próximos
        Collider[] colliders = Physics.OverlapSphere(transform.position, interactionRange, vehicleLayer);
        
        BoatController nearestBoat = null;
        float nearestDistance = float.MaxValue;

        foreach (var col in colliders)
        {
            BoatController boat = col.GetComponentInParent<BoatController>();
            if (boat != null)
            {
                float dist = Vector3.Distance(transform.position, boat.transform.position);
                if (dist < nearestDistance)
                {
                    nearestDistance = dist;
                    nearestBoat = boat;
                }
            }
        }

        if (nearestBoat != null)
        {
            EnterVehicle(nearestBoat);
        }
        else if (showDebugInfo)
        {
            Debug.Log("[VehicleInteraction] Nenhum veículo próximo encontrado");
        }
    }

    /// <summary>
    /// Entra em um veículo específico
    /// </summary>
    public void EnterVehicle(BoatController vehicle)
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

        // Move player para posição do piloto
        if (vehicle.pilotPosition != null)
        {
            transform.position = vehicle.pilotPosition.position;
            transform.rotation = vehicle.pilotPosition.rotation;
            transform.SetParent(vehicle.transform);
        }

        // Ativa controle do veículo
        vehicle.SetControllable(true);

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

        BoatController vehicle = currentVehicle;

        // Desativa controle do veículo
        vehicle.SetControllable(false);

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

    private void OnDrawGizmosSelected()
    {
        // Desenha range de interação
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
