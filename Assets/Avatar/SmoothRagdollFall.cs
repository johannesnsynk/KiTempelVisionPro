using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using NSYNK.HyperSlides.Runtime;

[RequireComponent(typeof(Animator))]
public class RagdollIntroSequence : MonoBehaviour
{
    [Header("References")]
    public PlayableDirector timelineDirector;
    public Animator animator;

    [Header("Drop Settings")]
    public float dropHeight = 150f;
    public Vector3 initialImpulse = Vector3.zero;

    [Header("Fall Tuning")]
    public float fallLinearDrag = 0.5f;
    [Tooltip("Lower this (e.g. 0.3) to let limbs swing more freely during the fall.")]
    public float fallAngularDrag = 0.3f;
    public float maxFallSpeed = 12f;

    [Header("Fall Liveliness")]
    [Tooltip("Random angular velocity range applied to each ragdoll body at the start of the fall. Higher = more flailing.")]
    public float initialAngularSpin = 2f;

    [Tooltip("Continuous random force applied to limbs during the fall, simulating air buffeting. 0 to disable.")]
    public float airBuffetForce = 0.5f;

    [Tooltip("Upward 'air resistance' force applied to extremities (hands, feet, head). Creates the classic skydiver silhouette. 0 to disable.")]
    public float extremityLift = 1.5f;

    [Tooltip("How often (in seconds) to apply random buffeting forces. Lower = more chaotic motion.")]
    public float buffetInterval = 0.15f;

    [Header("Force Exclusions")]
    [Tooltip("Skip liveliness forces on the left arm (broken Character Joint causes wild spinning).")]
    public bool excludeLeftArm = true;

    [Tooltip("Skip liveliness forces on the right arm.")]
    public bool excludeRightArm = false;

    [Header("Settle Detection")]
    public float settleVelocityThreshold = 0.05f;
    public float settleTime = 0.75f;
    public float maxRagdollTime = 25f;

    [Header("Pose Blend")]
    public bool useBlend = true;
    public float blendDuration = 0.4f;

    [Header("Transition")]
    public float delayBeforeTimeline = 0.1f;

    [Header("Debug")]
    public bool debugLogs = true;

    private Rigidbody[] ragdollBodies;
    private CharacterController characterController;
    private Transform hipsBone;

    private Transform[] boneTransforms;
    private Vector3[] boneBindLocalPositions;
    private Quaternion[] boneBindLocalRotations;

    private float[] originalLinearDrag;
    private float[] originalAngularDrag;

    private HashSet<Transform> excludedFromForces = new HashSet<Transform>();

    private bool falling = false;
    private Vector3 originalScenePosition;
    private Quaternion originalSceneRotation;

    private Coroutine introCoroutine;

    void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (timelineDirector == null) timelineDirector = GetComponent<PlayableDirector>();

        // Store once — never overwrite
        originalScenePosition = transform.position;
        originalSceneRotation = transform.rotation;

        ragdollBodies = GetComponentsInChildren<Rigidbody>();
        characterController = GetComponent<CharacterController>();

        if (animator != null && animator.isHuman)
            hipsBone = animator.GetBoneTransform(HumanBodyBones.Hips);
        if (hipsBone == null && ragdollBodies.Length > 0) hipsBone = ragdollBodies[0].transform;

        boneTransforms         = new Transform[ragdollBodies.Length];
        boneBindLocalPositions = new Vector3[ragdollBodies.Length];
        boneBindLocalRotations = new Quaternion[ragdollBodies.Length];
        originalLinearDrag     = new float[ragdollBodies.Length];
        originalAngularDrag    = new float[ragdollBodies.Length];

        for (int i = 0; i < ragdollBodies.Length; i++)
        {
            boneTransforms[i]         = ragdollBodies[i].transform;
            boneBindLocalPositions[i] = boneTransforms[i].localPosition;
            boneBindLocalRotations[i] = boneTransforms[i].localRotation;
            originalLinearDrag[i]     = ragdollBodies[i].linearDamping;
            originalAngularDrag[i]    = ragdollBodies[i].angularDamping;
        }

