using UnityEngine;

[RequireComponent(typeof(PortalTraveller))]
public class PortalDemoPlayer : MonoBehaviour
{
    public Camera View;
    public PortalPairGun Gun;
    float pitch,fall;
    public void ResetFall(){fall=0;}
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Escape)){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        if(Cursor.lockState!=CursorLockMode.Locked)
        {
            if(Input.GetMouseButtonDown(0)){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
            return;
        }
        transform.Rotate(0,Input.GetAxis("Mouse X")*2,0);
        pitch=Mathf.Clamp(pitch-Input.GetAxis("Mouse Y")*2,-85,85);View.transform.localRotation=Quaternion.Euler(pitch,0,0);
        var body=GetComponent<CharacterController>();if(body.isGrounded && fall<0)fall=-2;
        fall+=Physics.gravity.y*Time.deltaTime;
        var movement=Vector3.ClampMagnitude(transform.right*Input.GetAxisRaw("Horizontal")+transform.forward*Input.GetAxisRaw("Vertical"),1)*4;
        body.Move((movement+Vector3.up*fall)*Time.deltaTime);
        if(Input.GetMouseButtonDown(0))Gun.Fire(false);if(Input.GetMouseButtonDown(1))Gun.Fire(true);
        if(Input.GetKeyDown(KeyCode.R))Gun.Clear();
    }
    void OnGUI()
    {
        GUI.Label(new Rect(20,20,850,25),"WASD move | Mouse aim | Left: blue entrance | Right: orange exit | R reset | Esc release mouse");
        GUI.Label(new Rect(20,48,850,25),Gun.Status);
        GUI.Label(new Rect(Screen.width/2-5,Screen.height/2-12,25,25),"+");
    }
    void OnDisable(){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
}
