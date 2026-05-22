using UnityEngine;
using Apple.PHASE;
using System.Collections;
using CrazyMinnow.SALSA;

public class AvatarDialogue : MonoBehaviour
{
    [SerializeField] PHASESource phaseSource;
    [SerializeField] public AudioClip clip;
    [SerializeField] Salsa salsa;
    [SerializeField] float startDelay = 0f;
    [SerializeField] float phaseDelay = 0f;
    [SerializeField] float phaseAudioDelay = 4f; // PHASE braucht ~4s bis Ton hörbar

    private float playbackTime = 0f;
    private float phaseStartTime = 0f;
    private bool isPlaying = false;
    private float currentAmplitude = 0f;
    private float smoothedAmplitude = 0f;

    void Start()
    {
        Debug.Log($"AvatarDialogue Start – Zeit: {Time.realtimeSinceStartup:F2}s");
    }

    public void StartImmediate()
    {
        if (salsa != null)
        {
            salsa.useExternalAnalysis = true;
            salsa.getExternalAnalysis = GetAnalysis;
        }
        phaseStartTime = Time.realtimeSinceStartup;
        playbackTime = 0f;
        isPlaying = true;
        Debug.Log($"StartImmediate – Zeit: {Time.realtimeSinceStartup:F2}s");
    }

    float GetAnalysis()
    {
        return currentAmplitude;
    }

    public void Speak(AudioClip dialogueClip)
    {
        clip = dialogueClip;
        StartCoroutine(SyncedSpeak());
    }

    IEnumerator SyncedSpeak()
    {
        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        if (phaseSource != null)
            phaseSource.Play();

        phaseStartTime = Time.realtimeSinceStartup;
        playbackTime = 0f;
        isPlaying = true;

        if (salsa != null)
        {
            salsa.useExternalAnalysis = true;
            salsa.getExternalAnalysis = GetAnalysis;
        }

        if (phaseDelay > 0f)
            yield return new WaitForSeconds(phaseDelay);
    }

    void Update()
    {
        if (!isPlaying || clip == null)
        {
            currentAmplitude = 0f;
            if (salsa != null) salsa.analysisValue = 0f;
            return;
        }

        // phaseAudioDelay kompensiert PHASE Initialisierungs-Delay
        playbackTime = Time.realtimeSinceStartup - phaseStartTime - phaseAudioDelay;

        // Noch nicht starten bis PHASE wirklich spielt
        if (playbackTime < 0f)
        {
            currentAmplitude = 0f;
            if (salsa != null) salsa.analysisValue = 0f;
            return;
        }

        if (playbackTime >= clip.length)
        {
            isPlaying = false;
            currentAmplitude = 0f;
            if (salsa != null) salsa.analysisValue = 0f;
            Debug.Log($"Clip beendet: {clip.name}");
            return;
        }

        float raw = Mathf.Clamp01(GetAmplitudeAtTime(clip, playbackTime));
        smoothedAmplitude = Mathf.Lerp(smoothedAmplitude, raw, Time.deltaTime * 10f);
        currentAmplitude = smoothedAmplitude;
        if (salsa != null) salsa.analysisValue = currentAmplitude;
    }

    float GetAmplitudeAtTime(AudioClip clip, float time)
    {
        int sampleStart = Mathf.Clamp((int)(time * clip.frequency), 0, clip.samples - 512);
        float[] data = new float[512];
        clip.GetData(data, sampleStart);

        float sum = 0f;
        foreach (float s in data) sum += Mathf.Abs(s);
        return sum / data.Length;
    }

    public void Stop()
    {
        isPlaying = false;
        currentAmplitude = 0f;
        smoothedAmplitude = 0f;
        if (salsa != null) salsa.analysisValue = 0f;
        if (phaseSource != null) phaseSource.Stop();
    }
}