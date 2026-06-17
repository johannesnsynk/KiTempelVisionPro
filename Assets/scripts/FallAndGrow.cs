using UnityEngine;
public class FallAndGrow : MonoBehaviour
{
    [Header("Grow")]
    [SerializeField] private Vector3 targetScale = new Vector3(3f, 3f, 3f);
    [SerializeField] private float growDuration = 2f;
    [SerializeField] private float growDelay = 0.5f;
    [Header("Rotation")]
    [SerializeField] private Vector3 rotationSpeed = new Vector3(0f, 20f, 0f);
    [SerializeField] private float rotationFadeIn = 2f;
    [Header("Collider")]
    [SerializeField] private Collider impactCollider;       // solider Collider, für den Aufprall
    [SerializeField] private Collider pickupTriggerCollider; // Trigger Collider, fürs Greifen
    private bool dropped = false;
    private bool growing = false;
    private bool rotating = false;
    private float growTimer = 0f;
    private float rotationTimer = 0f;
    private Vector3 startScale;
    private Rigidbody rb;

    private void Start()
    {
        startScale = transform.localScale;
        rb = GetComponent<Rigidbody>();

        foreach (Collider c in GetComponents<Collider>())
        {
            if (c.isTrigger)
                pickupTriggerCollider = c;
            else
                impactCollider = c;
        }
    }

    public void Drop()
    {
        if (dropped) return;
        dropped = true;

        transform.SetParent(null);
        rb.isKinematic = false;
        rb.useGravity = true;
    }

    public void GrowInPlace()
    {
        if (growing || rotating) return;

        transform.SetParent(null);
        StartGrow();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (growing || rotating) return;

        rb.isKinematic = true;
        rb.useGravity = false;

        if (impactCollider != null)
            impactCollider.enabled = false;

        Invoke(nameof(StartGrow), growDelay);
    }

    private void StartGrow()
    {
        growing = true;
        startScale = transform.localScale;
        growTimer = 0f;

        if (pickupTriggerCollider != null)
            pickupTriggerCollider.enabled = false;
    }

    private void StartRotate()
    {
        rotating = true;
        rotationTimer = 0f;
    }

    private void Update()
    {
        if (growing)
        {
            growTimer += Time.deltaTime;
            float t = Mathf.Clamp01(growTimer / growDuration);
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);

            if (t >= 1f)
            {
                growing = false;
                StartRotate();
            }
        }

        if (rotating)
        {
            rotationTimer += Time.deltaTime;
            float t = Mathf.Clamp01(rotationTimer / rotationFadeIn);
            transform.Rotate(rotationSpeed * t * Time.deltaTime);
        }
    }
}