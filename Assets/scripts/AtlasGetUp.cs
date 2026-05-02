using UnityEngine;

/// <summary>
/// Attach to At (parent of atlas).
/// After gravity is enabled by RocketImpactDetacher,
/// waits a delay then re-enables the Animator and plays the get-up animation.
/// </summary>
public class AtlasGetUp : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Animator on atlas or parent A (auto-found in children if empty)")]
    public Animator animator;

    [Tooltip("Rigidbody on atlas (auto-found in children if empty)")]
    public Rigidbody rb;

    [Header("Timing")]
    [Tooltip("Seconds to wait after impact before getting up")]
    public float getUpDelay = 2.5f;

    [Header("Animation")]
    [Tooltip("Leave empty to continue current animation. Or set a clip name to play a specific one (uses Animator.Play).")]
    public string playClipName = "";

    private bool _falling = false;

    private void Awake()
    {
        // Animator is on At itself
        if (animator == null)
            animator = GetComponent<Animator>();

        // Rigidbody is on atlas child
        if (rb == null)
            rb = GetComponentInChildren<Rigidbody>();
    }

    /// <summary>
    /// Called by RocketImpactDetacher after gravity is enabled.
    /// </summary>
    public void StartGetUpSequence()
    {
        if (_falling) return;
        _falling = true;
        Invoke(nameof(GetUp), getUpDelay);
    }

    private void GetUp()
    {
        if (rb != null)
        {
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic     = true;
            rb.useGravity      = false;
        }

        if (animator != null)
        {
            animator.enabled = true;

            if (!string.IsNullOrEmpty(playClipName))
                animator.Play(playClipName, 0, 0f);
            // else: continues exactly where it left off
        }

        _falling = false;
        Debug.Log("AtlasGetUp: Atlas is getting up.", this);
    }

    /// <summary>
    /// Call from Animation Event at the END of the get-up clip.
    /// </summary>
    public void OnGetUpComplete()
    {
        Debug.Log("AtlasGetUp: Get-up animation complete.", this);
    }
}
