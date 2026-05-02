using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NSYNK.HyperSlides.Network;
using NSYNK.HyperSlides.Runtime;

public class SpectatorManager : MonoBehaviour
{
    public GameObject SpectatorEnvironment;

    private GameObject spectatorEnvironmentRuntime;

    public List<GameObject> gameObjectsToHide;

    void OnEnable() => XRNetworkManager.onUserConnected += CheckUserRole;
    void OnDisable() => XRNetworkManager.onUserConnected -= CheckUserRole;

    void Start()
    {
        CheckUserRole();
    }

    void CheckUserRole() 
    {
        if (DeviceInfo.Role == XRPlayer.Role.Simulation)
        {
            if (spectatorEnvironmentRuntime == null)
                spectatorEnvironmentRuntime = Instantiate(SpectatorEnvironment, new Vector3(0.0f, 0.0f, 0.0f), Quaternion.identity);

            foreach (GameObject gO in gameObjectsToHide) 
            {
                gO.SetActive(false);
            }
        }
        else
        {
            if (spectatorEnvironmentRuntime != null)
                Destroy(spectatorEnvironmentRuntime);

            foreach (GameObject gO in gameObjectsToHide) 
            {
                gO.SetActive(true);
            }
        }
    }
}
