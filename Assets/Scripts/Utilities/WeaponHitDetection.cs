using UnityEngine;

public class WeaponHitDetection : MonoBehaviour
{
    public float damage = 10f;  // Damage value that the weapon deals
    public GameObject damageTextPrefab; // Reference to the damage text prefab
    public float damageTextDuration = 1f; // How long the damage text stays visible
    public Vector3 damageTextOffset = new Vector3(0, 2, 0); // Offset position for the damage text

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Trigger entered with: " + other.name);

        if (other.CompareTag("Enemy"))
        {
            Debug.Log("Enemy detected: " + other.name);

            // Get the EnemyHealth component from the enemy object
            EnemyHealth enemyHealth = other.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                Debug.Log("EnemyHealth component found. Applying damage.");
                // Apply damage (casting to int to match the expected type in TakeDamage)
                enemyHealth.TakeDamage((int)damage);  

                // Display damage above the enemy's head
                DisplayDamage(other.transform.position, damage);
            }
            else
            {
                Debug.LogWarning("EnemyHealth component not found on: " + other.name);
            }
        }
    }

    // Method to display the damage above the enemy's head
    private void DisplayDamage(Vector3 enemyPosition, float damageAmount)
    {
        if (damageTextPrefab != null)
        {
            // Spawn the damage text prefab above the enemy
            GameObject damageText = Instantiate(damageTextPrefab, enemyPosition + damageTextOffset, Quaternion.identity);

            // Get the Text component in the prefab and update its text
            UnityEngine.UI.Text textComponent = damageText.GetComponentInChildren<UnityEngine.UI.Text>();
            if (textComponent != null)
            {
                textComponent.text = damageAmount.ToString(); // Set the damage value
            }

            // Optionally, destroy the damage text after a set duration
            Destroy(damageText, damageTextDuration);
        }
        else
        {
            Debug.LogWarning("Damage Text Prefab is not assigned in the WeaponHitDetection script.");
        }
    }
}
