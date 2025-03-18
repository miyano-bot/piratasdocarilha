using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    public Transform itemHoldPoint;
    private GameObject heldItem;
    
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            TryPickUpItem();
        }
        
        if (Input.GetKeyDown(KeyCode.Q))
        {
            DropItem();
        }
    }
    
    public void EquipItem(GameObject item)
    {
        if (heldItem != null)
        {
            DropItem();
        }
        
        heldItem = item;
        
        heldItem.transform.SetParent(itemHoldPoint);
        heldItem.transform.localPosition = Vector3.zero;
        heldItem.transform.localRotation = Quaternion.identity;
        
        Rigidbody itemRb = heldItem.GetComponent<Rigidbody>();
        if (itemRb != null)
        {
            itemRb.isKinematic = true;
        }
        
        Collider[] colliders = heldItem.GetComponents<Collider>();
        foreach (Collider collider in colliders)
        {
            collider.enabled = false;
        }
    }
    
    public void DropItem()
    {
        if (heldItem != null)
        {
            Rigidbody itemRb = heldItem.GetComponent<Rigidbody>();
            if (itemRb != null)
            {
                itemRb.isKinematic = false;
            }
            
            Collider[] colliders = heldItem.GetComponents<Collider>();
            foreach (Collider collider in colliders)
            {
                collider.enabled = true;
            }
            
            heldItem.transform.SetParent(null);
            heldItem = null;
        }
    }
    
    private void TryPickUpItem()
    {
        float pickupRange = 2f;
        Collider[] colliders = Physics.OverlapSphere(transform.position, pickupRange);
        
        GameObject closestItem = null;
        float closestDistance = float.MaxValue;
        
        foreach (Collider collider in colliders)
        {
            if (collider.CompareTag("Item"))
            {
                float distance = Vector3.Distance(transform.position, collider.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestItem = collider.gameObject;
                }
            }
        }
        
        if (closestItem != null)
        {
            EquipItem(closestItem);
        }
    }
}