using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider))]
public class LavaHazardPatch : MonoBehaviour
{
    private float lifetime = 10.0f;
    private float damagePerTick = 8.0f;
    private float tickInterval = 0.5f;

    private float timer;
    private readonly Dictionary<Collider, float> nextDamageTimePerTarget = new Dictionary<Collider, float>();

    private void Awake()
    {
        gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");

        SphereCollider sc = GetComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 1.25f; // Matches 2.5m ribbon width
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= lifetime)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("BossPart") ||
            other.gameObject.layer == LayerMask.NameToLayer("BossCore"))
        {
            return;
        }

        if (other.CompareTag("Player") || other.gameObject.layer == LayerMask.NameToLayer("Echo"))
        {
            if (!nextDamageTimePerTarget.ContainsKey(other))
                nextDamageTimePerTarget[other] = 0f;

            if (Time.time >= nextDamageTimePerTarget[other])
            {
                other.SendMessage("TakeDamage", damagePerTick, SendMessageOptions.DontRequireReceiver);
                nextDamageTimePerTarget[other] = Time.time + tickInterval;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (nextDamageTimePerTarget.ContainsKey(other))
            nextDamageTimePerTarget.Remove(other);
    }

    /// <summary>
    /// Spawns an invisible trigger node along the ribbon path to tick contact damage.
    /// </summary>
    public static void SpawnDamageNode(Vector3 floorPos, float duration = 10.0f)
    {
        GameObject node = new GameObject("LavaDamageNode");
        node.transform.position = floorPos;
        LavaHazardPatch patch = node.AddComponent<LavaHazardPatch>();
        patch.lifetime = duration;
    }
}