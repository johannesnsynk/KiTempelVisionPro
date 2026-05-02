using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace NSYNK
{
    public class ShaderOverwrite : MonoBehaviour
    {
        public Material customMaterialShader;

        private void OnEnable()
        {
            if(customMaterialShader)
                GetComponentsInChildren<MaskableGraphic>().ToList().ForEach(c =>
                {
                    c.material = customMaterialShader;
                });
        }
    }
}