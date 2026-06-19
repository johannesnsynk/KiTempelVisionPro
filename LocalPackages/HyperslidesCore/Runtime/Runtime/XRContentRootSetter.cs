using UnityEngine;

namespace NSYNK.HyperSlides.Runtime
{
    /// <summary>
    /// Sets up the XRContentRoot in the scene if it doesn't already exist.
    /// </summary>
    [ExecuteInEditMode]
    public class XRContentRootSetter : MonoBehaviour
    {
        private XRContentRoot xRContentRoot;

        private void OnEnable()
        {
            xRContentRoot = FindFirstObjectByType<XRContentRoot>(FindObjectsInactive.Include);

            if (!xRContentRoot)
            {
                Debug.LogWarning("No XRContentRoot found in scene. Creating one.");

                xRContentRoot = new GameObject().AddComponent<XRContentRoot>();
                xRContentRoot.transform.parent = null;
            }

            xRContentRoot.name = "XRContentRoot";
        }
    }
}