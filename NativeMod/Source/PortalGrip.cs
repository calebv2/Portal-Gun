using HarmonyLib;
using MelonLoader;
using UnityEngine;
namespace PortalGunNative
{
 // Docked grab memories can otherwise replace the newly configured grip.
 [HarmonyPatch(typeof(Pickup),nameof(Pickup.EnsureBestGrabFound))]
 public static class PortalGrip
 {
  static bool Prefix(Pickup __instance,Interactor interactor)
  {
   if(__instance.GetComponent<NativeGun>()==null||interactor==null)return true;
   var points=__instance.GrabPoints;if(points==null||points.Length==0)return true;
   var point=points[0];var origin=__instance.transform;
   point.CheckScore(origin,1f,1f,.005f,origin.InverseTransformPoint(interactor.InteractionPoint.position),
    Quaternion.Inverse(origin.rotation)*interactor.InteractionPoint.rotation,interactor.Controller!=null&&interactor.Controller.Hand!=null&&interactor.Controller.Hand.HandIndex==HandIndex.Left,false);
   // A pole grip is not a pointing frame. Derive the barrel direction from
   // wrist to middle knuckle, with the thumb side of the fist as gun-up.
   // ATT's own MasterHand uses index=0, little=1 and middle=2 children.
   var hand=interactor.Controller!=null?interactor.Controller.Hand:null;
   var wrist=hand!=null&&hand.MasterHand!=null?hand.MasterHand.IKHandRoot:null;
   Quaternion aim=interactor.Controller!=null?interactor.Controller.transform.rotation:interactor.InteractionPoint.rotation;
   bool anatomical=false;
   if(wrist!=null&&wrist.childCount>=5)
   {
    var forward=wrist.GetChild(2).position-wrist.position;
    var up=wrist.GetChild(0).position-wrist.GetChild(1).position;
    if(forward.sqrMagnitude>.00001f&&Vector3.Cross(forward,up).sqrMagnitude>.00000001f)
    {aim=PortalTransitMath.FistRotation(wrist.position,wrist.GetChild(2).position,wrist.GetChild(0).position,wrist.GetChild(1).position);anatomical=true;}
   }
   point.LastRotation=Quaternion.Inverse(aim)*interactor.InteractionPoint.rotation;
   MelonLogger.Msg("PORTALGUN_GRIP frame="+(anatomical?"wrist-to-knuckles":"controller fallback")+" local="+point.LastRotation.eulerAngles);
   __instance.SetupGrabPoint(interactor,point);return false;
  }
 }
}
