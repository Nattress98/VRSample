using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR;

public class NetworkedPlayerManager : MonoBehaviour
{
    public NetworkManager manager;
    public NetworkObject vrPlayerPrefab, pcPlayerPrefab; //Probably switch to NetworkObject

    void Start()
    {
        manager.OnClientConnectedCallback += (id) => SpawnPlayer(HeadsetConnected(), id);
    }
    public void SpawnPlayer(bool isVR, ulong id)
    {
        if (id == manager.LocalClient.ClientId)
        {
            NetworkObject go;
            if (isVR)
                go = Instantiate(vrPlayerPrefab);
            else
                go = Instantiate(pcPlayerPrefab);
            go.Spawn();
            go.GetComponent<INetworkUser>().OnSpawn(id);
        }
    }

    public static bool HeadsetConnected()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return true;
#endif
        var inputDevices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeadMounted, inputDevices);
        return inputDevices.Count > 0;
    }
}
