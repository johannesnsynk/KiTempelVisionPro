using UnityEngine;

/// <summary>
/// Automatically replaces legacy Tilt Brush shaders with PolySpatial-compatible URP Shader Graph versions.
/// Attach to any GameObject in the scene. Runs on Awake.
///
/// Assign the three materials from Assets/TiltBrush/Assets/Shaders/PolySpatial/ in the Inspector,
/// or leave them empty to let the script locate the shaders by name at runtime.
/// </summary>
public class PolySpatialShaderFixer : MonoBehaviour
{
    [Header("PolySpatial Replacement Materials")]
    [Tooltip("PS_Light_Bloom.mat — replaces Brush/Bloom (additive glow, Unlit Transparent Premultiply)")]
    public Material psLightBloomMaterial;

    [Tooltip("PS_WetPaint.mat — replaces Shader Graphs/StandardDoubleSidedURP (Lit Opaque Double-Sided)")]
    public Material psWetPaintMaterial;

    [Tooltip("PS_LightWire.mat — replaces Brush/Special/LightWire (Lit Opaque, emission pulsing)")]
    public Material psLightWireMaterial;

    // Fallback: new shader graph names used when material slots are not assigned in Inspector
    private const string BLOOM_NEW     = "Shader Graphs/PS_Light_Bloom";
    private const string WET_PAINT_NEW = "Shader Graphs/PS_WetPaint";
    private const string LIGHTWIRE_NEW = "Shader Graphs/PS_LightWire";

    // Old shader names to detect (multiple aliases per brush for resilience)
    private static readonly string[] LightShaderNames = {
        "Brush/Bloom",
        "Brush/AdditiveCutout",
    };

    private static readonly string[] WetPaintShaderNames = {
        "Shader Graphs/StandardDoubleSidedURP",
        "Brush/StandardDoubleSided",
    };

    private static readonly string[] LightWireShaderNames = {
        "Brush/Special/LightWire",
        "Brush/Wire",
    };

    void Awake()
    {
        // If material slots are empty, build them from Shader.Find fallbacks
        if (psLightBloomMaterial == null)
        {
            Shader s = Shader.Find(BLOOM_NEW);
            if (s != null) psLightBloomMaterial = new Material(s) { name = "PS_Light_Bloom_Runtime" };
        }
        if (psWetPaintMaterial == null)
        {
            Shader s = Shader.Find(WET_PAINT_NEW);
            if (s != null) psWetPaintMaterial = new Material(s) { name = "PS_WetPaint_Runtime" };
        }
        if (psLightWireMaterial == null)
        {
            Shader s = Shader.Find(LIGHTWIRE_NEW);
            if (s != null) psLightWireMaterial = new Material(s) { name = "PS_LightWire_Runtime" };
        }

        if (psLightBloomMaterial == null && psWetPaintMaterial == null && psLightWireMaterial == null)
        {
            Debug.LogWarning("[PolySpatialShaderFixer] No replacement materials found. " +
                             "Assign PS_Light_Bloom, PS_WetPaint, PS_LightWire materials in the Inspector.");
            return;
        }

        int replaced = 0;
        // includeInactive: also fix hidden stroke objects
        Renderer[] allRenderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Renderer rend in allRenderers)
        {
            Material[] mats = rend.sharedMaterials;
            bool changed = false;

            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null || mats[i].shader == null) continue;

                string shaderName = mats[i].shader.name;

                if (TryReplace(shaderName, LightShaderNames, psLightBloomMaterial, ref mats[i]))
                { replaced++; changed = true; }
                else if (TryReplace(shaderName, WetPaintShaderNames, psWetPaintMaterial, ref mats[i]))
                { replaced++; changed = true; }
                else if (TryReplace(shaderName, LightWireShaderNames, psLightWireMaterial, ref mats[i]))
                { replaced++; changed = true; }
            }

            if (changed)
                rend.sharedMaterials = mats;
        }

        Debug.Log($"[PolySpatialShaderFixer] Replaced {replaced} material slot(s) with PolySpatial-compatible shaders.");
    }

    private static bool TryReplace(string currentName, string[] oldNames, Material replacement, ref Material mat)
    {
        if (replacement == null) return false;

        foreach (string oldName in oldNames)
        {
            if (currentName != oldName) continue;

            // Clone the replacement so per-renderer overrides don't bleed across objects
            Material instance = new Material(replacement) { name = replacement.name };

            // Preserve key properties from the original material
            CopyIfPresent(mat, instance, "_MainTex",       (s, d, p) => d.SetTexture(p, s.GetTexture(p)));
            CopyIfPresent(mat, instance, "_BumpMap",       (s, d, p) => d.SetTexture(p, s.GetTexture(p)));
            CopyIfPresent(mat, instance, "_Color",         (s, d, p) => d.SetColor(p, s.GetColor(p)));
            CopyIfPresent(mat, instance, "_TintColor",     (s, d, p) => d.SetColor(p, s.GetColor(p)));
            CopyIfPresent(mat, instance, "_EmissionGain",  (s, d, p) => d.SetFloat(p, s.GetFloat(p)));

            mat = instance;
            return true;
        }
        return false;
    }

    private delegate void CopyDelegate(Material src, Material dst, string prop);

    private static void CopyIfPresent(Material src, Material dst, string prop, CopyDelegate copy)
    {
        if (src.HasProperty(prop) && dst.HasProperty(prop))
            copy(src, dst, prop);
    }
}
