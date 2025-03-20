using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    public float attackCooldown = 1f; // Time between attacks
    private float lastAttackTime = 0f; // Time when the player last attacked

    public Animator animator;  // The player's animator

    private void Update()
    {
        // Check if the player presses the attack button (left mouse button or 'E')
        if (Input.GetKeyDown(KeyCode.Mouse0) && Time.time >= lastAttackTime + attackCooldown)
        {
            Attack();
        }
    }

    void Attack()
    {
        // Trigger the attack animation
        animator.SetTrigger("Attack");

        // Update the last attack time
        lastAttackTime = Time.time;

        // Optionally, add sound, effects, etc.
    }
}
