using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class PlatformChecker : MonoBehaviour
{
    public UnityEvent isVR, isPC;
    private void OnEnable()
    {
        if (NetworkedPlayerManager.HeadsetConnected())
            isVR.Invoke();
        else
            isPC.Invoke();
    }
}
