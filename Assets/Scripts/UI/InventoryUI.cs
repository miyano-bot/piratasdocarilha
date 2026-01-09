using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Binds PlayerInventory data to UI (hotbar + backpack).
/// Drag references in the Inspector: icons, counts, selection highlights.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("References")]
    public PlayerInventory inventory;

    [Header("Hotbar")]
    public List<Image> hotbarIcons = new();
    public List<TextMeshProUGUI> hotbarCounts = new();
    public List<GameObject> hotbarSelection = new();

    [Header("Backpack")]
    public List<Image> backpackIcons = new();
    public List<TextMeshProUGUI> backpackCounts = new();

    private void OnEnable()
    {
        if (inventory == null)
            inventory = FindAnyObjectByType<PlayerInventory>();

        if (inventory != null)
        {
            inventory.OnInventoryChanged += Refresh;
            inventory.OnHotbarSelectionChanged += UpdateSelection;
            Refresh();
        }
    }

    private void OnDisable()
    {
        if (inventory != null)
        {
            inventory.OnInventoryChanged -= Refresh;
            inventory.OnHotbarSelectionChanged -= UpdateSelection;
        }
    }

    private void Refresh()
    {
        if (inventory == null) return;
        var hotbar = inventory.GetHotbar();
        var backpack = inventory.GetBackpack();
        
        if (hotbar == null || backpack == null) return;

        // Hotbar
        if (hotbarIcons != null)
        {
            for (int i = 0; i < hotbarIcons.Count; i++)
            {
                var icon = hotbarIcons[i];
                if (icon == null) continue;
                var count = i < hotbarCounts.Count ? hotbarCounts[i] : null;
                if (i < hotbar.Length && !hotbar[i].IsEmpty)
                {
                    icon.enabled = true;
                    icon.sprite = hotbar[i].item.icon;
                    if (count != null)
                        count.text = hotbar[i].quantity > 1 ? hotbar[i].quantity.ToString() : "";
                }
                else
                {
                    icon.enabled = false;
                    if (count != null) count.text = "";
                }
            }
        }

        // Backpack
        if (backpackIcons != null)
        {
            for (int i = 0; i < backpackIcons.Count; i++)
            {
                var icon = backpackIcons[i];
                if (icon == null) continue;
                var count = i < backpackCounts.Count ? backpackCounts[i] : null;
                if (i < backpack.Length && !backpack[i].IsEmpty)
                {
                    icon.enabled = true;
                    icon.sprite = backpack[i].item.icon;
                    if (count != null)
                        count.text = backpack[i].quantity > 1 ? backpack[i].quantity.ToString() : "";
                }
                else
                {
                    icon.enabled = false;
                    if (count != null) count.text = "";
                }
            }
        }

        UpdateSelection(inventory.GetSelectedHotbarIndex());
    }

    private void UpdateSelection(int index)
    {
        if (hotbarSelection == null) return;
        for (int i = 0; i < hotbarSelection.Count; i++)
        {
            if (hotbarSelection[i] != null)
                hotbarSelection[i].SetActive(i == index);
        }
    }
}
