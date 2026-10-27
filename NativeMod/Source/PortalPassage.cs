using System.Collections.Generic;
using System.Linq;
using Alta.Character;
using Alta.Chunks;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
namespace PortalGunNative
{
 // A collision lease is limited to the local capsule and the two supporting
 // surfaces. It never disables a world collider or changes its layer.
 public sealed class PortalPassage:MonoBehaviour
 {
  static readonly HashSet<PortalPassage> all=new HashSet<PortalPassage>();
  static PortalPassage active;
  static float transitUntil;
  static readonly Dictionary<Camera,float> nearPlanes=new Dictionary<Camera,float>();
  bool linked,crossed,mustLeave,relocating;Matrix4x4 exit;Vector3 previousHead,recoveryCenter;bool hasPrevious;
  CharacterController capsule;Collider entrySurface,exitSurface;
  readonly List<Collider> leased=new List<Collider>();
  readonly List<bool> previousIgnore=new List<bool>();
  Vector3 savedFeet;Quaternion savedRotation;float lastBlockedLog,enteredAt;
  string releaseReason="clear";
  float halfWidth=.72f,halfHeight=1.08f;bool rectangular;
  Vector3 entryAtLease;Quaternion entryRotationAtLease;
  public void SetAperture(float width,float height,bool rectangle){halfWidth=width;halfHeight=height;rectangular=rectangle;}
  public static bool IsPassing(SmoothLocomotion motion){return active!=null&&active.capsule==motion.CharacterController;}
  public static bool IsRelocating(SmoothLocomotion motion){return IsPassing(motion)&&active.relocating;}
  void OnEnable(){if(all.Count==0)Camera.onPreCull+=BeforeCamera;all.Add(this);}
  static void BeforeCamera(Camera camera)
  {
   var player=LocalPlayer;
   if(active!=null&&player!=null&&player.Head!=null&&camera.gameObject.hideFlags!=HideFlags.HideAndDontSave&&Vector3.Distance(camera.transform.position,player.Head.position)<.4f)
   {if(!nearPlanes.ContainsKey(camera))nearPlanes.Add(camera,camera.nearClipPlane);camera.nearClipPlane=Mathf.Min(nearPlanes[camera],.005f);}
   else if(nearPlanes.ContainsKey(camera)){camera.nearClipPlane=nearPlanes[camera];nearPlanes.Remove(camera);}
  }
  public void SetExit(bool ready,Vector3 p,Vector3 n,Vector3 u)
  {
   if(active==this&&linked&&ready&&(Vector3.Distance(exit.MultiplyPoint(Vector3.zero),p)>.05f||Vector3.Angle(exit.MultiplyVector(Vector3.forward),n)>2))
   {releaseReason="destination moved during crossing";Release(true);}
   linked=ready&&n.sqrMagnitude>.5f&&u.sqrMagnitude>.5f;
   if(linked)exit=Matrix4x4.TRS(p,Quaternion.LookRotation(n,u),Vector3.one);
   else Release(true);
  }
  static SmoothLocomotionPlayerController LocalPlayer=>PlayerController.Current as SmoothLocomotionPlayerController;
  public static void BeforeMove(SmoothLocomotion motion)
  {
   var player=LocalPlayer;if(player==null||!player.IsLocalPlayer||player.SmoothLocomotion!=motion)return;
   if(active!=null){active.Maintain(player);return;}
   foreach(var portal in all.ToArray())if(portal!=null&&portal.TryAcquire(player))break;
  }
  bool TryAcquire(SmoothLocomotionPlayerController player)
  {
   if(!linked||player.IsDowned||player.Head==null||Time.time<transitUntil)return false;
   var cc=player.SmoothLocomotion.CharacterController;
   if(cc==null||!cc.enabled)return false;
   var center=cc.transform.TransformPoint(cc.center);
   float radius=cc.radius*Mathf.Max(Mathf.Abs(cc.transform.lossyScale.x),Mathf.Abs(cc.transform.lossyScale.z));
   float half=Mathf.Max(0,cc.height*Mathf.Abs(cc.transform.lossyScale.y)*.5f-radius);
   var axis=cc.transform.up;
   var local=transform.InverseTransformPoint(center);
   float support=radius+half*Mathf.Abs(Vector3.Dot(axis,transform.forward));
   if(mustLeave)
   {
    var away=local-recoveryCenter;
    if(away.z>.2f||away.x*away.x+away.y*away.y>.25f*.25f)mustLeave=false;
    else return false;
   }
   float approach=Mathf.Min(.5f,player.SmoothLocomotion.velocity.magnitude*Time.deltaTime+.12f);
   if(local.z<-.05f||local.z>support+approach||!Fits(center,axis,half,radius,transform.worldToLocalMatrix))return false;
   if(Vector3.Dot(player.Head.position-transform.position,transform.forward)<=0)return false;
   var chunk=LocationChunkHelper.GetBestContaining(exit.MultiplyPoint(Vector3.forward*.2f));
   if(chunk==null||!chunk.IsFullyLoaded)
   {if(Time.time>lastBlockedLog){lastBlockedLog=Time.time+5;MelonLogger.Warning("PORTALGUN_PASSAGE_BLOCKED destination must be loaded for continuous crossing");}return false;}
   entrySurface=FindSurface(transform.position,transform.forward);
   exitSurface=FindSurface(exit.MultiplyPoint(Vector3.zero),exit.MultiplyVector(Vector3.forward));
   if(entrySurface==null||exitSurface==null)return false;
   active=this;capsule=cc;crossed=false;hasPrevious=true;previousHead=player.Head.position;
   enteredAt=Time.time;releaseReason="clear";
   entryAtLease=transform.position;entryRotationAtLease=transform.rotation;
   // The acquisition point may already be within the controller's skin margin.
   // Recovery must clear the supporting surface before restoring collision.
   savedFeet=player.PlayerFeetPosition+transform.forward*Mathf.Max(0,support+cc.skinWidth+.15f-local.z);
   savedRotation=PortalTransitMath.UprightRotation(player.transform.rotation,player.Head.forward);
   Lease(entrySurface);Lease(exitSurface);
   MelonLogger.Msg("PORTALGUN_PASSAGE_ENTER");return true;
  }
  static bool HeldBy(Collider c,PlayerController player)
  {var pickup=c.GetComponentInParent<Pickup>();return pickup!=null&&pickup.Interactors.Any(i=>i!=null&&i.Controller!=null&&i.Controller.PlayerController==player);}
  Matrix4x4 Mapping=>exit*Matrix4x4.Rotate(Quaternion.Euler(0,180,0))*transform.worldToLocalMatrix;
  static Collider FindSurface(Vector3 p,Vector3 n)
  {
   var hits=Physics.RaycastAll(p+n*.12f,-n,.3f,~0,QueryTriggerInteraction.Ignore);
   foreach(var h in hits.OrderBy(h=>h.distance))if(Vector3.Dot(h.normal,n)>.7f&&(h.collider.GetComponentInParent<NativeGate>()==null||(h.collider.name=="PortalDoorBacking"&&h.collider.GetComponentInParent<NativeDoor>()!=null)))return h.collider;
   return null;
  }
  bool Fits(Vector3 center,Vector3 axis,float half,float radius,Matrix4x4 worldToPlane,float margin=0)
  {
   var c=worldToPlane.MultiplyPoint(center);var a=worldToPlane.MultiplyVector(axis);
   return PortalTransitMath.FitsCapsule(c,a,half,radius,halfWidth+margin,halfHeight+margin,rectangular);
  }
  void Lease(Collider collider)
  {
   if(leased.Contains(collider))return;
   leased.Add(collider);previousIgnore.Add(Physics.GetIgnoreCollision(capsule,collider));Physics.IgnoreCollision(capsule,collider,true);
  }
  void LateUpdate()
  {
   if(active!=this)return;
   var player=LocalPlayer;
   if(player==null||player.Head==null){Release(false);return;}
   Maintain(player);if(active!=this)return;
   var head=player.Head.position;var plane=crossed?exit.inverse:transform.worldToLocalMatrix;
   var old=plane.MultiplyPoint(previousHead);var now=plane.MultiplyPoint(head);
   previousHead=head;
   if(hasPrevious&&PortalTransitMath.CrossesPlane(old,now,halfWidth,halfHeight,rectangular))Cross(player,head);
   hasPrevious=true;
  }
  void Cross(SmoothLocomotionPlayerController player,Vector3 head)
  {
   var map=crossed?Mapping.inverse:Mapping;
   var rotation=exit.rotation*Quaternion.Euler(0,180,0)*Quaternion.Inverse(transform.rotation);
   if(crossed)rotation=Quaternion.Inverse(rotation);
   var motion=player.SmoothLocomotion;
   var velocity=motion.CurrentMovingVelocity;if(motion.velocity.sqrMagnitude>velocity.sqrMagnitude)velocity=motion.velocity;
   var targetPlane=crossed?transform.localToWorldMatrix:exit;
   var sourceNormal=crossed?exit.MultiplyVector(Vector3.forward):transform.forward;
   var upright=PortalTransitMath.TravelHeading(player.transform.rotation,player.Head.forward,rotation,sourceNormal,targetPlane.MultiplyVector(Vector3.forward));
   var yawDelta=upright*Quaternion.Inverse(player.transform.rotation);
   var targetHead=map.MultiplyPoint(head);
   float radius=capsule.radius*Mathf.Max(Mathf.Abs(capsule.transform.lossyScale.x),Mathf.Abs(capsule.transform.lossyScale.z));
   float half=Mathf.Max(0,capsule.height*Mathf.Abs(capsule.transform.lossyScale.y)*.5f-radius);
   var targetCenter=PortalTransitMath.HeadAnchoredPoint(map,head,capsule.transform.TransformPoint(capsule.center),yawDelta);
   var targetBody=PortalTransitMath.HeadAnchoredPoint(map,head,capsule.transform.position,yawDelta);
   if(Obstructed(targetCenter,half,radius,player))
   {releaseReason="head-anchored exit obstructed";Release(true);return;}
   // Preserve the viewpoint shown through the portal. The capsule may still
   // straddle its backing surface; keep the collision lease until it emerges.
   // Native HandleTeleport otherwise adds skin-width and performs a zero Move,
   // which can depenetrate/recenter the player before they have emerged.
   PortalViewLeveling.Cancel();
   relocating=true;
   try
   {
    player.LocomotionController.RotateTo(upright,LocomotionStyle.Instant,0);
    player.LocomotionController.MoveTo(targetHead,TeleportMode.Head,LocomotionFunction.Reposition);
    capsule.transform.position=targetBody;
    Physics.SyncTransforms();
   }
   catch(System.Exception error)
   {
    relocating=false;releaseReason="native relocation failed";
    MelonLogger.Error("PORTALGUN_CROSS_FAILED "+error.Message);Release(true);return;
   }
   finally{relocating=false;}
   var actualCenter=capsule.transform.TransformPoint(capsule.center);
   if(Obstructed(actualCenter,half,radius,player))
   {releaseReason="native body offset obstructed exit";Release(true);return;}
   float headError=Vector3.Distance(player.Head.position,targetHead);
   if(headError>.03f){releaseReason="native head alignment error "+headError;Release(true);return;}
   crossed=!crossed;
   motion.velocity=rotation*velocity;
   Core.Set(motion,"currentCollisionFlags",CollisionFlags.None);
   player.LocomotionController.IsGrounded=false;
   motion.TreatAsGrounded(); // Reset the fall-height origin to this world location.
   enteredAt=Time.time;
   previousHead=player.Head.position;
   MelonLogger.Msg("PORTALGUN_CONTINUOUS_CROSS headError="+headError+" up="+Vector3.Dot(player.transform.up,Vector3.up)+" reverse="+!crossed);
  }
  bool Obstructed(Vector3 center,float half,float radius,PlayerController player)
  {
   return Physics.OverlapCapsule(center+Vector3.up*half,center-Vector3.up*half,radius,~0,QueryTriggerInteraction.Ignore)
    .Any(c=>c!=capsule&&c!=entrySurface&&c!=exitSurface&&!c.transform.IsChildOf(player.transform)&&!HeldBy(c,player));
  }
  void Maintain(SmoothLocomotionPlayerController player)
  {
   if(!linked||capsule==null||entrySurface==null||exitSurface==null||!entrySurface.enabled||!exitSurface.enabled){releaseReason="surface unavailable";Release(true);return;}
   if(rectangular&&(Vector3.Distance(entryAtLease,transform.position)>.05f||Quaternion.Angle(entryRotationAtLease,transform.rotation)>2))
   {releaseReason="door moved during crossing";Release(true);return;}
   // Native height updates can recreate a controller's physics shape.
   // Reassert only the leased pairs, preserving their original restore values.
   foreach(var collider in leased)if(collider!=null&&!Physics.GetIgnoreCollision(capsule,collider))Physics.IgnoreCollision(capsule,collider,true);
   var plane=crossed?exit.inverse:transform.worldToLocalMatrix;
   var center=capsule.transform.TransformPoint(capsule.center);var axis=capsule.transform.up;
   float radius=capsule.radius*Mathf.Max(Mathf.Abs(capsule.transform.lossyScale.x),Mathf.Abs(capsule.transform.lossyScale.z));
   float half=Mathf.Max(0,capsule.height*Mathf.Abs(capsule.transform.lossyScale.y)*.5f-radius);
   var c=plane.MultiplyPoint(center);var a=plane.MultiplyVector(axis);
   float support=radius+half*Mathf.Abs(a.z);
   if(PortalTransitMath.UnsafePassage(c.z,support,Time.time-enteredAt))
   {releaseReason="time/depth guard";Release(true);return;}
   // Gates are 4 cm in front of their backing surfaces. A trailing end 3 cm
   // behind the visual plane is still 1 cm clear of the actual collider.
   if(c.z>support+(crossed?-.03f:.18f))
   {releaseReason=crossed?"trailing body cleared exit":"withdrew from entry";transitUntil=Time.time+.12f;Release(false);return;}
   // Allow a small rim tolerance after acquisition so native capsule resizing
   // and a single movement step cannot cause enter/recover every frame.
   if(!crossed&&!Fits(center,axis,half,radius,plane,.10f)){releaseReason="outside aperture";Release(true);return;}
   if(crossed)
   {
    // Upright legs do not share the mapped orientation of a floor-to-wall
    // crossing. Follow the head through the opening until the body clears.
    var h=plane.MultiplyPoint(player.Head.position);
    if(rectangular?(Mathf.Abs(h.x)>halfWidth+.1f||Mathf.Abs(h.y)>halfHeight+.1f):h.x*h.x/((halfWidth+.1f)*(halfWidth+.1f))+h.y*h.y/((halfHeight+.1f)*(halfHeight+.1f))>1)
    {releaseReason="head left exit aperture before body cleared";Release(true);}
   }
  }
  void Release(bool recover)
  {
   if(active!=this)return;
   var player=LocalPlayer;
   try
   {if(recover&&player!=null&&capsule!=null)
   {
    mustLeave=true;transitUntil=Time.time+.5f;
    PortalViewLeveling.Cancel();
    // Portal closure or moving out through its rim must not leave the player
    // embedded in the supporting collider when its collision is restored.
    player.LocomotionController.RotateTo(savedRotation,LocomotionStyle.Instant,0);
    player.LocomotionController.MoveTo(savedFeet,TeleportMode.Feet,LocomotionFunction.Reposition);
    player.SmoothLocomotion.velocity=Vector3.zero;
    recoveryCenter=transform.InverseTransformPoint(capsule.transform.TransformPoint(capsule.center));
   }}
   finally
   {
    for(int i=0;i<leased.Count;i++)if(capsule!=null&&leased[i]!=null)Physics.IgnoreCollision(capsule,leased[i],previousIgnore[i]);
    leased.Clear();previousIgnore.Clear();capsule=null;active=null;hasPrevious=false;
    MelonLogger.Msg("PORTALGUN_PASSAGE_END recovered="+recover+" reason="+releaseReason+" duration="+(Time.time-enteredAt));
   }
  }
  void OnDisable()
  {
   Release(true);all.Remove(this);
   if(all.Count==0){Camera.onPreCull-=BeforeCamera;foreach(var pair in nearPlanes)if(pair.Key!=null)pair.Key.nearClipPlane=pair.Value;nearPlanes.Clear();}
  }
 }
 [HarmonyPatch(typeof(SmoothLocomotion),nameof(SmoothLocomotion.ApplyMovement))]
 static class PortalMovementCollision
 {static void Prefix(SmoothLocomotion __instance){PortalPassage.BeforeMove(__instance);}}
 [HarmonyPatch(typeof(SmoothLocomotion),"HandleTeleport")]
 static class PortalNativeRecenter
 {static bool Prefix(SmoothLocomotion __instance){return !PortalPassage.IsRelocating(__instance);}}
 [HarmonyPatch(typeof(SmoothLocomotion),nameof(SmoothLocomotion.RepositionCharacter),new[]{typeof(float)})]
 static class PortalHeadWallCheck
 {static bool Prefix(SmoothLocomotion __instance){return !PortalPassage.IsPassing(__instance);}}
 [HarmonyPatch(typeof(SmoothLocomotion),"CheckForInfiniteFall")]
 static class PortalFallCheck
 {static bool Prefix(SmoothLocomotion __instance){return !PortalPassage.IsPassing(__instance);}}
 [HarmonyPatch(typeof(SmoothLocomotion),"IsCloseToGround")]
 static class PortalGroundCheck
 {
  static bool Prefix(SmoothLocomotion __instance,ref Vector3 gravityAndSlide,ref bool __result)
  {if(!PortalPassage.IsPassing(__instance))return true;gravityAndSlide=Vector3.zero;__result=false;return false;}
 }
}
