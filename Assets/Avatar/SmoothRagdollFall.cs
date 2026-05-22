using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using Apple.PHASE;

[RequireComponent(typeof(Animator))]
public class RagdollIntroSequence : MonoBehaviour
{
    [Header("References")]
    public PlayableDirector timelineDirector;
    public Animator animator;

    [Header("Audio")]
    public PHASESource phaseSource;
    public AvatarDialogue avatarDialogue;
    public AudioClip dialogueClip;

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
    public bool debugLogs = false;

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

    void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (timelineDirector == null) timelineDirector = GetComponent<PlayableDirector>();

        originalScenePosition = transform.position;

        ragdollBodies = GetComponentsInChildren<Rigidbody>();
        characterController = GetComponent<CharacterController>();

        if (animator != null && animator.isHuman)
            hipsBone = animator.GetBoneTransform(HumanBodyBones.Hips);
        if (hipsBone == null && ragdollBodies.Length > 0)
            hipsBone = ragdollBodies[0].transform;

        boneTransforms = new Transform[ragdollBodies.Length];
        boneBindLocalPositions = new Vector3[ragdollBodies.Length];
        boneBindLocalRotations = new Quaternion[ragdollBodies.Length];
        originalLinearDrag = new float[ragdollBodies.Length];
        originalAngularDrag = new float[ragdollBodies.Length];

        for (int i = 0; i < ragdollBodies.Length; i++)
        {
            boneTransforms[i] = ragdollBodies[i].transform;
            boneBindLocalPositions[i] = boneTransforms[i].localPosition;
            boneBindLocalRotations[i] = boneTransforms[i].localRotation;
            originalLinearDrag[i] = ragdollBodies[i].linearDamping;
            originalAngularDrag[i] = ragdollBodies[i].angularDamping;
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
        // PHASE erst in Start() – nach Apple.Core Initialisierung
        if (phaseSource != null)
            phaseSource.Play();

        if (avatarDialogue != null && dialogueClip != null)
        {
            avatarDialogue.clip = dialogueClip;
            avatarDialogue.StartImmediate();
            Debug.Log($"Audio gestartet in Start – Zeit: {Time.realtimeSinceStartup:F2}s");
        }

        StartCoroutine(RunIntroSequence());
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
        transform.position = originalScenePosition + Vector3.up * dropHeight;

        EnableRagdoll(true);
        ApplyFallTuning(true);
        falling = true;

        foreach (var rb in ragdollBodies)
        {
            if (initialImpulse != Vector3.zero)
                rb.AddForce(initialImpulse, ForceMode.VelocityChange);

            if (excludedFromForces.Contains(rb.transform)) continue;

            if (initialAngularSpin > 0f)
            {
                Vector3 randomSpin = new Vector3(
                    Random.Range(-initialAngularSpin, initialAngularSpin),
                    Random.Range(-initialAngularSpin, initialAngularSpin),
                    Random.Range(-initialAngularSpin, initialAngularSpin)
                );
                rb.angularVelocity = randomSpin;
            }
        }

        if (airBuffetForce > 0f || extremityLift > 0f)
            StartCoroutine(ApplyFallForces());

        yield return StartCoroutine(WaitForRagdollToSettle());

        falling = false;
        ApplyFallTuning(false);

        if (debugLogs) Debug.Log("[RagdollIntro] Ragdoll settled.");

        yield return new WaitForSeconds(delayBeforeTimeline);

        Vector3[] landedWorldPositions;
        Quaternion[] landedWorldRotations;
        CaptureCurrentWorldPose(out landedWorldPositions, out landedWorldRotations);

        if (debugLogs) Debug.Log($"[RagdollIntro] Captured landed pose. Hips world Y: {landedWorldPositions[0].y:F3}");

        transform.position = originalScenePosition;

        EnableRagdoll(false);

        if (timelineDirector != null)
        {
            timelineDirector.time = 0;
            timelineDirector.Play();
            timelineDirector.Pause();
            if (debugLogs) Debug.Log("[RagdollIntro] Timeline started and paused at frame 0.");
        }

        if (animator != null)
            animator.Update(0f);

        if (useBlend && blendDuration > 0f)
        {
            if (debugLogs) Debug.Log($"[RagdollIntro] Starting blend, duration: {blendDuration}s");
            yield return StartCoroutine(BlendFromRagdollPose(landedWorldPositions, landedWorldRotations, blendDuration));
            if (debugLogs) Debug.Log("[RagdollIntro] Blend complete.");
        }
        else
        {
            if (debugLogs) Debug.Log($"[RagdollIntro] Blend SKIPPED. useBlend={useBlend}, blendDuration={blendDuration}");
        }

        if (timelineDirector != null)
        {
            timelineDirector.Resume();
            if (debugLogs) Debug.Log("[RagdollIntro] Timeline resumed.");
        }
    }

