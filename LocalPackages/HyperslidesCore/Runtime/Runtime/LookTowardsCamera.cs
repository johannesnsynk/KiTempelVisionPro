using NSYNK.HyperSlides.XR;
using UnityEngine;

namespace NSYNK.HyperSlides
{
    public class LookTowardsCamera : MonoBehaviour
    {
        private void Update()
        {
            transform.forward = XRTrackedUser.Instance.Forward();
        }
    }
}