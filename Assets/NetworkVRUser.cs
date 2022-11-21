using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

public class NetworkVRUser : NetworkBehaviour
{
    public UnityEvent setupLocalUser = new UnityEvent();

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (GetComponent<NetworkObject>().IsOwner)
            setupLocalUser.Invoke();

        /*m_Transform = transform;
        m_ReplicatedNetworkState.OnValueChanged += OnNetworkStateChanged;

        CanCommitToTransform = IsOwner;
        m_CachedIsServer = IsServer;
        m_CachedNetworkManager = NetworkManager;

        if (CanCommitToTransform)
        {
            TryCommitTransformToServer(m_Transform, m_CachedNetworkManager.LocalTime.Time);
        }
        m_LocalAuthoritativeNetworkState = m_ReplicatedNetworkState.Value;

        // crucial we do this to reset the interpolators so that recycled objects when using a pool will
        //  not have leftover interpolator state from the previous object
        Initialize();*/

    }
}
