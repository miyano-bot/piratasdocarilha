using UnityEngine;

/// <summary>
/// Attach to a world item prefab. Holds an ItemDefinition and quantity.
/// PlayerInventory looks for this to pick up items.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PickupItem : MonoBehaviour
{
    public ItemDefinition item;
    public int quantity = 1;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }
}
