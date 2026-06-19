using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using NSYNK.HyperSlides.XR;


#if UNITY_IOS
using UnityEngine.XR.ARKit;
#endif

namespace NSYNK.HyperSlides.XR
{
    /// <summary>
    /// allows placing of additional anchors for better tracking performance, mainly for iOS
    /// </summary>
    public class XRAdditionalAnchorsManager : Singleton<XRAdditionalAnchorsManager>
    {
        ARRaycastManager m_RaycastManager;
        ARAnchorManager m_AnchorManager;

        public GameObject XROrigin;

        [SerializeField]
        GameObject m_Prefab;
        public GameObject prefab
        {
            get => m_Prefab;
            set => m_Prefab = value;
        }

        static List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();
        
        /// <summary>
        /// all created regular anchors
        /// </summary>
        List<ARAnchor> m_Anchors = new List<ARAnchor>();


        void Start()
        {
            XROrigin = GameObject.Find("XR Origin");
            m_RaycastManager = XROrigin.GetComponent<ARRaycastManager>();
            if (m_RaycastManager == null)
            {
                m_RaycastManager = XROrigin.AddComponent<ARRaycastManager>();
            }
            m_AnchorManager = XROrigin.GetComponent<ARAnchorManager>();
        }

        /// <summary>
        /// Remove all current anchors from the ARSession
        /// </summary>
        public void RemoveAllAnchors()
        {
            m_Anchors.ForEach(anchor =>
            {
                if (anchor != null)
                    Destroy(anchor.gameObject);
            });

            m_Anchors.Clear();

#if UNITY_IOS && !UNITY_EDITOR
            XRWorldMapManager.SaveWorldMapAsync();
#endif
        }

        ARAnchor CreateAnchor(in ARRaycastHit hit)
        {
            ARAnchor anchor = null;

            // If we hit a plane, try to "attach" the anchor to the plane
            if (hit.trackable is ARPlane plane)
            {
                var planeManager = GetComponent<ARPlaneManager>();
                if (planeManager)
                {
                    Debug.Log("Creating anchor attachment.");
                    var oldPrefab = m_AnchorManager.anchorPrefab;
                    m_AnchorManager.anchorPrefab = prefab;
                    anchor = m_AnchorManager.AttachAnchor(plane, hit.pose);
                    m_AnchorManager.anchorPrefab = oldPrefab;
                    return anchor;
                }
            }

            // Otherwise, just create a regular anchor at the hit pose
            Debug.Log("Creating regular anchor.");

            // Note: the anchor can be anywhere in the scene hierarchy
            var gameObject = Instantiate(prefab, hit.pose.position, hit.pose.rotation);

            // Make sure the new GameObject has an ARAnchor component
            anchor = gameObject.GetComponent<ARAnchor>();
            if (anchor == null)
            {
                anchor = gameObject.AddComponent<ARAnchor>();
            }

            return anchor;
        }

        public void OnAddAnchorPressed()
        {
            Vector3 pos = AddAnchor();
            Debug.Log($"Anchor added at {pos}");
        }

        public Vector3 AddAnchor()
        {
            ARAnchor ignored;
            return AddAnchorByRaycast(out ignored);
        }

        public Vector3 AddAnchorByRaycast(out ARAnchor newAnchor, bool addToList = true)
        {
            // Raycast against planes and feature points
            const TrackableType trackableTypes =
                TrackableType.FeaturePoint |
                TrackableType.PlaneWithinPolygon;

            // Perform the raycast
            if (m_RaycastManager.Raycast(new Vector2(Screen.width / 2, Screen.height / 2), s_Hits, trackableTypes))
            {
                // Raycast hits are sorted by distance, so the first one will be the closest hit.
                var hit = s_Hits[0];

                // Create a new anchor
                var anchor = CreateAnchor(hit);
                if (anchor)
                {
                    // Remember the anchor so we can remove it later.
                    m_Anchors.Add(anchor);
                    newAnchor = anchor;
#if UNITY_IOS && !UNITY_EDITOR
                    XRWorldMapManager.SaveWorldMapAsync();
#endif
                    return hit.pose.position;
                }
                else
                {
                    Debug.Log("Error creating anchor");
                    newAnchor = null;
                    return Vector3.zero;
                }
            }
            newAnchor = null;
            return Vector3.zero;
        }
    }


    public static class XRAnchorManagerExtension
    {

    }
}
