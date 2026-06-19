using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class ARDebugVisualizerHider : MonoBehaviour
{
    private MeshRenderer meshRenderer;
    private LineRenderer lineRenderer;
    private ARPlaneMeshVisualizer meshVisualizer;

    private static List<ARDebugVisualizerHider> instances = new List<ARDebugVisualizerHider>();

    public bool InitialVisibility = false;

    private void Awake()
    {
        instances.Add(this);
    }
    private void OnDestroy()
    {
        instances.Remove(this);
    }

    private void Start()
    {
        meshRenderer = this.GetComponent<MeshRenderer>();
        lineRenderer = this.GetComponent<LineRenderer>();
        meshVisualizer = this.GetComponent<ARPlaneMeshVisualizer>();

        if (ARDebugger.Instance != null)
            InitialVisibility = ARDebugger.Instance.ShowDebugObjects;
        SetVisible(InitialVisibility);
    }

    public static void SetInstancesVisible(bool visibility)
    {
        foreach (ARDebugVisualizerHider instance in instances)
        {
            if (instance != null)
            {
                instance.SetVisible(visibility);
            }
            else
            {
                instances.Remove(instance);
            }
        }
    }

    public void SetVisible(bool visiblity)
    {
        Debug.Log($"Setting visibility of {this.gameObject.name}, {visiblity}");
        meshVisualizer.enabled = visiblity;
        meshRenderer.enabled = visiblity;
        lineRenderer.enabled = visiblity;
    }
}
