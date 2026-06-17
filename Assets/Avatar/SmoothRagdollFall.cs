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
    public float dropHeight = 100f;
    public Vector3 initialImpulse = Vector3.zero;

    [Header("Fall Tuning")]
    public float fallLinearDrag = 0.5f;
    [Tooltip("Lower this (e.g. 0.3) to let limbs swing more freely during the fall.")]
    public float fallAngularDrag = 0.3f;
    public float maxFallSpeed = 12f;

    [Header("Fall Liveliness")]
    public float initialAngularSpin = 2f;
    public float airBuffetForce = 0.5f;
    public float extremityLift = 1.5f;
    public float buffetInterval = 0.15f;

    [Header("Force Exclusions")]
    public bool excludeLeftArm = true;
    public bool excludeRightArm = false;

    [Header("Settle Detection")]
    [Tooltip("How long to wait after ragdoll starts before transitioning. Set longer than your expected fall duration.")]
    public float maxRagdollTime = 10f;

    [Header("Pose Blend")]
    public bool useBlend = true;
    public float blendDuration = 0.9f;

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

    // Stored as LOCAL position/rotation so they stay correct
    // after XRContentRoot/Anchor moves the parent
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private Vector3 parentOriginalLocalPosition;
    private Quaternion parentOriginalLocalRotation;

    private Coroutine introCoroutine;

    void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (timelineDirector == null) timelineDirector = GetComponent<PlayableDirector>();

        // Store LOCAL position — world position is wrong before anchor is set
        originalLocalPosition = transform.localPosition;
        originalLocalRotation = transform.localRotation;

        // Store parent (AvatarMover) local position/rotation
        if (transform.parent != null)
        {
            parentOriginalLocalPosition = transform.parent.localPosition;
            parentOriginalLocalRotation = transform.parent.localRotation;
        }

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

    void Start() { }

    void OnEnable()
    {
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

        HardReset();
    }

    void HardReset()
    {
        falling = false;

        if (timelineDirector != null)
        {
            timelineDirector.Stop();
            timelineDirector.time = 0;
        }
        if (characterController != null) characterController.enabled = true;

        if (ragdollBodies == null) return;

        for (int i = 0; i < ragdollBodies.Length; i++)
        {
            if (ragdollBodies[i] == null) continue;
            ragdollBodies[i].isKinematic = true;
            ragdollBodies[i].useGravity = true;
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

        // Use LOCAL position so anchor offset is respected
        transform.localPosition = originalLocalPosition;
        transform.localRotation = originalLocalRotation;

        // Restore AvatarMover (parent) to original position/rotation
        if (transform.parent != null)
        {
            transform.parent.localPosition = parentOriginalLocalPosition;
            transform.parent.localRotation = parentOriginalLocalRotation;
        }

        if (hipsBone != null)
        {
            hipsBone.localPosition = Vector3.zero;
            hipsBone.localRotation = Quaternion.identity;
        }

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
        if (debugLogs) Debug.Log($"[RagdollIntro] Visibility: {state} at {Time.time:F2}s");

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
        // Read world position NOW — anchor is set at this point
        Vector3 worldStart = transform.position;

        transform.position = new Vector3(
            worldStart.x,
            worldStart.y + dropHeight,
            worldStart.z
        );

        if (debugLogs) Debug.Log($"[RagdollIntro] Drop start at {Time.time:F2}s, from world {worldStart}, height={dropHeight}");

        EnableRagdoll();

        // Wait 10 frames for PolySpatial to register physics state
        for (int i = 0; i < 10; i++)
            yield return null;

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

        // Simple time-based wait — most reliable on visionOS/PolySpatial
        if (debugLogs) Debug.Log($"[RagdollIntro] Waiting {maxRagdollTime}s...");
        yield return new WaitForSeconds(maxRagdollTime);

        falling = false;

        if (buffetCoroutine != null)
            StopCoroutine(buffetCoroutine);

        if (debugLogs) Debug.Log($"[RagdollIntro] Ragdoll done at {Time.time:F2}s — starting blend");

        yield return new WaitForSeconds(delayBeforeTimeline);

        if (useBlend && blendDuration > 0f)
            yield return StartCoroutine(BlendToAnimatedPose());
        else
            DisableRagdoll();

        if (debugLogs) Debug.Log($"[RagdollIntro] Blend END at {Time.time:F2}s");

        // Reset AvatarV2 (this object)
        transform.localPosition = originalLocalPosition;
        transform.localRotation = originalLocalRotation;

        // Reset AvatarMover (parent) to its original position/rotation
        // NOT to zero — AvatarMover has z=-2, y=180 etc.
        if (transform.parent != null)
        {
            transform.parent.localPosition = parentOriginalLocalPosition;
            transform.parent.localRotation = parentOriginalLocalRotation;
        }

        if (debugLogs) Debug.Log($"[RagdollIntro] Timeline START at {Time.time:F2}s, world pos={transform.position}");

        if (timelineDirector != null)
        {
            timelineDirector.time = 0;
            timelineDirector.Evaluate();
            timelineDirector.Play();
            if (debugLogs) Debug.Log($"[RagdollIntro] Timeline state after Play(): {timelineDirector.state}");
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
        Vector3[] ragdollPositions = new Vector3[ragdollBodies.Length];
        Quaternion[] ragdollRotations = new Quaternion[ragdollBodies.Length];

        for (int i = 0; i < ragdollBodies.Length; i++)
        {
            ragdollPositions[i] = boneTransforms[i].localPosition;
            ragdollRotations[i] = boneTransforms[i].localRotation;
        }

        // Kinematic on, animator stays OFF — manual bone drive prevents pop
        for (int i = 0; i < ragdollBodies.Length; i++)
        {
            ragdollBodies[i].isKinematic = true;
            ragdollBodies[i].linearVelocity = Vector3.zero;
            ragdollBodies[i].angularVelocity = Vector3.zero;
            ragdollBodies[i].linearDamping = originalLinearDrag[i];
            ragdollBodies[i].angularDamping = originalAngularDrag[i];
        }
        if (characterController != null) characterController.enabled = false;

        // Snapshot root position at blend start
        Vector3 ragdollRootLocalPos = transform.localPosition;
        Quaternion ragdollRootLocalRot = transform.localRotation;

        float t = 0f;
        while (t < blendDuration)
        {
            t += Time.deltaTime;
            float blend = Mathf.Clamp01(t / blendDuration);

            // Lerp bones back to bind pose
            for (int i = 0; i < ragdollBodies.Length; i++)
            {
                if (boneTransforms[i] == null) continue;
                boneTransforms[i].localPosition = Vector3.Lerp(ragdollPositions[i], boneBindLocalPositions[i], blend);
                boneTransforms[i].localRotation = Quaternion.Slerp(ragdollRotations[i], boneBindLocalRotations[i], blend);
            }

            // Also lerp root transform back to original local position
            // so avatar doesn't snap/float when animator takes over
            transform.localPosition = Vector3.Lerp(ragdollRootLocalPos, originalLocalPosition, blend);
            transform.localRotation = Quaternion.Slerp(ragdollRootLocalRot, originalLocalRotation, blend);

            yield return null;
        }

        // Ensure exact final position
        transform.localPosition = originalLocalPosition;
        transform.localRotation = originalLocalRotation;

        if (animator != null) animator.enabled = true;
        if (characterController != null) characterController.enabled = true;
    }

    void EnableRagdoll()
    {
        if (animator != null) animator.enabled = false;

        for (int i = 0; i < ragdollBodies.Length; i++)
        {
            ragdollBodies[i].isKinematic = false;
            ragdollBodies[i].useGravity = true;
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
