using UnityEngine;
using UnityEngine.XR.Hands;

namespace NSYNK.HyperSlides.XR
{
    /// <summary>
    /// The XRHandPalm attached to the users left hand joint and been activated base on the <see cref="UnityEngine.XR.Hands.Gestures.XRHandShape"/>
    /// </summary>
    public class XRHandPalm : MonoBehaviour
    {
        public static XRHandPalm Right, Left;

        public Handedness handedness;
        public bool facingUp = false;

        public void Awake()
        {
            if(handedness == Handedness.Left)
                Left = this;
            if (handedness == Handedness.Right)
                Right = this;
        }

        public void FacingUp(bool isFacingUp) => facingUp = isFacingUp;
    }
}