using UnityEngine;

[CreateAssetMenu(fileName = "ItemDefinition", menuName = "Inventory/Item Definition", order = 0)]
public class ItemDefinition : ScriptableObject
{
    [Header("Identity")]
    public string id = "item_id";
    public string displayName = "Item";

    [Header("Visuals")]
    public Sprite icon;

    [Header("World Prefab")]
    [Tooltip("Prefab dropped/spawned in the world; should include a PickupItem component.")]
    public PickupItem worldPrefab;

    [Header("Stacking")]
    public int maxStack = 64;

    // Helper property for backward compatibility or direct GameObject access
    public GameObject WorldPrefabGameObject => worldPrefab != null ? worldPrefab.gameObject : null;
}
