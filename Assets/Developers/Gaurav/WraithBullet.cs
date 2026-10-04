using UnityEngine;

public class WraithBullet : MonoBehaviour
{
    [Header("Bullet Stats")]
    [SerializeField] private float speed = 80f;
    [SerializeField] private float lifetime = 3f;
    [SerializeField] private float damage = 25f;

    private Vector3 travelDirection;
    private LayerMask collisionMask;
    private float lifeTimer = 0f;
    private bool isInitialized = false;

    public void Initialize(Vector3 direction, LayerMask hitMask, float customSpeed = -1f, float customDamage = -1f)
    {
        travelDirection = direction.normalized;
        collisionMask = hitMask;

        if (customSpeed > 0f) speed = customSpeed;
        if (customDamage > 0f) damage = customDamage;

        // Orient bullet model along its flight path
        transform.forward = travelDirection;
        isInitialized = true;
    }

    void Update()
    {
        if (!isInitialized) return;

        lifeTimer += Time.deltaTime;
        if (lifeTimer >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        float stepDistance = speed * Time.deltaTime;

        // Continuous raycast check prevents tunneling through thin geometry
        if (Physics.Raycast(transform.position, travelDirection, out RaycastHit hit, stepDistance, collisionMask))
        {
            transform.position = hit.point;

            // Damage Boss Hitbox (Routes to Parts, Core weakpoint, or Shield)
            BossHitbox bossHitbox = hit.collider.GetComponentInParent<BossHitbox>();
            if (bossHitbox != null)
            {
                bossHitbox.TakeHit(damage, DamageSource.Player);
            }

            Destroy(gameObject);
        }
        else
        {
            transform.position += travelDirection * stepDistance;
        }
    }
}