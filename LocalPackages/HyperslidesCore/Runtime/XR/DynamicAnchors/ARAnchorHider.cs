using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ARAnchorHider : MonoBehaviour
{
    private static List<ARAnchorHider> instances = new List<ARAnchorHider>();
    public List<GameObject> meshGOs;

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
        if (ARDebugger.Instance != null)
            InitialVisibility = ARDebugger.Instance.ShowDebugObjects;
        SetVisible(InitialVisibility);
    }

    public static void SetInstancesVisible(bool visibility)
    {
        foreach (ARAnchorHider instance in instances)
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
        foreach (GameObject go in meshGOs)
        {
            if (go != null)
            {
                go.SetActive(visiblity);
            }
        }
    }
}