        BuildExclusionSet();

        if (timelineDirector != null)
            timelineDirector.playOnAwake = false;
    }

    void BuildExclusionSet()
    {
        excludedFromForces.Clear();
        if (animator == null || !animator.isHuman) return;

        if (excludeLeftArm)
        {
            AddIfExists(excludedFromForces, animator.GetBoneTransform(HumanBodyBones.LeftUpperArm));
            AddIfExists(excludedFromForces, animator.GetBoneTransform(HumanBodyBones.LeftLowerArm));
            AddIfExists(excludedFromForces, animator.GetBoneTransform(HumanBodyBones.LeftHand));
        }
        if (excludeRightArm)
        {
            AddIfExists(excludedFromForces, animator.GetBoneTransform(HumanBodyBones.RightUpperArm));
            AddIfExists(excludedFromForces, animator.GetBoneTransform(HumanBodyBones.RightLowerArm));
            AddIfExists(excludedFromForces, animator.GetBoneTransform(HumanBodyBones.RightHand));
        }
    }

    void Start()
    {
        // Start() only runs once — actual sequencing handled by OnEnable
    }

    void OnEnable()
    {
        // OnEnable: only subscribe, no Rebind here to avoid pop
        var slideElement = GetComponentInParent<XRSlideElement>();
        if (slideElement != null)
        {
            if (debugLogs) Debug.Log("[RagdollIntro] XRSlideElement gefunden — warte auf FullyVisible.");
            slideElement.OnVisibilityStateChanged += OnSlideVisibilityChanged;
        }
        else
        {
            if (debugLogs) Debug.Log("[RagdollIntro] Kein XRSlideElement — starte sofort.");
            introCoroutine = StartCoroutine(RunIntroSequence());
        }
    }

    void OnDisable()
    {
        if (introCoroutine != null)
        {
            StopCoroutine(introCoroutine);
            introCoroutine = null;
        }

        var slideElement = GetComponentInParent<XRSlideElement>();
        if (slideElement != null)
            slideElement.OnVisibilityStateChanged -= OnSlideVisibilityChanged;

        // Full reset on disable — Rebind here clears root motion for next cycle
        HardReset();
    }

    /// <summary>
    /// Full reset including Rebind — call only on disable, not on enable,
    /// to avoid the animator snap/pop being visible.
    /// </summary>
    void HardReset()
    {
        falling = false;

        if (timelineDirector != null) timelineDirector.Stop();
        if (characterController != null) characterController.enabled = true;

        if (ragdollBodies == null) return;

        for (int i = 0; i < ragdollBodies.Length; i++)
        {
            if (ragdollBodies[i] == null) continue;
            ragdollBodies[i].isKinematic = true;
            ragdollBodies[i].linearVelocity = Vector3.zero;
            ragdollBodies[i].angularVelocity = Vector3.zero;
            ragdollBodies[i].linearDamping = originalLinearDrag[i];
            ragdollBodies[i].angularDamping = originalAngularDrag[i];
        }

        if (boneTransforms != null)
        {
            for (int i = 0; i < boneTransforms.Length; i++)
            {
                if (boneTransforms[i] == null) continue;
                boneTransforms[i].localPosition = boneBindLocalPositions[i];
                boneTransforms[i].localRotation = boneBindLocalRotations[i];
            }
        }

        // Reset root
        transform.position = originalScenePosition;
        transform.rotation = originalSceneRotation;

        if (hipsBone != null)
        {
            hipsBone.localPosition = Vector3.zero;
            hipsBone.localRotation = Quaternion.identity;
        }

        // Rebind clears accumulated root motion — safe here because object is inactive
        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.enabled = true;
            animator.Rebind();
            animator.Update(0f);
        }
    }

    void OnSlideVisibilityChanged(XRSlideElement.VisibilityState state)
    {
        if (debugLogs) Debug.Log($"[RagdollIntro] SlideElement Visibility: {state}");

        if (state == XRSlideElement.VisibilityState.FullyVisible)
        {
            var slideElement = GetComponentInParent<XRSlideElement>();
            if (slideElement != null)
                slideElement.OnVisibilityStateChanged -= OnSlideVisibilityChanged;

            if (debugLogs) Debug.Log("[RagdollIntro] FullyVisible — starte Sequenz.");
            introCoroutine = StartCoroutine(RunIntroSequence());
        }
    }

    void FixedUpdate()
    {
        if (!falling || maxFallSpeed <= 0f) return;

        foreach (var rb in ragdollBodies)
        {
            if (rb.linearVelocity.magnitude > maxFallSpeed)
                rb.linearVelocity = rb.linearVelocity.normalized * maxFallSpeed;
        }
    }

    IEnumerator RunIntroSequence()
    {
        // Drop from above originalScenePosition
        transform.position = new Vector3(
            originalScenePosition.x,
            originalScenePosition.y + dropHeight,
            originalScenePosition.z
        );

        if (debugLogs) Debug.Log($"[RagdollIntro] Dropping from height {dropHeight}");

        EnableRagdoll();
        falling = true;

        if (initialImpulse != Vector3.zero)
        {
            foreach (var rb in ragdollBodies)
                rb.AddForce(initialImpulse, ForceMode.VelocityChange);
        }

        if (initialAngularSpin > 0f)
        {
            foreach (var rb in ragdollBodies)
            {
                if (excludedFromForces.Contains(rb.transform)) continue;
                rb.angularVelocity = new Vector3(
                    Random.Range(-initialAngularSpin, initialAngularSpin),
                    Random.Range(-initialAngularSpin, initialAngularSpin),
                    Random.Range(-initialAngularSpin, initialAngularSpin)
                );
            }
        }

        Coroutine buffetCoroutine = null;
        if (airBuffetForce > 0f || extremityLift > 0f)
            buffetCoroutine = StartCoroutine(ApplyAirBuffet());

        // Wait for settle
        float ragdollTimer = 0f;
        float settleTimer = 0f;

        while (ragdollTimer < maxRagdollTime)
        {
            ragdollTimer += Time.deltaTime;

            float maxVel = 0f;
            foreach (var rb in ragdollBodies)
                maxVel = Mathf.Max(maxVel, rb.linearVelocity.magnitude);

            if (maxVel < settleVelocityThreshold)
            {
                settleTimer += Time.deltaTime;
                if (settleTimer >= settleTime) break;
            }
            else
            {
                settleTimer = 0f;
            }

            yield return null;
        }

        falling = false;

        if (buffetCoroutine != null)
            StopCoroutine(buffetCoroutine);

        if (debugLogs) Debug.Log("[RagdollIntro] Ragdoll settled.");

        yield return new WaitForSeconds(delayBeforeTimeline);

        // Smooth blend from ragdoll pose back to bind pose
        if (useBlend && blendDuration > 0f)
            yield return StartCoroutine(BlendToAnimatedPose());
        else
            DisableRagdoll();

        // Snap root position — animator is already enabled by BlendToAnimatedPose/DisableRagdoll
        // applyRootMotion was false; let the timeline control movement
        transform.position = originalScenePosition;
        transform.rotation = originalSceneRotation;

        if (timelineDirector != null)
        {
            if (debugLogs) Debug.Log("[RagdollIntro] Playing timeline.");
            timelineDirector.Play();
        }

        introCoroutine = null;
    }

    IEnumerator ApplyAirBuffet()
    {
        HashSet<Transform> extremities = new HashSet<Transform>();
        if (animator != null && animator.isHuman)
        {
            AddIfExists(extremities, animator.GetBoneTransform(HumanBodyBones.LeftHand));
            AddIfExists(extremities, animator.GetBoneTransform(HumanBodyBones.RightHand));
            AddIfExists(extremities, animator.GetBoneTransform(HumanBodyBones.LeftFoot));
            AddIfExists(extremities, animator.GetBoneTransform(HumanBodyBones.RightFoot));
            AddIfExists(extremities, animator.GetBoneTransform(HumanBodyBones.Head));
        }

        while (falling)
        {
            foreach (var rb in ragdollBodies)
            {
                if (excludedFromForces.Contains(rb.transform)) continue;

                if (airBuffetForce > 0f)
                {
                    Vector3 randomForce = new Vector3(
                        Random.Range(-airBuffetForce, airBuffetForce),
                        Random.Range(-airBuffetForce * 0.5f, airBuffetForce * 0.5f),
                        Random.Range(-airBuffetForce, airBuffetForce)
                    );
                    rb.AddForce(randomForce, ForceMode.Impulse);
                }

                if (extremityLift > 0f && extremities.Contains(rb.transform))
                    rb.AddForce(Vector3.up * extremityLift, ForceMode.Impulse);
            }

            yield return new WaitForSeconds(buffetInterval);
        }
    }

    IEnumerator BlendToAnimatedPose()
    {
        // Snapshot ragdoll bone poses
        Vector3[] ragdollPositions = new Vector3[ragdollBodies.Length];
        Quaternion[] ragdollRotations = new Quaternion[ragdollBodies.Length];

        for (int i = 0; i < ragdollBodies.Length; i++)
        {
            ragdollPositions[i] = boneTransforms[i].localPosition;
            ragdollRotations[i] = boneTransforms[i].localRotation;
        }

        // Make kinematic, keep animator OFF — we drive bones manually
        // This prevents the one-frame pop from animator snapping to its first pose
        for (int i = 0; i < ragdollBodies.Length; i++)
        {
            ragdollBodies[i].isKinematic = true;
            ragdollBodies[i].linearVelocity = Vector3.zero;
            ragdollBodies[i].angularVelocity = Vector3.zero;
            ragdollBodies[i].linearDamping = originalLinearDrag[i];
            ragdollBodies[i].angularDamping = originalAngularDrag[i];
        }
        if (characterController != null) characterController.enabled = false;

        float t = 0f;
        while (t < blendDuration)
        {
            t += Time.deltaTime;
            float blend = Mathf.Clamp01(t / blendDuration);

            for (int i = 0; i < ragdollBodies.Length; i++)
            {
                if (boneTransforms[i] == null) continue;
                boneTransforms[i].localPosition = Vector3.Lerp(ragdollPositions[i], boneBindLocalPositions[i], blend);
                boneTransforms[i].localRotation = Quaternion.Slerp(ragdollRotations[i], boneBindLocalRotations[i], blend);
            }

            yield return null;
        }

        // Blend done — enable animator smoothly, no Rebind here
        if (animator != null) animator.enabled = true;
        if (characterController != null) characterController.enabled = true;
    }

    void EnableRagdoll()
    {
        if (animator != null) animator.enabled = false;

        for (int i = 0; i < ragdollBodies.Length; i++)
        {
            ragdollBodies[i].isKinematic = false;
            ragdollBodies[i].linearDamping = fallLinearDrag;
            ragdollBodies[i].angularDamping = fallAngularDrag;
        }

        if (characterController != null) characterController.enabled = false;
    }

    void DisableRagdoll()
    {
        for (int i = 0; i < ragdollBodies.Length; i++)
        {
            ragdollBodies[i].isKinematic = true;
            ragdollBodies[i].linearVelocity = Vector3.zero;
            ragdollBodies[i].angularVelocity = Vector3.zero;
            ragdollBodies[i].linearDamping = originalLinearDrag[i];
            ragdollBodies[i].angularDamping = originalAngularDrag[i];
        }

        if (animator != null) animator.enabled = true;
        if (characterController != null) characterController.enabled = true;
    }

    private void AddIfExists(HashSet<Transform> set, Transform t)
    {
        if (t != null) set.Add(t);
    }
}
