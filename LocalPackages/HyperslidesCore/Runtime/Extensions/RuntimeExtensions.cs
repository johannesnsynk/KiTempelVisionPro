using System;
using UnityEngine;

namespace NSYNK.HyperSlides.Runtime
{
    public class RuntimeExtensions : MonoBehaviour
    {
        public void ToggleGameObject(GameObject go) => go.SetActive(!go.activeInHierarchy);
    }
}