using System;
using UnityEngine;

/// <summary>
/// Inventory system with a 10-slot hotbar plus a backpack (Minecraft-style).
/// - Press 1-0 to select hotbar slots.
/// - Press E near a PickupItem to pick up.
/// - Press Q to drop the selected hotbar item (one unit).
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    [Header("Inventory Sizes")]
    public int hotbarSize = 10;
    public int backpackSize = 20;

    [Header("Pickup Settings")]
    public float pickupRange = 3f;
    public LayerMask pickupMask = ~0;
    public Transform dropPoint; // where dropped items appear (defaults to player position)

    [Header("Debug")]
    public bool logInventory = false;

    public struct InventorySlot
    {
        public ItemDefinition item;
        public int quantity;

        public bool IsEmpty => item == null || quantity <= 0;
        public int RemainingSpace => IsEmpty ? (item == null ? 0 : item.maxStack) : Mathf.Max(0, item.maxStack - quantity);
    }

    private InventorySlot[] hotbar;
    private InventorySlot[] backpack;
    private int selectedHotbarIndex = 0;

    // Events for UI
    public event Action OnInventoryChanged;
    public event Action<int> OnHotbarSelectionChanged;

    private void Awake()
    {
        hotbar = new InventorySlot[hotbarSize];
        backpack = new InventorySlot[backpackSize];

        if (dropPoint == null)
            dropPoint = transform;
    }

    private void Update()
    {
        HandleHotbarSelection();

        if (Input.GetKeyDown(KeyCode.E))
            TryPickUpNearby();

        if (Input.GetKeyDown(KeyCode.Q))
            DropSelected(1);
    }

    #region Public API

    public InventorySlot[] GetHotbar() => hotbar;
    public InventorySlot[] GetBackpack() => backpack;
    public int GetSelectedHotbarIndex() => selectedHotbarIndex;
    public ItemDefinition GetSelectedItem() => hotbar != null && hotbar.Length > 0 ? hotbar[selectedHotbarIndex].item : null;

    public bool AddItem(ItemDefinition item, int quantity)
    {
        if (item == null || quantity <= 0) return false;

        int remaining = quantity;

        // Fill existing stacks in hotbar then backpack
        AddToCollection(hotbar, item, ref remaining);
        AddToCollection(backpack, item, ref remaining);

        // Fill empty slots if still remaining
        FillEmptySlots(hotbar, item, ref remaining);
        FillEmptySlots(backpack, item, ref remaining);

        bool fullyAdded = remaining == 0;
        if (fullyAdded)
            NotifyInventoryChanged();
        else if (logInventory)
            Debug.LogWarning($"Inventory full. Could not add {remaining}x {item.displayName}");

        return fullyAdded;
    }

    public bool RemoveFromHotbar(int slotIndex, int quantity)
    {
        if (!IsValidIndex(hotbar, slotIndex) || quantity <= 0) return false;
        ref InventorySlot slot = ref hotbar[slotIndex];
        if (slot.IsEmpty) return false;

        int removed = Mathf.Min(quantity, slot.quantity);
        slot.quantity -= removed;
        if (slot.quantity <= 0)
            slot = default;

        NotifyInventoryChanged();
        return true;
    }

    public void DropSelected(int quantity)
    {
        if (!IsValidIndex(hotbar, selectedHotbarIndex)) return;
        ref InventorySlot slot = ref hotbar[selectedHotbarIndex];
        if (slot.IsEmpty) return;

        int dropAmount = Mathf.Clamp(quantity, 1, slot.quantity);
        SpawnWorldItem(slot.item, dropAmount);
        slot.quantity -= dropAmount;
        if (slot.quantity <= 0)
            slot = default;

        NotifyInventoryChanged();
    }

    #endregion

    #region Input & Pickup

    private void HandleHotbarSelection()
    {
        // Keys 1-0 map to slots 0-9
        for (int i = 0; i < 10; i++)
        {
            KeyCode key = (i == 9) ? KeyCode.Alpha0 : (KeyCode)((int)KeyCode.Alpha1 + i);
            if (Input.GetKeyDown(key))
            {
                SetHotbarIndex(i);
                break;
            }
        }
    }

    private void SetHotbarIndex(int index)
    {
        if (!IsValidIndex(hotbar, index)) return;
        if (selectedHotbarIndex == index) return;
        selectedHotbarIndex = index;
        OnHotbarSelectionChanged?.Invoke(selectedHotbarIndex);
    }

    private void TryPickUpNearby()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, pickupRange, pickupMask);
        PickupItem closest = null;
        float closestDist = float.MaxValue;
        
        foreach (var hit in hits)
        {
            var p = hit.GetComponentInParent<PickupItem>();
            if (p == null) continue;
            float d = Vector3.Distance(transform.position, p.transform.position);
            if (d < closestDist)
            {
                closestDist = d;
                closest = p;
            }
        }

        if (closest != null)
        {
            bool added = AddItem(closest.item, closest.quantity);
            if (added)
                Destroy(closest.gameObject);
        }
        else
        {
            // Fallback: try old "Item" tag for backward compatibility
            TryPickUpLegacyItem(hits);
        }
    }

    private void TryPickUpLegacyItem(Collider[] hits)
    {
        GameObject closest = null;
        float closestDist = float.MaxValue;
        
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Item"))
            {
                float d = Vector3.Distance(transform.position, hit.transform.position);
                if (d < closestDist)
                {
                    closestDist = d;
                    closest = hit.gameObject;
                }
            }
        }

        if (closest != null)
        {
            // Create a temporary ItemDefinition on the fly or just log
            if (logInventory)
                Debug.LogWarning($"Picked up legacy item '{closest.name}' without PickupItem component. Add ItemDefinition + PickupItem to use inventory.");
            
            // Option: Auto-add PickupItem if you have a default item
            var pickup = closest.AddComponent<PickupItem>();
            pickup.item = null; // You'll need to assign this manually or create a default
            pickup.quantity = 1;
            
            // Destroy it so it doesn't pile up
            Destroy(closest);
        }
    }

    #endregion

    #region Internal Helpers

    private void AddToCollection(InventorySlot[] collection, ItemDefinition item, ref int remaining)
    {
        if (remaining <= 0) return;
        for (int i = 0; i < collection.Length && remaining > 0; i++)
        {
            ref InventorySlot slot = ref collection[i];
            if (slot.IsEmpty || slot.item != item) continue;
            int canAdd = Mathf.Min(slot.item.maxStack - slot.quantity, remaining);
            if (canAdd <= 0) continue;
            slot.quantity += canAdd;
            remaining -= canAdd;
        }
    }

    private void FillEmptySlots(InventorySlot[] collection, ItemDefinition item, ref int remaining)
    {
        if (remaining <= 0) return;
        for (int i = 0; i < collection.Length && remaining > 0; i++)
        {
            ref InventorySlot slot = ref collection[i];
            if (!slot.IsEmpty) continue;
            int toAdd = Mathf.Min(item.maxStack, remaining);
            slot.item = item;
            slot.quantity = toAdd;
            remaining -= toAdd;
        }
    }

    private bool IsValidIndex(InventorySlot[] arr, int index)
    {
        return arr != null && index >= 0 && index < arr.Length;
    }

    private void NotifyInventoryChanged()
    {
        OnInventoryChanged?.Invoke();
        if (logInventory)
            DebugInventory();
    }

    private void DebugInventory()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("Hotbar: ");
        for (int i = 0; i < hotbar.Length; i++)
        {
            var s = hotbar[i];
            sb.Append($"[{i}:{(s.item ? s.item.displayName : "-")}/{s.quantity}] ");
        }
        sb.Append(" | Backpack: ");
        for (int i = 0; i < backpack.Length; i++)
        {
            var s = backpack[i];
            if (!s.IsEmpty)
                sb.Append($"[{i}:{s.item.displayName}/{s.quantity}] ");
        }
        Debug.Log(sb.ToString());
    }

    private void SpawnWorldItem(ItemDefinition item, int quantity)
    {
        if (item == null || item.worldPrefab == null) return;
        Vector3 spawnPos = dropPoint != null ? dropPoint.position : transform.position + transform.forward;
        var pickup = Instantiate(item.worldPrefab, spawnPos, Quaternion.identity);
        if (pickup != null)
        {
            pickup.item = item;
            pickup.quantity = quantity;
        }
    }

    #endregion
}
