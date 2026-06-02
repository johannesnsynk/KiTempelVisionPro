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
    }

    public void Drop()
    {
        transform.SetParent(null);
        rb.isKinematic = false;
        rb.useGravity = true;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (growing || rotating) return;
        Invoke(nameof(StartGrow), growDelay);
    }

    private void StartGrow()
    {
        growing = true;
        startScale = transform.localScale;
        growTimer = 0f;
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