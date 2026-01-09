using UnityEngine;
using UnityEngine.UI;
using Crest;

/// <summary>
/// UI simples para mostrar prompt de interação quando perto de veículos.
/// Adicione a um Canvas no Player ou cena.
/// </summary>
public class InteractionPromptUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Referência ao VehicleInteraction do player")]
    public VehicleInteraction vehicleInteraction;
    
    [Header("UI Elements")]
    [Tooltip("Objeto do prompt a mostrar/esconder")]
    public GameObject promptObject;
    
    [Tooltip("Texto do prompt (opcional, usa TextMeshPro ou Text)")]
    public Text promptText;

    [Header("Settings")]
    [Tooltip("Mensagem quando pode entrar")]
    public string enterMessage = "Pressione [E] para entrar";
    
    [Tooltip("Mensagem quando pode sair")]
    public string exitMessage = "Pressione [E] para sair";

    [Header("Detection")]
    [Tooltip("Distância para mostrar prompt")]
    public float promptRange = 4f;
    
    [Tooltip("Layer dos veículos")]
    public LayerMask vehicleLayer;

    private void Start()
    {
        // Auto-encontra VehicleInteraction se não atribuído
        if (vehicleInteraction == null)
        {
            vehicleInteraction = FindAnyObjectByType<VehicleInteraction>();
        }

        // Esconde prompt inicialmente
        if (promptObject != null)
            promptObject.SetActive(false);
    }

    private void Update()
    {
        if (vehicleInteraction == null) return;

        bool shouldShow = false;
        string message = "";

        if (vehicleInteraction.IsDriving)
        {
            // Player está pilotando - mostra "sair"
            shouldShow = true;
            message = exitMessage;
        }
        else
        {
            // Verifica se há veículo próximo
            if (IsVehicleNearby())
            {
                shouldShow = true;
                message = enterMessage;
            }
        }

        // Atualiza UI
        if (promptObject != null)
        {
            promptObject.SetActive(shouldShow);
        }

        if (promptText != null && shouldShow)
        {
            promptText.text = message;
        }
    }

    private bool IsVehicleNearby()
    {
        if (vehicleInteraction == null) return false;

        Collider[] colliders = Physics.OverlapSphere(
            vehicleInteraction.transform.position, 
            promptRange, 
            vehicleLayer
        );

        foreach (var col in colliders)
        {
            if (col.GetComponentInParent<BoatProbes>() != null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Setup rápido em runtime (cria UI se não existir)
    /// </summary>
    public static InteractionPromptUI CreateDefaultUI(VehicleInteraction interaction)
    {
        // Cria Canvas
        GameObject canvasGO = new GameObject("InteractionPromptCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // Cria Panel de fundo
        GameObject panelGO = new GameObject("PromptPanel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        
        RectTransform panelRect = panelGO.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.2f);
        panelRect.anchorMax = new Vector2(0.5f, 0.2f);
        panelRect.sizeDelta = new Vector2(300, 50);
        
        Image panelImage = panelGO.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.7f);

        // Cria Texto
        GameObject textGO = new GameObject("PromptText");
        textGO.transform.SetParent(panelGO.transform, false);
        
        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        
        Text text = textGO.AddComponent<Text>();
        text.text = "Pressione [E] para entrar";
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = 20;
        text.color = Color.white;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Configura componente
        InteractionPromptUI promptUI = canvasGO.AddComponent<InteractionPromptUI>();
        promptUI.vehicleInteraction = interaction;
        promptUI.promptObject = panelGO;
        promptUI.promptText = text;

        panelGO.SetActive(false);

        return promptUI;
    }
}
