using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NSYNK.HyperSlides.Runtime
{
    public class MaterialUpdate : MonoBehaviour
    {
        private MeshRenderer meshRenderer;

        private void Awake() => meshRenderer = GetComponent<MeshRenderer>();

        public void UpdateTransparency(float value)
        {
            if (!meshRenderer)
                return;

            meshRenderer.sharedMaterial.SetFloat("_Transparency", value);
        }
    }
}