using System.Collections;
using UnityEngine;

public class PortalFlickerDisappear : MonoBehaviour
{
    [Header("Objekte")]
    public GameObject portalRoot;       // → Portal3.0
    public Renderer[] portalRenderers;  // → brush_Rainbow
    public Renderer[] assetRenderers;   // → alle anderen Brushes
    public ShaderTimeUpdater shaderTime;

    [Header("Flicker")]
    public float flickerDuration = 2.5f;
    public float flickerMinInterval = 0.03f;
    public float flickerMaxInterval = 0.15f;
    [Range(0f, 1f)] public float flickerMinAlpha = 0.0f;
    [Range(0f, 1f)] public float flickerMaxAlpha = 1.0f;

    [Header("Fade Out")]
    public float fadeOutDuration = 1.2f;

    public void TriggerDisappear()
    {
        StopAllCoroutines();
        StartCoroutine(FlickerSequence());
    }

    IEnumerator FlickerSequence()
    {
        // Rainbow einfrieren
        if (shaderTime != null) shaderTime.enabled = false;

        float elapsed = 0f;

        // --- Phase 1: Flackern ---
        while (elapsed < flickerDuration)
        {
            float alpha = Random.Range(flickerMinAlpha, flickerMaxAlpha);
            float progress = elapsed / flickerDuration;
            float interval = Mathf.Lerp(flickerMaxInterval, flickerMinInterval, progress);

            SetAlpha(portalRenderers, alpha);
            SetAlpha(assetRenderers, alpha * 0.8f);

            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }

        // --- Phase 2: Fade Out ---
        float fadeElapsed = 0f;
        while (fadeElapsed < fadeOutDuration)
        {
            float t = Mathf.SmoothStep(0f, 1f, fadeElapsed / fadeOutDuration);
            float alpha = 1f - t;

            SetAlpha(portalRenderers, alpha);
            SetAlpha(assetRenderers, alpha);

            fadeElapsed += Time.deltaTime;
            yield return null;
        }

        SetAlpha(portalRenderers, 0f);
        SetAlpha(assetRenderers, 0f);

        // Nur Portal deaktivieren, nicht den Berg
        if (portalRoot != null)
            portalRoot.SetActive(false);
    }

    void SetAlpha(Renderer[] renderers, float value)
    {
        foreach (var r in renderers)
        {
            if (r == null) continue;
            foreach (var mat in r.materials)
            {
                if (mat.HasProperty("_Cutoff"))
                    mat.SetFloat("_Cutoff", value);
                if (mat.HasProperty("_Opacity"))
                    mat.SetFloat("_Opacity", value);
                if (mat.HasProperty("_Alpha"))
                    mat.SetFloat("_Alpha", value);
            }
        }
    }

    // Debug: in Start() aufrufen um Properties in Console zu sehen
    void LogMaterialProperties()
    {
        foreach (var r in assetRenderers)
        {
            if (r == null) continue;
            foreach (var mat in r.materials)
            {
                Debug.Log($"{r.name} / {mat.name}: " +
                    $"_Cutoff={mat.HasProperty("_Cutoff")} " +
                    $"_Opacity={mat.HasProperty("_Opacity")} " +
                    $"_Alpha={mat.HasProperty("_Alpha")}");
            }
        }
    }
}