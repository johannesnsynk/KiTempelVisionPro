using UnityEngine;

namespace NSYNK.HyperSlides.Runtime
{
    [ExecuteInEditMode]
    public class XRContentRootSetter : MonoBehaviour
    {
        private XRContentRoot xRContentRoot;

        private void OnEnable()
        {
            xRContentRoot = FindFirstObjectByType<XRContentRoot>(FindObjectsInactive.Include);

            if (!xRContentRoot)
            {
                xRContentRoot = new GameObject().AddComponent<XRContentRoot>();
                xRContentRoot.transform.parent = null;                
            }

            xRContentRoot.name = "XRContentRoot";
        }
    }
}