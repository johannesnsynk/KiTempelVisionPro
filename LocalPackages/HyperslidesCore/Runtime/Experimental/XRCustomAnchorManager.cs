// This script is testing inheritance of XRAnchorManager of the main Hyperslides library
using UnityEngine;
using NSYNK.HyperSlides.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class XRCustomAnchorManager : XRAnchorManager
{   
    Vector3 leftBannerPosition, rightBannerPosition;
    protected override void UpdateWorldAnchorWithTrackable(ARTrackable trackable)
    {
        // base.UpdateWorldAnchorWithTrackable(trackable);
        string name = "";
        if (trackable is ARTrackedObject aRTrackedObject) 
        { 
            name = aRTrackedObject.referenceObject.name.Split('_')[0];
        } 
        else if (trackable is ARTrackedImage aRTrackedImage)
        {
            name = aRTrackedImage.referenceImage.name.Split('_')[0];
        }
        if (trackable && trackable.trackingState == TrackingState.Tracking)
        {
            if (name == "SpatialTracking")
            {
                leftBannerPosition = trackable.transform.position;

                // positionAnchor.UpdateAnchor(trackable);
                // positionAnchor.transform.position = trackable.transform.position;
            }
            if (name == "WorldAnchor")
            {
                rightBannerPosition = trackable.transform.position;

                // rotationAnchor.UpdateAnchor(trackable);
                // rotationAnchor.transform.position = trackable.transform.position;
            }

            if (leftBannerPosition != null && rightBannerPosition != null)
            {
                Vector3 middlePoint = (leftBannerPosition + rightBannerPosition) / 2;
                Vector3 right = leftBannerPosition - rightBannerPosition;
                Vector3 forward = Vector3.Cross(right, Vector3.up).normalized;
                Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);

                positionAnchor.UpdateTargetPositionLocation(middlePoint, rotation);
                rotationAnchor.UpdateTargetPositionLocation(middlePoint + forward, rotation);
            }

        }
    }
}
