using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DesktopPlayerController : MonoBehaviour
{
    private CharacterController cc;
    public Transform cam;
    public float speed;
    void Start()
    {
        cc = GetComponent<CharacterController>();
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    float mouseY;
    void Update()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 dir = Vector3.Scale(transform.forward * v + transform.right * h, new Vector3(1, 0, 1)).normalized;

        cc.Move(speed * Time.deltaTime * dir);

        float mouseX = Input.GetAxisRaw("Mouse X");
        transform.Rotate(new Vector3(0, mouseX, 0));

        mouseY += Input.GetAxis("Mouse Y");
        mouseY = Mathf.Clamp(mouseY, -60, 60);
        cam.localEulerAngles = new Vector3(-mouseY, 0, 0);
    }
}
