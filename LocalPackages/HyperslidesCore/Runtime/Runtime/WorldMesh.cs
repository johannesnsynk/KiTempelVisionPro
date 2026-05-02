using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NSYNK
{
    public class WorldMesh : MonoBehaviour
    {
        [SerializeField]
        private float loopTime, startValue, endValue = 0;
        private float timer = 0;
        [SerializeField]
        private string floatName;

        private MeshRenderer meshRenderer;

        private void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }

        private void Update()
        {
            timer += Time.deltaTime;

            if (meshRenderer && loopTime > 0)
            {
                meshRenderer.sharedMaterial.SetFloat(floatName, Mathf.Lerp(startValue, endValue, timer / loopTime));

                if(meshRenderer.sharedMaterial.HasFloat("_LoopTime"))
                    meshRenderer.sharedMaterial.SetFloat("_LoopTime", timer / loopTime);
            }

            if (timer > loopTime)
                timer = 0;
        }
    }
}