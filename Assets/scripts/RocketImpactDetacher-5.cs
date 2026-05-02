using UnityEngine;

public class RocketImpactDetacher : MonoBehaviour
{
    [Header("Rocket")]
    public GameObject rocket;
    public Rigidbody rocketRigidbody;
    public Vector3 launchForce = new Vector3(0f, -500f, 0f);
    public bool detachOnLaunch = true;

    [Header("Impact References")]
    public GameObject earth2;
    public GameObject atlasPrefab;
    public Animator atlasAnimator;
    public AtlasGetUp atlasGetUp;  // drag At here directly
    public string earth2Tag = "Earth2";

    [Header("Detachment Settings")]
    public bool maintainWorldTransform = true;
    public Vector3 earth2ImpactForce   = Vector3.zero;
    public Vector3 atlasImpactForce    = Vector3.zero;
    public ForceMode forceMode         = ForceMode.Impulse;

    private bool _triggered = false;

    private void Awake()
    {
        if (rocket == null)
        {
            Transform found = transform.Find("kinzhal");
            if (found == null) found = transform.Find("kinzhal 1");
            if (found != null)
                rocket = found.gameObject;
            else
                Debug.LogWarning("RocketImpactDetacher: rocket not found in children!", this);
        }

        if (rocketRigidbody == null && rocket != null)
            rocketRigidbody = rocket.GetComponent<Rigidbody>();

        if (atlasAnimator == null && atlasPrefab != null)
            atlasAnimator = atlasPrefab.GetComponentInParent<Animator>();

        // Auto-find AtlasGetUp if not assigned
        if (atlasGetUp == null && atlasPrefab != null)
            atlasGetUp = atlasPrefab.GetComponentInParent<AtlasGetUp>();
    }

    private void Start()
    {
        if (rocket == null) return;
        var listener = rocket.AddComponent<RocketCollisionListener>();
        listener.owner = this;
    }

    public void LaunchRocket()
    {
        if (rocketRigidbody == null)
        {
            Debug.LogError("RocketImpactDetacher: No Rigidbody on rocket!", this);
            return;
        }

        if (detachOnLaunch && rocket != null)
            rocket.transform.SetParent(null, true);

        rocketRigidbody.isKinematic = false;
        rocketRigidbody.useGravity  = true;
        rocketRigidbody.AddForce(launchForce, ForceMode.Impulse);

        Debug.Log("RocketImpactDetacher: rocket launched.", this);
    }

    internal void OnRocketHitEarth2(GameObject hit)
    {
        if (_triggered) return;

        bool hitEarth2 = (earth2 != null && hit == earth2)
                      || (!string.IsNullOrEmpty(earth2Tag)
                          && hit.CompareTag(earth2Tag));

        if (!hitEarth2) return;
        _triggered = true;

        Debug.Log("RocketImpactDetacher: Impact on Earth2!", this);

        DetachAndEnableGravity(earth2,      earth2ImpactForce);
        DetachAndEnableGravity(atlasPrefab, atlasImpactForce);

        if (atlasGetUp != null)
        {
            Debug.Log("RocketImpactDetacher: calling StartGetUpSequence.", this);
            atlasGetUp.StartGetUpSequence();
        }
        else
        {
            Debug.LogWarning("RocketImpactDetacher: AtlasGetUp not found!", this);
        }
    }

    private void DetachAndEnableGravity(GameObject obj, Vector3 force)
    {
        if (obj == null)
        {
            Debug.LogWarning("RocketImpactDetacher: reference not assigned, skipping.", this);
            return;
        }

        obj.transform.SetParent(null, maintainWorldTransform);

        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity  = true;

            if (force != Vector3.zero)
                rb.AddForce(force, forceMode);
        }

        Debug.Log($"RocketImpactDetacher: detached '{obj.name}' and enabled gravity.", obj);
    }
}

[AddComponentMenu("")]
internal class RocketCollisionListener : MonoBehaviour
{
    internal RocketImpactDetacher owner;

    private void OnCollisionEnter(Collision collision)
    {
        owner?.OnRocketHitEarth2(collision.gameObject);
    }
}
