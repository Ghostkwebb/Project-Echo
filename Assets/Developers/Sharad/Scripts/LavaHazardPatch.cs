using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class LavaHazardPatch : MonoBehaviour
{
    [Header("Lava Hazard Tuning (Designer PDF 3 & 4)")]
    [SerializeField] private float lifetime = 10.0f;
    [SerializeField] private float damagePerTick = 8.0f;
    [SerializeField] private float tickInterval = 0.5f;
    [SerializeField] private float patchRadius = 1.6f;
    [SerializeField] private float coolDownFadeTime = 2.0f;

    [Header("Colors (Molten to Cooled)")]
    [SerializeField] private Color moltenColor = new Color(1f, 0.35f, 0.05f, 0.95f);
    [SerializeField] private Color cooledColor = new Color(0.12f, 0.1f, 0.1f, 0f);

    private float timer;
    private Material lavaMaterialInstance;
    private Renderer patchRenderer;
    private Vector3 initialScale;

    private readonly Dictionary<Collider, float> nextDamageTimePerTarget = new Dictionary<Collider, float>();

    private void Awake()
    {
        // Enforce Ignore Raycast layer so laser never hits this puddle!
        gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");

        patchRenderer = GetComponent<Renderer>();
        if (patchRenderer != null)
        {
            lavaMaterialInstance = patchRenderer.material;
            lavaMaterialInstance.color = moltenColor;
        }

        Collider col = GetComponent<Collider>();
        col.isTrigger = true;

        initialScale = new Vector3(patchRadius * 2f, 0.01f, patchRadius * 2f);
        transform.localScale = initialScale;
    }

    private void Update()
    {
        timer += Time.deltaTime;

        // Fading / Cooling in last 2 seconds
        if (timer >= lifetime - coolDownFadeTime)
        {
            float fadeProgress = (timer - (lifetime - coolDownFadeTime)) / coolDownFadeTime;

            if (lavaMaterialInstance != null)
            {
                lavaMaterialInstance.color = Color.Lerp(moltenColor, cooledColor, fadeProgress);
            }

            transform.localScale = Vector3.Lerp(initialScale, initialScale * 0.3f, fadeProgress);
        }

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
            {
                nextDamageTimePerTarget[other] = 0f;
            }

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
        {
            nextDamageTimePerTarget.Remove(other);
        }
    }

    private void OnDestroy()
    {
        if (lavaMaterialInstance != null)
        {
            Destroy(lavaMaterialInstance);
        }
    }

    public static LavaHazardPatch Spawn(Vector3 floorPos, float radius = 1.6f)
    {
        GameObject patchObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        patchObj.name = "LavaHazardPatch";
        patchObj.layer = LayerMask.NameToLayer("Ignore Raycast"); // Zero raycast blockage
        patchObj.transform.position = new Vector3(floorPos.x, floorPos.y, floorPos.z);

        Renderer rend = patchObj.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        }

        LavaHazardPatch patch = patchObj.AddComponent<LavaHazardPatch>();
        return patch;
    }
}