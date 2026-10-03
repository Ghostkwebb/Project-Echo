using UnityEngine;

[DisallowMultipleComponent]
public class ShockwaveFX : MonoBehaviour
{
    [Header("Shockwave Timing")]
    [SerializeField] private float lifetime = 0.65f;
    [SerializeField] private float maxDiameter = 22.0f;

    private Material matInstance;
    private float timer;

    private static readonly int DissolveID = Shader.PropertyToID("_Dissolve");
    private static readonly int InnerEdgeID = Shader.PropertyToID("_InnerEdge");
    private static readonly int OuterEdgeID = Shader.PropertyToID("_OuterEdge");

    private void Awake()
    {
        Renderer r = GetComponentInChildren<Renderer>();
        if (r != null)
        {
            matInstance = r.material;
        }
        transform.localScale = Vector3.zero;
    }

    private void Update()
    {
        timer += Time.deltaTime;
        float progress = Mathf.Clamp01(timer / lifetime);

        // 1. Explosive Outward Blast: Fast initial burst, coasting finish (EaseOutExpo)
        float blastExpansion = 1f - Mathf.Pow(2f, -10f * progress);
        float currentDiameter = Mathf.Lerp(2.0f, maxDiameter, blastExpansion);
        transform.localScale = new Vector3(currentDiameter, 1.0f, currentDiameter);

        if (matInstance != null)
        {
            // 2. Wavefront Dynamics: Inner edge pushes outward so the blast ring thins as it expands!
            if (matInstance.HasProperty(InnerEdgeID))
            {
                float innerCutoff = Mathf.Lerp(0.10f, 0.45f, progress);
                matInstance.SetFloat(InnerEdgeID, innerCutoff);
            }

            // 3. Smooth Cross-Dissolve: Starts fading at 40% lifetime, smoothly erodes to 0
            if (matInstance.HasProperty(DissolveID))
            {
                float dissolveProgress = Mathf.Clamp01((progress - 0.40f) / 0.60f);
                // Ease-in dissolve so ring tears into energy shards
                matInstance.SetFloat(DissolveID, dissolveProgress * dissolveProgress);
            }
        }

        // Clean destroy only after full dissolve
        if (timer >= lifetime)
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (matInstance != null) Destroy(matInstance);
    }

    public static void Spawn(Vector3 floorPos, GameObject shockwaveMeshPrefab, Material shockwaveMat)
    {
        GameObject obj;
        if (shockwaveMeshPrefab != null)
        {
            obj = Instantiate(shockwaveMeshPrefab, floorPos, Quaternion.identity);
        }
        else
        {
            obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Collider c = obj.GetComponent<Collider>();
            if (c != null) Destroy(c);
        }

        obj.name = "Stomp_Shockwave_Burst";

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (shockwaveMat != null) renderers[i].material = shockwaveMat;
        }

        obj.AddComponent<ShockwaveFX>();
    }
}