using UnityEngine;
using Apple.PHASE;

public class PHASELipSync : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] PHASESource phaseSource;
    [SerializeField] AudioClip clip;

    [Header("Jaw Rig")]
    [SerializeField] Transform jawTarget;
    [SerializeField] float jawOpenX = -25f;
    [SerializeField] float jawClosedX = 0f;
    [SerializeField] float smoothSpeed = 15f;

    [Header("Tuning")]
    [SerializeField] float amplitudeThreshold = 0.3f;
    [SerializeField] float amplitudeMultiplier = 0.5f;

    [Header("Debug")]
    [SerializeField] bool debugMode = true;

    private float playbackTime = 0f;
    private bool isPlaying = false;
    private float currentValue = 0f;

    void Start()
    {
        Debug.Log($"PHASELipSync Start – Clip null? {clip == null} | PHASESource null? {phaseSource == null} | JawTarget null? {jawTarget == null}");
        
        if (clip != null)
            SpeakClip(clip);
        else
            Debug.LogError("PHASELipSync: Kein AudioClip zugewiesen!");
    }

    public void SpeakClip(AudioClip dialogueClip)
    {
        clip = dialogueClip;
        playbackTime = 0f;
        isPlaying = true;

        if (phaseSource != null)
            phaseSource.Play();
        else
            Debug.LogError("PHASELipSync: PHASESource nicht zugewiesen!");

        Debug.Log($"PHASELipSync: SpeakClip gestartet – {dialogueClip.name}, Länge: {dialogueClip.length:F2}s");
    }

    public void Stop()
    {
        isPlaying = false;
        if (phaseSource != null) phaseSource.Stop();
        SetJaw(0f);
    }

    void LateUpdate()
    {
        if (jawTarget == null) return;
        
        if (!isPlaying || clip == null)
        {
            // Mund schließen wenn nicht spielt
            currentValue = Mathf.Lerp(currentValue, 0f, Time.deltaTime * smoothSpeed);
            SetJaw(currentValue);
            return;
        }

        playbackTime += Time.deltaTime;

        if (playbackTime >= clip.length)
        {
            isPlaying = false;
            currentValue = 0f;
            SetJaw(0f);
            Debug.Log("PHASELipSync: Clip beendet");
            return;
        }

        float amplitude = GetAmplitudeAtTime(clip, playbackTime);
        float target = amplitude > amplitudeThreshold
            ? Mathf.Clamp01(amplitude * amplitudeMultiplier)
            : 0f;

        currentValue = Mathf.Lerp(currentValue, target, Time.deltaTime * smoothSpeed);

        if (debugMode)
            Debug.Log($"amp:{amplitude:F3} target:{target:F3} jaw:{currentValue:F3}");

        SetJaw(currentValue);
    }

    void SetJaw(float value)
    {
        float angle = Mathf.Lerp(jawClosedX, jawOpenX, value);
        jawTarget.localRotation = Quaternion.Euler(angle, 0f, 0f);
    }

    float GetAmplitudeAtTime(AudioClip clip, float time)
    {
        int sampleStart = Mathf.Clamp((int)(time * clip.frequency), 0, clip.samples - 512);
        float[] data = new float[512];
        clip.GetData(data, sampleStart);

        float sum = 0f;
        foreach (float s in data) sum += Mathf.Abs(s);
        return Mathf.Clamp01(sum / data.Length * 10f);
    }
}