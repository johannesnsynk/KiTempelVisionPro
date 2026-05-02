using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.XR;

namespace NSYNK.HyperSlides.XR
{
    /// <summary>
    /// A static representation of the current tracked user posedriver
    /// </summary>
    public class XRTrackedUser : Singleton<XRTrackedUser>
    {
        private TrackedPoseDriver cameraPoseDriver;

        public Vector3 Forward()
        {
            if (cameraPoseDriver)
                return cameraPoseDriver.transform.forward;
            else
                return Camera.main.transform.forward;
        }

        /// <summary>
        /// The posedriver position, in build the actual head
        /// </summary>
        /// <returns></returns>
        public Vector3 Position()
        {
            if (cameraPoseDriver)
                return cameraPoseDriver.transform.position;
            else
                return Camera.main.transform.position;
        }

        /// <summary>
        /// The posedriver rotation, in build head rotation
        /// </summary>
        /// <returns></returns>
        public Quaternion Rotation()
        {
            if (cameraPoseDriver)
                return cameraPoseDriver.transform.rotation;
            else
                return Camera.main.transform.rotation;
        }

        private void OnEnable() => cameraPoseDriver = GetComponent<TrackedPoseDriver>();
    }
}