using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class ARDebugger : Singleton<ARDebugger>
{
    private bool _showDebugObjects = false;
    public bool ShowDebugObjects
    {
        private set
        {
            _showDebugObjects = value;
        }
        get
        {
            return _showDebugObjects;
        }
    }

    public GameObject PlanePrefab;
    public GameObject PointPrefab;

    private ARPointCloudManager pointCloudManager;
    private ARPlaneManager planeManager;

    void OnEnable()
    {
        pointCloudManager = FindAnyObjectByType<ARPointCloudManager>(FindObjectsInactive.Include);
        planeManager = FindAnyObjectByType<ARPlaneManager>(FindObjectsInactive.Include);

        if (pointCloudManager != null)
            pointCloudManager.pointCloudPrefab = PointPrefab;
        if (planeManager != null)
        {
            planeManager.planePrefab = PlanePrefab;
            planeManager.trackablesChanged.AddListener(LogTrackables);
        }
    }

    void OnDisable()
    {
        if (planeManager != null)
            planeManager.trackablesChanged.RemoveListener(LogTrackables);
    }

    private void LogTrackables(ARTrackablesChangedEventArgs<ARPlane> arg0)
    {
        // foreach (var added in arg0.added)
        // {
        //     string layerName = GetLayerName(added.gameObject.layer);
        //     Debug.Log($"Added plane on layer '{layerName}' with position {added.transform.position}");
        // }

        // foreach (var updated in arg0.updated)
        // {
        //     string layerName = GetLayerName(updated.gameObject.layer);
        //     Debug.Log($"Updated plane on layer '{layerName}' with position {updated.transform.position}");
        // }

        // foreach (var removed in arg0.removed)
        // {
        //     Debug.Log($"Removed plane at {removed}");
        // }
    }

    private static string GetLayerName(int layer)
    {
        string layerName = LayerMask.LayerToName(layer);
        return string.IsNullOrEmpty(layerName) ? $"Layer {layer}" : layerName;
    }

    public void ToggleDebugObjects()
    {
        Debug.Log("Toggling debug objects");
        SetShowDebugObjects(!ShowDebugObjects);
    }

    public void ToggleDebugObjects(bool newValue)
    {
        Debug.Log("Toggling debug objects to " + newValue);
        SetShowDebugObjects(newValue);
    }

    public void SetShowDebugObjects(bool newValue)
    {
        ShowDebugObjects = newValue;
        ARDebugVisualizerHider.SetInstancesVisible(ShowDebugObjects);
        ARAnchorHider.SetInstancesVisible(ShowDebugObjects);
    }
}