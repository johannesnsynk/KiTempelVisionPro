using NSYNK.HyperSlides.UI;
using UnityEngine;

namespace NSYNK.HyperSlides.Runtime
{
    /// <summary>
    /// The XR pointer being instantiated by the moderator. For now, we only allow one active pointer
    /// </summary>
    public class XRPointer : MonoBehaviour
    {
        public GameObject visualPointer;
        public GameObject pointerHitMarker;
        public MeshRenderer pointerRenderer;

        private void Start()
        {
            //pointerHitMarker.transform.parent = XRContentRoot.instance.transform;

            if(pointerHitMarker.TryGetComponent(out Collider collider))
                Destroy(collider);

            gameObject.SetActive(false);
        }

        private void OnEnable() => Show();
        private void OnDisable() => Hide();

        public void Show()
        {
            this.AnimateFloat(pointerRenderer.transform, Easing.Ease.EaseInOutQuad, 0, RuntimeHandler.Settings.pointerTransparency, 1f, 0, (float update) => pointerRenderer.sharedMaterial.SetFloat("_Transparency", update));

            visualPointer.SetActive(true);
            pointerHitMarker.SetActive(true);
        }

        public void Hide()
        {
            visualPointer.SetActive(false);
            pointerHitMarker.SetActive(false);

            pointerRenderer.sharedMaterial.SetFloat("_Transparency", 0);
        }

        private void OnDestroy()
        {
            //Destroy(pointerHitMarker);
        }
    }
}