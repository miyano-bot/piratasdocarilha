using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    // Replace itemHoldPoint with swordAnchor
    public Transform swordAnchor;  // The bone or point where the sword will be held
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

        // Parent the item to the swordAnchor instead of itemHoldPoint
        heldItem.transform.SetParent(swordAnchor);
        heldItem.transform.localPosition = Vector3.zero;  // Adjust if necessary for positioning
        heldItem.transform.localRotation = Quaternion.identity;  // Adjust if necessary for rotation

        Rigidbody itemRb = heldItem.GetComponent<Rigidbody>();
        if (itemRb != null)
        {
            itemRb.isKinematic = true;
        }

        Collider[] colliders = heldItem.GetComponents<Collider>();
        foreach (Collider collider in colliders)
        {
            collider.enabled = true;  // Disable the collider while holding the item
        }
    }

    public void DropItem()
    {
        if (heldItem != null)
        {
            Rigidbody itemRb = heldItem.GetComponent<Rigidbody>();
            if (itemRb != null)
            {
                itemRb.isKinematic = false;  // Reactivate physics when dropped
            }

            Collider[] colliders = heldItem.GetComponents<Collider>();
            foreach (Collider collider in colliders)
            {
                collider.enabled = true;  // Enable collider when dropped
            }

            heldItem.transform.SetParent(null);  // Remove parent
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
