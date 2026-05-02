using NSYNK.HyperSlides.XR;
using UnityEngine;

namespace NSYNK.HyperSlides
{
    public class LookAtCamera : MonoBehaviour
    {
        private void Update()
        {
            transform.LookAt(XRTrackedUser.Instance.Position());
        }
    }
}