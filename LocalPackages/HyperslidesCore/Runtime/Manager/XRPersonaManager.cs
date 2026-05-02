using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;

namespace NSYNK.HyperSlides.Runtime
{
    public class XRPersonaManager : Singleton<XRPersonaManager>
    {
        public Material videoMaterial;

        internal Dictionary<uint, GameObject> personaGamePairs = new Dictionary<uint, GameObject>();

        public override void Awake()
        {
            base.Awake();

            SetNativeCallback(CallbackFromNative);
        }

        #region VISION OS Persona integration
        public void StartSharePlay()
        {
            Debug.Log("Start share play");
            StartGroupActivity();
        }

        [MonoPInvokeCallback(typeof(CallbackDelegate))]
        static void CallbackFromNative(string command)
        {
            Debug.Log("Callback from native: " + command);
        }

        delegate void CallbackDelegate(string command);

#if UNITY_VISIONOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void SetNativeCallback(CallbackDelegate callback);

        [DllImport("__Internal")]
        static extern void StartGroupActivity();

        [DllImport("__Internal")]
        static extern void StopGroupActivity();
#else
        static void SetNativeCallback(CallbackDelegate callback) { }
        static void StartGroupActivity() { }
        static void StopGroupActivity() { }
#endif
        #endregion
    }
}