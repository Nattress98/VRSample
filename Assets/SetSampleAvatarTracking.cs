using Oculus.Avatar2;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SetSampleAvatarTracking : MonoBehaviour
{
    public void SetupSampleAvatarTrackingLocal()
    {
        //Has to be called after enables
        SampleAvatarEntity avatar = GetComponentInChildren<SampleAvatarEntity>();
        avatar.SetBodyTracking(FindObjectOfType<SampleInputManager>());
        avatar.SetLipSync(FindObjectOfType<OvrAvatarLipSyncContext>());

    }
}
