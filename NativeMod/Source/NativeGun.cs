using System;
using System.Linq;
using Alta.Chunks;
using Alta.Networking;
using Alta.Networking.Scripts.Player;
using Alta.Serialization;
using MelonLoader;
using UnityEngine;
using UnityEngine.XR;
using Valve.VR;
using System.Runtime.InteropServices;
namespace PortalGunNative
{
 public sealed class NativeGun:NetworkEntityBehaviour
 {
  MethodSync shot;Pickup pickup;bool pressed;float nextShot;int nextColor;
  readonly NativeGate[] gates=new NativeGate[2];
  Interactor lastHolder;string lastInputSource;
  static SteamVR_Action_Boolean triggerAction;
  MethodSync effect;Vector3 effectStart,effectEnd,effectColor;
  public override void Initialize(){base.Initialize();pickup=GetComponent<Pickup>();shot=new MethodSync(Entity,EntityMessageType.SyncDebugCommand,ShotPacket);effect=new MethodSync(Entity,EntityMessageType.SyncDebugCommandFull,EffectPacket);MelonLogger.Msg("PORTALGUN_INITIALIZED entity="+Entity.Identifier);}
  void EffectPacket(IPlayer sender,Stream stream)
  {
   var a=effectStart;var b=effectEnd;var c=effectColor;
   stream.SerializeVector3(ref a);stream.SerializeVector3(ref b);stream.SerializeVector3(ref c);
   if(stream.IsReading&&!NetworkSceneManager.IsServer&&!float.IsNaN(b.sqrMagnitude)&&!float.IsInfinity(b.sqrMagnitude))ShotVisual.Play(a,b,c.x==0);
  }
  void BroadcastEffect(Vector3 target)
  {effectStart=transform.TransformPoint(new Vector3(0,0,.3f));effectEnd=target;effectColor=new Vector3(nextColor,0,0);effect.SendToChunks();}
  void ShotPacket(IPlayer sender,Stream stream)
  {
   // Requests carry no positions or destinations. Server derives both from the held gun.
   var request=Vector3.one;stream.SerializeVector3(ref request);
   if(stream.IsReading&&NetworkSceneManager.IsServer&&request==Vector3.one)
   {
    var holder=pickup.Interactors.FirstOrDefault(i=>i!=null&&i.Controller!=null&&i.Controller.PlayerController!=null&&ReferenceEquals(i.Controller.PlayerController.NetworkPlayer,sender));
    if(holder!=null){MelonLogger.Msg("PORTALGUN_SHOT_RECEIVED entity="+Entity.Identifier);Fire(holder);}
    else MelonLogger.Warning("PORTALGUN_SHOT_REJECTED sender is not holding gun");
   }
  }
  void Update()
  {
   if(Entity==null||!Entity.IsInitialized||NetworkSceneManager.IsServer||pickup==null)return;
   var holder=pickup.Interactors.FirstOrDefault(i=>i!=null&&i.Controller!=null&&i.Controller.PlayerController!=null&&i.Controller.PlayerController.IsLocalPlayer);
   if(holder!=lastHolder){lastHolder=holder;pressed=true;MelonLogger.Msg("PORTALGUN_HELD entity="+Entity.Identifier+" hand="+(holder==null?"none":holder.Input.HandIndex.ToString()));}
   if(holder==null){pressed=true;return;}
   string source="no input";
   bool down=holder.Input!=null&&TriggerDown(holder.Input,out source);
   if(source!=lastInputSource){lastInputSource=source;MelonLogger.Msg("PORTALGUN_INPUT entity="+Entity.Identifier+" source="+source);}
   if(down&&!pressed&&Time.time>=nextShot){shot.SendToChunks();nextShot=Time.time+.35f;holder.Input.TriggerHaptic(.3f,.08f);MelonLogger.Msg("PORTALGUN_TRIGGER entity="+Entity.Identifier);}
   pressed=down;
  }
  static bool TriggerDown(PlayerInput input,out string source)
  {
   bool right=input.HandIndex==HandIndex.Right;
   // ATT uses action-based SteamVR input. Legacy controller state can return
   // a valid but empty state under this system. Read the existing raw trigger
   // action, before ATT turns it into a long-hold screenshot gesture.
   if(OpenVR.Input!=null)
   {
    if(triggerAction==null)triggerAction=SteamVR_Input.GetAction<SteamVR_Action_Boolean>("a_township_tale","takephotolonghold");
    var hand=right?SteamVR_Input_Sources.RightHand:SteamVR_Input_Sources.LeftHand;
    if(triggerAction!=null&&triggerAction[hand].activeBinding&&triggerAction[hand].active)
    {source="SteamVR trigger action";return triggerAction[hand].state;}
   }
   source="XR/OpenVR fallback";
   float axis;var device=InputDevices.GetDeviceAtXRNode(right?XRNode.RightHand:XRNode.LeftHand);
   if(device.isValid&&device.TryGetFeatureValue(CommonUsages.trigger,out axis)&&axis>.75f)return true;
   var system=OpenVR.System;
   if(system!=null)
   {
    uint index=system.GetTrackedDeviceIndexForControllerRole(right?ETrackedControllerRole.RightHand:ETrackedControllerRole.LeftHand);
    var value=new VRControllerState_t();
    if(system.GetControllerState(index,ref value,(uint)Marshal.SizeOf(typeof(VRControllerState_t))))
     if((value.ulButtonPressed&(1UL<<(int)EVRButtonId.k_EButton_SteamVR_Trigger))!=0)return true;
   }
   return input.RawInput.SecondaryAxis>.75f;
  }
  void Fire(Interactor holder)
  {
   if(Time.time<nextShot||holder.Controller.PlayerController.IsDowned)return;nextShot=Time.time+.35f;
   var origin=transform.position;var direction=transform.forward;
   var hits=Physics.RaycastAll(origin,direction,2000,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance);
   RaycastHit hit=default(RaycastHit);bool found=false;
   foreach(var candidate in hits)
   {
    if(candidate.collider.transform.IsChildOf(transform)||candidate.collider.GetComponentInParent<PlayerController>()!=null)continue;
    hit=candidate;found=true;break;
   }
   if(!found){BroadcastEffect(origin+direction*50);MelonLogger.Msg("PORTALGUN_SHOT_MISS");return;}
   if(Core.GatePrefab==null){MelonLogger.Error("PORTALGUN_SHOT_REJECTED gate prefab registration incomplete");return;}
   var other=gates[1-nextColor];if(other!=null&&Vector3.Distance(hit.point,other.transform.position)<2.3f)return;
   var chunk=LocationChunkHelper.GetBestContaining(hit.point);if(chunk==null){MelonLogger.Warning("PORTALGUN_SHOT_REJECTED destination has no world chunk");return;}
   NativeGate replacement=null;
   try
   {
    var prefab=SpawnHelper.Spawn(Core.GatePrefab,SpawnData.Default,chunk,hit.point+hit.normal*.04f,Quaternion.identity);
    replacement=prefab.GetComponent<NativeGate>();if(replacement==null)throw new Exception("Spawned gate missing behavior");
    replacement.Configure(hit,nextColor);
    var previous=gates[nextColor];gates[nextColor]=replacement;
    if(other!=null){replacement.Other=other;other.Other=replacement;other.Broadcast();}
    replacement.Broadcast();if(previous!=null)previous.Entity.SceneDestroy();
    MelonLogger.Msg("PORTALGUN_PLACED color="+(nextColor==0?"blue":"orange")+" position="+hit.point);
    BroadcastEffect(hit.point);
    nextColor=1-nextColor;
   }
   catch(Exception e){if(replacement!=null)replacement.Entity.SceneDestroy();MelonLogger.Error("PORTALGUN_SHOT_FAILED "+e);}
  }
  void OnDestroy(){if(NetworkSceneManager.IsServer)foreach(var gate in gates)if(gate!=null&&gate.Entity!=null)gate.Entity.SceneDestroy();}
 }
}