    IEnumerator ApplyFallForces()
    {
        HashSet<Transform> extremities = new HashSet<Transform>();
        if (animator != null && animator.isHuman)
        {
            AddIfExists(extremities, animator.GetBoneTransform(HumanBodyBones.LeftHand));
            AddIfExists(extremities, animator.GetBoneTransform(HumanBodyBones.RightHand));
            AddIfExists(extremities, animator.GetBoneTransform(HumanBodyBones.LeftLowerArm));
            AddIfExists(extremities, animator.GetBoneTransform(HumanBodyBones.RightLowerArm));
            AddIfExists(extremities, animator.GetBoneTransform(HumanBodyBones.LeftFoot));
            AddIfExists(extremities, animator.GetBoneTransform(HumanBodyBones.RightFoot));
            AddIfExists(extremities, animator.GetBoneTransform(HumanBodyBones.Head));
        }

        float nextBuffet = 0f;

        while (falling)
        {
            if (airBuffetForce > 0f && Time.time >= nextBuffet)
            {
                foreach (var rb in ragdollBodies)
                {
                    if (excludedFromForces.Contains(rb.transform)) continue;

                    Vector3 randomForce = new Vector3(
                        Random.Range(-airBuffetForce, airBuffetForce),
                        Random.Range(-airBuffetForce * 0.3f, airBuffetForce * 0.3f),
                        Random.Range(-airBuffetForce, airBuffetForce)
                    );
                    rb.AddForce(randomForce, ForceMode.Impulse);
                }
                nextBuffet = Time.time + buffetInterval;
            }

            if (extremityLift > 0f)
            {
                foreach (var rb in ragdollBodies)
                {
                    if (excludedFromForces.Contains(rb.transform)) continue;
                    if (extremities.Contains(rb.transform))
                        rb.AddForce(Vector3.up * extremityLift, ForceMode.Force);
                }
            }

            yield return new WaitForFixedUpdate();
        }
    }

    void AddIfExists(HashSet<Transform> set, Transform t)
    {
        if (t != null) set.Add(t);
    }

    void ApplyFallTuning(bool falling)
    {
        for (int i = 0; i < ragdollBodies.Length; i++)
        {
            var rb = ragdollBodies[i];
            if (falling)
            {
                rb.linearDamping = fallLinearDrag;
                rb.angularDamping = fallAngularDrag;
            }
            else
            {
                rb.linearDamping = originalLinearDrag[i];
                rb.angularDamping = originalAngularDrag[i];
            }
        }
    }

    IEnumerator WaitForRagdollToSettle()
    {
        float settledFor = 0f;
        float elapsed = 0f;
        yield return new WaitForFixedUpdate();

        while (elapsed < maxRagdollTime)
        {
            float maxVel = 0f;
            foreach (var rb in ragdollBodies)
            {
                float v = rb.linearVelocity.magnitude;
                if (v > maxVel) maxVel = v;
            }

            if (maxVel < settleVelocityThreshold) settledFor += Time.fixedDeltaTime;
            else settledFor = 0f;

            if (settledFor >= settleTime) yield break;

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
    }

    void EnableRagdoll(bool enabled)
    {
        if (animator != null) animator.enabled = !enabled;
        if (characterController != null) characterController.enabled = !enabled;

        foreach (var rb in ragdollBodies)
        {
            rb.isKinematic = !enabled;
            rb.detectCollisions = true;
            if (enabled) rb.linearVelocity = Vector3.zero;
        }
    }

    void CaptureCurrentWorldPose(out Vector3[] worldPositions, out Quaternion[] worldRotations)
    {
        worldPositions = new Vector3[boneTransforms.Length];
        worldRotations = new Quaternion[boneTransforms.Length];
        for (int i = 0; i < boneTransforms.Length; i++)
        {
            worldPositions[i] = boneTransforms[i].position;
            worldRotations[i] = boneTransforms[i].rotation;
        }
    }

    IEnumerator BlendFromRagdollPose(Vector3[] landedWorldPositions, Quaternion[] landedWorldRotations, float duration)
    {
        float t = 0f;
        int frameCount = 0;

        yield return null;

        while (t < duration)
        {
            if (timelineDirector != null)
            {
                timelineDirector.time = 0;
                timelineDirector.Evaluate();
            }

            if (debugLogs && frameCount < 5)
            {
                Vector3 leftHandTarget = boneTransforms.Length > 5 ? boneTransforms[5].position : Vector3.zero;
                Vector3 leftHandLanded = landedWorldPositions.Length > 5 ? landedWorldPositions[5] : Vector3.zero;
                Debug.Log($"[RagdollIntro] Blend frame {frameCount}: t={t:F3}, dt={Time.deltaTime:F4}, " +
                          $"hips landed Y={landedWorldPositions[0].y:F3} target Y={boneTransforms[0].position.y:F3}, " +
                          $"bone[5] landed={leftHandLanded} target={leftHandTarget}");
            }

            float k = Mathf.Clamp01(t / duration);
            float s = k * k * (3f - 2f * k);

            for (int i = 0; i < boneTransforms.Length; i++)
            {
                Vector3 targetPos = boneTransforms[i].position;
                Quaternion targetRot = boneTransforms[i].rotation;

                boneTransforms[i].position = Vector3.Lerp(landedWorldPositions[i], targetPos, s);
                boneTransforms[i].rotation = Quaternion.Slerp(landedWorldRotations[i], targetRot, s);
            }

            t += Time.deltaTime;
            frameCount++;
            yield return null;
        }
    }
}