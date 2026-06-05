using System.Collections.Generic;
using UnityEngine;

public class EuryDissolve : MonoBehaviour
{
    [Header("Movement")]
    public float explodeForce = 0.3f;

    [Header("Rotation")]
    public float rotationSpeed = 45f;

    [Header("Fade")]
    public float fadeDelay = 6f;
    public float fadeDuration = 4f;

    public void Explode()
    {
        Debug.Log($"EuryDissolve: Explode called on {gameObject.name}");

        foreach (var anim in GetComponentsInChildren<Animator>())
        {
            anim.enabled = false;
            anim.Rebind();
        }

        var brushTransforms = new List<Transform>();
        foreach (Transform child in transform.GetComponentsInChildren<Transform>())
        {
            if (child == transform) continue;
            if (!child.name.StartsWith("brush_")) continue;
            brushTransforms.Add(child);
        }

        Debug.Log($"EuryDissolve: {brushTransforms.Count} brush meshes found");

       foreach (var child in brushTransforms)
{
    var smr = child.GetComponent<SkinnedMeshRenderer>();
    if (smr == null) continue;

    Mesh bakedMesh = new Mesh();
    smr.BakeMesh(bakedMesh, true);

    GameObject partGO = new GameObject("euryPart_" + child.name);
    partGO.transform.position = child.transform.position;
    partGO.transform.rotation = child.transform.rotation;
    partGO.transform.localScale = Vector3.one * 1000f;

    partGO.AddComponent<MeshFilter>().mesh = bakedMesh;
    partGO.AddComponent<MeshRenderer>().material = new Material(smr.sharedMaterial);
    smr.enabled = false;

    Vector3 driftDir = (child.transform.position - transform.position).normalized;
    driftDir.y -= 0.3f;
    driftDir.Normalize();

    var mover = partGO.AddComponent<EuryPartMover>();
    mover.Init(
        transform.position,                          // Mitte = eurydikeRIGposeNEW2 Position
        driftDir,
        Random.Range(0.02f, 0.06f),                   // Drift-Geschwindigkeit
        Random.onUnitSphere,                         // Orbit-Achse
        Random.Range(rotationSpeed * 0.1f, rotationSpeed * 0.1f), // Orbit-Geschwindigkeit
        fadeDelay,
        fadeDuration
    );
}
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E)) Explode();
    }
}