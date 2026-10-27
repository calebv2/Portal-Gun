using UnityEngine;
namespace PortalGunNative
{
 // Editor/standalone visual preview only; ATT uses NativeDoor and NativeGate.
 [ExecuteAlways]
 public sealed class PortalDoorPreviewLink:MonoBehaviour
 {
  public Transform Entrance,Destination;
  public Material Surface;
  void OnEnable(){Refresh();}
  void Update(){Refresh();}
  void Refresh()
  {
   if(Entrance==null||Destination==null||Surface==null)return;
   var view=GetComponent<PortalView>();if(view==null)view=gameObject.AddComponent<PortalView>();
   view.Setup(Entrance,Surface);view.SetExit(true,Destination.position,Destination.forward,Destination.up);
  }
 }
}
