using UnityEngine;

public class PortalPairGun : MonoBehaviour
{
    public Transform Aim;
    public float ShotRange=2000;
    public LayerMask SurfaceLayers=~0;
    public PortalEndpoint Entrance,Exit;
    public string Status="Place both portals: left click blue, right click orange.";
    public bool Fire(bool orange)
    {
        if(Aim==null) {Status="Assign an aim transform.";return false;}
        RaycastHit hit;
        if(!Physics.Raycast(Aim.position,Aim.forward,out hit,ShotRange,SurfaceLayers,QueryTriggerInteraction.Ignore)) {Status="No solid surface in range.";return false;}
        if(hit.collider.GetComponentInParent<PortalTraveller>()!=null) {Status="Aim at an object or world surface.";return false;}
        var other=orange?Entrance:Exit;
        if(other!=null && Vector3.Distance(hit.point,other.transform.position)<2.3f) {Status="Place the portals farther apart.";return false;}
        var portal=orange?Exit:Entrance;
        if(portal==null) portal=new GameObject(orange?"Orange exit portal":"Blue entrance portal").AddComponent<PortalEndpoint>();
        portal.Place(hit,orange?new Color(1,.38f,.03f):new Color(.05f,.55f,1));
        if(orange)Exit=portal;else Entrance=portal;
        if(Entrance!=null && Exit!=null) {Entrance.Other=Exit;Exit.Other=Entrance;}
        Status=Entrance!=null && Exit!=null?"Linked. Walk into either portal. R clears both.":"Place the other portal to connect them.";
        return true;
    }
    public void Clear() {if(Entrance!=null)Destroy(Entrance.gameObject);if(Exit!=null)Destroy(Exit.gameObject);Entrance=null;Exit=null;Status="Portals cleared.";}
    void OnDestroy(){Clear();}
}
