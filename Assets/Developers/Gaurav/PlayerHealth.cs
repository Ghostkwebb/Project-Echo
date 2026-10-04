using UnityEngine;

[DisallowMultipleComponent]
public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;
    public float CurrentHealth => currentHealth;
    public bool IsDead => currentHealth <= 0f;

    private void Awake() => ResetHealth();

    public void TakeDamage(float amount)
    {
        if (IsDead) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        Debug.Log($"Player HP: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0f) Die();
    }

    private void Die()
    {
        Debug.Log("Player Died!");
        // Triggers the round reset loop!
        if (EncounterManager.Instance != null)
        {
            EncounterManager.Instance.TriggerPlayerDeath();
        }
    }

    public void ResetHealth() => currentHealth = maxHealth;
}