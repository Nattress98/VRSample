using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

public class NetworkVRUser : MonoBehaviour, INetworkUser
{
    public UnityEvent setupLocalUser = new UnityEvent();
    public void OnSpawn(ulong ownerId)
    {
        if (ownerId == NetworkManager.Singleton.LocalClientId)
            setupLocalUser.Invoke();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
