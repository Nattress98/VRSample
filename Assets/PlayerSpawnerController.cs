using Unity.Netcode;
using UnityEngine;
public class PlayerSpawnerController : MonoBehaviour
{
    public void Start()
    {
        if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
            NetworkedPlayerManager.Instance.SpawnPlayerServerRPC(NetworkedPlayerManager.HeadsetConnected(), GetComponent<NetworkObject>().OwnerClientId);
        Destroy(gameObject);
    }

}
