using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetConnectManagement : MonoBehaviour
{
    public GameObject[] hiders;
    public void Join()
    {
        foreach (GameObject go in hiders)
            go.SetActive(false);
        NetworkManager.Singleton.StartClient();
    }
    public void Host()
    {
        foreach (GameObject go in hiders)
            go.SetActive(false);
        NetworkManager.Singleton.StartHost();
    }
}
