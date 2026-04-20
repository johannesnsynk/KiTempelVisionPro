using UnityEngine;

public class ShaderTimeUpdater : MonoBehaviour
{
    Material[] materials;
    
    void Start()
    {
        materials = GetComponent<Renderer>().materials;
    }
    
    void Update()
    {
        foreach (var mat in materials)
        {
            mat.SetFloat("_TimeValue", Time.time);
        }
    }
}