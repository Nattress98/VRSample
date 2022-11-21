using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR;

public class NetworkedPlayerManager : MonoBehaviour
{
    public static NetworkedPlayerManager Instance;
    public GameObject vrPlayerPrefab, pcPlayerPrefab; //Probably switch to NetworkObject
    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogError($"Multiple instances of {this} not supported. Destroying...");
            Destroy(this);
        }
        Instance = this;
    }
    void Start()
    {
    }
    [ServerRpc(RequireOwnership = false)]
    public void SpawnPlayerServerRPC(bool isVR, ulong id)
    {
        Debug.Log("ONLY SERVER CALLED: " + NetworkManager.Singleton.IsServer + " Host: " + NetworkManager.Singleton.IsHost);
        GameObject go;
        if (isVR)
            go = Instantiate(vrPlayerPrefab);
        else
            go = Instantiate(pcPlayerPrefab);
        NetworkObject no = go.GetComponent<NetworkObject>();
        no.SpawnWithOwnership(id);
    }
    public static bool HeadsetConnected()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return true;
#endif
        /**var inputDevices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeadMounted, inputDevices); //DOESNT WORK???
        Debug.Log("Headsets: " + inputDevices.Count);
        return inputDevices.Count > 0;**/
        var xrDisplaySubsystems = new List<XRDisplaySubsystem>();
        SubsystemManager.GetInstances<XRDisplaySubsystem>(xrDisplaySubsystems);
        foreach (var xrDisplay in xrDisplaySubsystems)
        {
            if (xrDisplay.running)
            {
                return true;
            }
        }
        return false;
    }
}
