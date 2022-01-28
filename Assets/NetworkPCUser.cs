using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

public class NetworkPCUser : MonoBehaviour, INetworkUser
{
    public UnityEvent setupLocalUser = new UnityEvent();

    public void OnSpawn(ulong ownerId)
    {
        if(ownerId == NetworkManager.Singleton.LocalClientId)
            setupLocalUser.Invoke();

    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
