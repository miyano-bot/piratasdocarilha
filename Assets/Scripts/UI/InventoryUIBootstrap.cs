using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Auto-creates a simple Canvas with hotbar (10 slots) + backpack (20 slots) and wires it to InventoryUI.
/// Drop this on any GameObject (or an empty) in the scene and press Play.
/// </summary>
public class InventoryUIBootstrap : MonoBehaviour
{
    [Header("Layout")]
    public Vector2 referenceResolution = new Vector2(1920, 1080);
    public Vector2 hotbarSlotSize = new Vector2(72, 72);
    public Vector2 backpackSlotSize = new Vector2(64, 64);

    public Color slotBackground = new Color(0.08f, 0.08f, 0.1f, 0.7f);
    public Color selectionColor = new Color(1f, 0.85f, 0.2f, 0.45f);

    private const int HotbarCount = 10;
    private const int BackpackCount = 20;

    private void Start()
    {
        BuildUI();
    }

    private void BuildUI()
    {
        // Ensure EventSystem exists
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(es);
        }

        // Canvas root
        var canvasGO = new GameObject("InventoryCanvas_Auto", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        // InventoryUI component
        var inventoryUI = canvasGO.AddComponent<InventoryUI>();
        if (inventoryUI.inventory == null)
            inventoryUI.inventory = FindAnyObjectByType<PlayerInventory>();

        // Build Hotbar (bottom center)
        var hotbarRoot = CreateUIObject("Hotbar", canvasGO.transform);
        var hotbarRect = hotbarRoot.GetComponent<RectTransform>();
        hotbarRect.anchorMin = new Vector2(0.5f, 0f);
        hotbarRect.anchorMax = new Vector2(0.5f, 0f);
        hotbarRect.pivot = new Vector2(0.5f, 0f);
        hotbarRect.anchoredPosition = new Vector2(0f, 24f);

        var hotbarLayout = hotbarRoot.AddComponent<HorizontalLayoutGroup>();
        hotbarLayout.spacing = 6f;
        hotbarLayout.childAlignment = TextAnchor.MiddleCenter;
        hotbarLayout.childForceExpandHeight = false;
        hotbarLayout.childForceExpandWidth = false;

        inventoryUI.hotbarIcons = new List<Image>(HotbarCount);
        inventoryUI.hotbarCounts = new List<TextMeshProUGUI>(HotbarCount);
        inventoryUI.hotbarSelection = new List<GameObject>(HotbarCount);

        for (int i = 0; i < HotbarCount; i++)
        {
            var slot = CreateSlot(hotbarRoot.transform, hotbarSlotSize, out var icon, out var count, out var highlight);
            slot.name = $"HotbarSlot_{i}";
            inventoryUI.hotbarIcons.Add(icon);
            inventoryUI.hotbarCounts.Add(count);
            inventoryUI.hotbarSelection.Add(highlight);
        }

        // Build Backpack (bottom left)
        var backpackRoot = CreateUIObject("Backpack", canvasGO.transform);
        var backpackRect = backpackRoot.GetComponent<RectTransform>();
        backpackRect.anchorMin = new Vector2(0f, 0f);
        backpackRect.anchorMax = new Vector2(0f, 0f);
        backpackRect.pivot = new Vector2(0f, 0f);
        backpackRect.anchoredPosition = new Vector2(24f, 24f);

        var grid = backpackRoot.AddComponent<GridLayoutGroup>();
        grid.cellSize = backpackSlotSize;
        grid.spacing = new Vector2(6f, 6f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5; // 5 columns x 4 rows = 20 slots

        inventoryUI.backpackIcons = new List<Image>(BackpackCount);
        inventoryUI.backpackCounts = new List<TextMeshProUGUI>(BackpackCount);

        for (int i = 0; i < BackpackCount; i++)
        {
            var slot = CreateSlot(backpackRoot.transform, backpackSlotSize, out var icon, out var count, out _);
            slot.name = $"BackpackSlot_{i}";
            inventoryUI.backpackIcons.Add(icon);
            inventoryUI.backpackCounts.Add(count);
        }

        // Refresh UI initially
        inventoryUI.enabled = true;
    }

    private GameObject CreateSlot(Transform parent, Vector2 size, out Image icon, out TextMeshProUGUI count, out GameObject highlight)
    {
        var slotGO = CreateUIObject("Slot", parent);
        var rect = slotGO.GetComponent<RectTransform>();
        rect.sizeDelta = size;

        // Background
        var bg = slotGO.AddComponent<Image>();
        bg.color = slotBackground;

        // Icon
        var iconGO = CreateUIObject("Icon", slotGO.transform);
        var iconRect = iconGO.GetComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = size * 0.8f;
        icon = iconGO.AddComponent<Image>();
        icon.enabled = false;

        // Count text
        var countGO = CreateUIObject("Count", slotGO.transform);
        var countRect = countGO.GetComponent<RectTransform>();
        countRect.anchorMin = new Vector2(1f, 0f);
        countRect.anchorMax = new Vector2(1f, 0f);
        countRect.pivot = new Vector2(1f, 0f);
        countRect.anchoredPosition = new Vector2(-6f, 6f);
        countRect.sizeDelta = new Vector2(size.x * 0.5f, size.y * 0.4f);
        count = countGO.AddComponent<TextMeshProUGUI>();
        count.fontSize = 22;
        count.alignment = TextAlignmentOptions.BottomRight;
        count.enableAutoSizing = true;
        count.text = "";

        // Selection highlight
        highlight = CreateUIObject("Highlight", slotGO.transform);
        var hRect = highlight.GetComponent<RectTransform>();
        hRect.anchorMin = hRect.anchorMax = hRect.pivot = new Vector2(0.5f, 0.5f);
        hRect.sizeDelta = size + new Vector2(6f, 6f);
        var hImg = highlight.AddComponent<Image>();
        hImg.color = selectionColor;
        highlight.SetActive(false);

        return slotGO;
    }

    private GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, worldPositionStays: false);
        var rect = go.GetComponent<RectTransform>();
        rect.localScale = Vector3.one;
        rect.localPosition = Vector3.zero;
        rect.anchoredPosition = Vector2.zero;
        return go;
    }
}
