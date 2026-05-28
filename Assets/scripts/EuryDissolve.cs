using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EuryDissolve : MonoBehaviour
{
    [Header("Movement")]
    public float explodeForce = 0.02f;

    [Header("Rotation")]
    public float rotationSpeed = 5f;

    [Header("Fade")]
    public float fadeDelay = 6f;
    public float fadeDuration = 2f;

    [Header("Scale")]
    public float meshScale = 1f;

    public void Explode()
    {
        Debug.Log("EuryDissolve: Explode called!");

        var animator = GetComponentInChildren<Animator>();
        if (animator != null) animator.enabled = false;

        int count = 0;

        foreach (Transform child in transform.GetComponentsInChildren<Transform>())
        {
            if (child == transform) continue;
            if (!child.name.StartsWith("brush_")) continue;

            count++;

            // SMR bleibt aktiv — kein Swap, kein SetParent(null)
            var smr = child.GetComponent<SkinnedMeshRenderer>();
            if (smr != null)
            {
                // Eigene Material-Instanz damit Alpha-Manipulation unabhängig ist
                smr.material = new Material(smr.material);
            }

            Vector3 dir = Random.onUnitSphere;
            // Gravity-Bias: nach unten tendieren
            dir.y -= 1.2f;
            dir.Normalize();

            Vector3 randomAxis = Random.onUnitSphere;
            float randomSpeed = Random.Range(rotationSpeed * 0.3f, rotationSpeed * 1.5f);

            StartCoroutine(FloatAway(child, dir, randomAxis, randomSpeed));

            if (fadeDelay > 0)
                StartCoroutine(FadeOut(child.gameObject, fadeDelay, fadeDuration));
        }

        Debug.Log($"EuryDissolve: total brush meshes found: {count}");
    }

    private IEnumerator FloatAway(Transform obj, Vector3 dir, Vector3 rotAxis, float rotSpeed)
    {
        while (true)
        {
            if (obj == null) yield break;
            obj.position += dir * (explodeForce * Time.deltaTime);
            obj.Rotate(rotAxis, rotSpeed * Time.deltaTime, Space.World);
            yield return null;
        }
    }

    private IEnumerator FadeOut(GameObject obj, float delay, float duration)
    {
        yield return new WaitForSeconds(delay);

        if (obj == null) yield break;

        // Alle Renderer (SMR oder MR) einsammeln
        var renderers = obj.GetComponents<Renderer>();
        if (renderers.Length == 0) { obj.SetActive(false); yield break; }

        float elapsed = 0f;

        // Startfarben merken
        var startColors = new Color[renderers.Length][];
        for (int i = 0; i < renderers.Length; i++)
        {
            var mats = renderers[i].materials;
            startColors[i] = new Color[mats.Length];
            for (int j = 0; j < mats.Length; j++)
                startColors[i][j] = mats[j].HasProperty("_Color")
                    ? mats[j].color
                    : Color.white;
        }

        while (elapsed < duration)
        {
            if (obj == null) yield break;
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(1f - (elapsed / duration));

            for (int i = 0; i < renderers.Length; i++)
            {
                var mats = renderers[i].materials;
                for (int j = 0; j < mats.Length; j++)
                {
                    if (mats[j].HasProperty("_Color"))
                    {
                        Color c = startColors[i][j];
                        c.a = alpha;
                        mats[j].color = c;
                    }
                }
            }
            yield return null;
        }

        if (obj != null) obj.SetActive(false);
    }
}