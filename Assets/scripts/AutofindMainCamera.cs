using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AutofindMainCamera : MonoBehaviour
{
    void Start()
    {
        if(TryGetComponent(out Canvas canvas))
            canvas.worldCamera = Camera.main;
    }
}
