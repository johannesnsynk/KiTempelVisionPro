using System.Collections.Generic;
using UnityEngine;
using NSYNK.HyperSlides.Network;

namespace NSYNK.HyperSlides.Runtime
{
    /// <summary>
    /// Manages the spectator environment for simulation role users.
    /// </summary>
    public class SpectatorManager : Singleton<SpectatorManager>
    {
        public GameObject SpectatorEnvironment;
        public List<GameObject> gameObjectsToHide;

        private GameObject spectatorEnvironmentRuntime;

        private void OnEnable() => XRNetworkManager.Instance.OnUserConnected += CheckUserRole;
        private void OnDisable()
        {
            if (XRNetworkManager.Instance != null)
                XRNetworkManager.Instance.OnUserConnected -= CheckUserRole;
        }
        
        private void Start() => CheckUserRole();

        /// <summary>
        /// Checks the user's role and manages the spectator environment accordingly.
        /// </summary>
        private void CheckUserRole()
        {
            if (DeviceInfo.Instance.Role == XRPlayer.Role.Simulation)
            {
                if (spectatorEnvironmentRuntime == null)
                    spectatorEnvironmentRuntime = Instantiate(SpectatorEnvironment, new Vector3(0.0f, 0.0f, 0.0f), Quaternion.identity);

                gameObjectsToHide.ForEach(go => go.SetActive(false));
            }
            else
            {
                if (spectatorEnvironmentRuntime != null)
                    Destroy(spectatorEnvironmentRuntime);

                gameObjectsToHide.ForEach(go => go.SetActive(true));
            }
        }
    }
}