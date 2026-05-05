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

            var smr = child.GetComponent<SkinnedMeshRenderer>();
            if (smr != null)
            {
                Vector3 worldPos = child.position;
                Quaternion worldRot = child.rotation;

                Mesh bakedMesh = new Mesh();
                smr.BakeMesh(bakedMesh, false);
                smr.enabled = false;

                var mf = child.gameObject.AddComponent<MeshFilter>();
                mf.mesh = bakedMesh;

                var mr = child.gameObject.AddComponent<MeshRenderer>();
                mr.material = smr.material;

                child.SetParent(null, true);
                child.position = worldPos;
                child.rotation = worldRot;
                child.localScale = Vector3.one * meshScale;
            }
            else
            {
                child.SetParent(null, true);
                child.localScale = Vector3.one * meshScale;
            }

            Vector3 dir = Random.onUnitSphere;
            Vector3 randomAxis = Random.onUnitSphere;
            float randomSpeed = Random.Range(rotationSpeed * 0.3f, rotationSpeed * 1.5f);

            StartCoroutine(FloatAway(child, dir, randomAxis, randomSpeed));

            if (fadeDelay > 0)
                StartCoroutine(FadeOut(child.gameObject, fadeDelay));
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

    private IEnumerator FadeOut(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (obj != null)
            obj.SetActive(false);
    }
}