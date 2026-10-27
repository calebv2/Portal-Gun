using System.Collections.Generic;
using System.Linq;
using Alta.Character;
using Alta.Networking;
using Alta.Networking.Scripts.Player;
using Alta.Serialization;
using MelonLoader;
using UnityEngine;
namespace PortalGunNative
{
 public sealed class NativeGate:NetworkEntityBehaviour
 {
  public NativeGate Other;
  Collider surface;Vector3 anchor,normal,up;int color;bool placed,linked;
  MethodSync state,itemTransit;float nextTick,born,nextItemLog;GameObject display;Material material;
  NetworkEntity transitEntity;Vector3 transitPosition,transitVelocity,transitAngular;Quaternion transitRotation;
  PortalView view;
  PortalReach reachVisual;
  PortalPassage passage;

  static readonly Dictionary<Pickup,float> itemCooldown=new Dictionary<Pickup,float>();
  bool IsDoor=>GetComponent<NativeDoor>()!=null;
  float HalfWidth=>IsDoor ? .8f : .72f;
  float HalfHeight=>IsDoor ? 1.2f : 1.08f;
  public override void Initialize(){base.Initialize();born=Time.time;state=new MethodSync(Entity,EntityMessageType.SyncDebugCommandFull,Serialize);itemTransit=new MethodSync(Entity,EntityMessageType.SyncDebugCommand,SerializeItemTransit);}
  void SerializeItemTransit(IPlayer sender,Stream stream)
  {
   var entity=transitEntity;var p=transitPosition;var q=transitRotation;var v=transitVelocity;var w=transitAngular;
   stream.SerializeNetworkEntity(ref entity,Entity.Scene);
   stream.SerializeVector3(ref p);stream.SerializeQuaternionCompressed(ref q);stream.SerializeVector3(ref v);stream.SerializeVector3(ref w);
   if(!stream.IsReading||NetworkSceneManager.IsServer||entity==null)return;
   if(float.IsNaN(p.sqrMagnitude)||float.IsInfinity(p.sqrMagnitude)||float.IsNaN(v.sqrMagnitude)||float.IsInfinity(v.sqrMagnitude))return;
   var pickup=entity.GetComponent<Pickup>();var body=entity.GetComponent<Rigidbody>();
   if(pickup==null||body==null||pickup.IsDocked||pickup.Interactors.Count!=0)return;
   body.position=p;body.rotation=q;entity.transform.SetPositionAndRotation(p,q);
   if(!body.isKinematic){body.velocity=v;body.angularVelocity=w;body.WakeUp();}
   var network=entity.GetComponent<NetworkRigidbody>();
   // Throw prediction normally suppresses server updates for up to a second.
   // Snap to the authoritative exit now and allow fresh snapshots to catch up.
   if(network!=null){network.SetActive(true);network.LerpAuthorityFromClient(-.25f,1f);}
   MelonLogger.Msg("PORTALGUN_ITEM_TRANSIT_CLIENT entity="+entity.Identifier);
  }
  public void Configure(RaycastHit hit,int index)
  {
   color=index;surface=hit.collider;
   var tangent=Vector3.ProjectOnPlane(Vector3.up,hit.normal);if(tangent.sqrMagnitude<.01f)tangent=Vector3.ProjectOnPlane(Vector3.forward,hit.normal);
   transform.SetPositionAndRotation(hit.point+hit.normal*.04f,Quaternion.LookRotation(hit.normal,tangent.normalized));
   anchor=surface.transform.InverseTransformPoint(transform.position);normal=surface.transform.InverseTransformDirection(transform.forward);up=surface.transform.InverseTransformDirection(transform.up);placed=true;
  }
  public void ConfigureDoor(Collider backing,int index)
  {
   color=index;surface=backing;placed=true;
   anchor=surface.transform.InverseTransformPoint(transform.position);
   normal=surface.transform.InverseTransformDirection(transform.forward);
   up=surface.transform.InverseTransformDirection(transform.up);
  }
  public void Broadcast(){if(state!=null)state.SendToChunks();}
  public override void OnConnected(Player player){base.OnConnected(player);if(NetworkSceneManager.IsServer&&state!=null)state.SendToPlayer(player);}
  void Serialize(IPlayer sender,Stream stream)
  {
   var p=transform.position;var n=transform.forward;var u=transform.up;var flags=new Vector3(placed?1:0,color,Other!=null?1:0);
   stream.SerializeVector3(ref p);stream.SerializeVector3(ref n);stream.SerializeVector3(ref u);stream.SerializeVector3(ref flags);
   var exit=Other!=null?Other.transform:transform;
   var ep=exit.position;var en=exit.forward;var eu=exit.up;
   stream.SerializeVector3(ref ep);stream.SerializeVector3(ref en);stream.SerializeVector3(ref eu);
   if(stream.IsReading&&!NetworkSceneManager.IsServer)
   {
    if(float.IsNaN(p.sqrMagnitude)||float.IsInfinity(p.sqrMagnitude)||n.sqrMagnitude<.5f||u.sqrMagnitude<.5f)return;
    placed=flags.x==1;color=flags.y==1?1:0;linked=flags.z==1;transform.SetPositionAndRotation(p,Quaternion.LookRotation(n,u));Show();
    if(passage!=null)passage.SetExit(linked,ep,en,eu);
    if(view!=null)view.SetExit(linked,ep,en,eu);
    if(reachVisual!=null)reachVisual.SetExit(linked,ep,en,eu);
   }
  }
  void Show()
  {
   if(!placed)return;
   if(display==null)
   {
    display=GameObject.CreatePrimitive(PrimitiveType.Quad);display.name="Portal surface";display.transform.SetParent(transform,false);Object.Destroy(display.GetComponent<Collider>());
    display.transform.localScale=new Vector3(HalfWidth*2,HalfHeight*2,1);
    material=new Material(Core.PortalShader);material.enableInstancing=true;display.GetComponent<Renderer>().sharedMaterial=material;
    view=display.AddComponent<PortalView>();view.Setup(transform,material);
    reachVisual=gameObject.AddComponent<PortalReach>();
    reachVisual.SetAperture(HalfWidth,HalfHeight,IsDoor);
    passage=gameObject.AddComponent<PortalPassage>();
    passage.SetAperture(HalfWidth,HalfHeight,IsDoor);
    material.SetFloat("_Rectangular",IsDoor?1:0);
   }
   material.color=(color==0?new Color(.05f,.55f,1):new Color(1,.38f,.03f))*(linked?1:.4f);
  }
  void FixedUpdate()
  {
   if(!NetworkSceneManager.IsServer||Entity==null||!Entity.IsInitialized)return;
   if(!placed){if(Time.time-born>10)Entity.SceneDestroy();return;} // Session portals do not restore without their pair.
   if(surface==null||!surface.enabled||!surface.gameObject.activeInHierarchy){Entity.SceneDestroy();return;}
   if(!IsDoor)transform.SetPositionAndRotation(surface.transform.TransformPoint(anchor),Quaternion.LookRotation(surface.transform.TransformDirection(normal),surface.transform.TransformDirection(up)));
   if(Time.time>=nextTick){nextTick=Time.time+.1f;Broadcast();}
   if(Other==null||!Other.placed||Other.surface==null)return;
   TransferItems();
  }
  void TransferItems()
  {
   foreach(var stale in itemCooldown.Keys.Where(p=>p==null||itemCooldown[p]<Time.time).ToArray())itemCooldown.Remove(stale);
   var seen=new HashSet<Pickup>();
   var colliders=Physics.OverlapBox(transform.position+transform.forward,new Vector3(HalfWidth+.18f,HalfHeight+.22f,1.1f),transform.rotation,~0,QueryTriggerInteraction.Ignore);
   foreach(var hit in colliders)
   {
    var pickup=hit.GetComponentInParent<Pickup>();
    if(pickup==null||!seen.Add(pickup)||itemCooldown.ContainsKey(pickup)||pickup.IsDocked||pickup.IsEnvironmental||pickup.Interactors.Count!=0)continue;
    var body=pickup.GetComponent<Rigidbody>();var entity=pickup.GetComponent<NetworkEntity>();
    if(body==null||entity==null||!entity.IsInitialized||body==surface.attachedRigidbody||body==Other.surface.attachedRigidbody)continue;
    if(body.isKinematic){ItemBlocked(entity,"kinematic body");continue;}
    // A loose native pickup can retain joint components. Only actual physical
    // connections to a different body/world prevent independent travel.
    if(pickup.GetComponents<Joint>().Any(j=>HasExternalConstraint(j,pickup.transform)))
    {ItemBlocked(entity,"connected joint");continue;}
    if(Vector3.Dot(body.velocity,transform.forward)>=-.05f)continue;
    var shapes=pickup.Colliders.Where(c=>c!=null&&c.enabled&&c.gameObject.activeInHierarchy&&!c.isTrigger&&c.attachedRigidbody==body).ToArray();
    if(shapes.Length==0){ItemBlocked(entity,"no solid body colliders");continue;}
    var bounds=shapes[0].bounds;foreach(var c in shapes.Skip(1))bounds.Encapsulate(c.bounds);
    float sx=PortalTransitMath.BoundsSupport(bounds.extents,transform.right);
    float sy=PortalTransitMath.BoundsSupport(bounds.extents,transform.up);
    float sz=PortalTransitMath.BoundsSupport(bounds.extents,transform.forward);
    var local=transform.InverseTransformPoint(bounds.center);
    var next=transform.InverseTransformPoint(bounds.center+body.velocity*Time.fixedDeltaTime);
    if(!PortalTransitMath.CrossesAperture(local,next,sz+.12f,HalfWidth-sx,HalfHeight-sy,IsDoor))
    {if(local.z<sz+.3f)ItemBlocked(entity,"aperture fit/approach extents="+new Vector3(sx,sy,sz));continue;}
    var rotation=PortalTransitMath.Rotation(transform,Other.transform);
    var target=PortalTransitMath.ItemExitCenter(bounds.center,bounds.extents,transform,Other.transform);
    // Conservative clearance: never insert a pickup inside exit geometry.
    var blocker=Physics.OverlapBox(target,bounds.extents,rotation,~0,QueryTriggerInteraction.Ignore).FirstOrDefault(c=>!c.transform.IsChildOf(pickup.transform));
    if(blocker!=null){ItemBlocked(entity,"exit obstructed by "+blocker.name);continue;}
    var velocity=rotation*body.velocity;var angular=rotation*body.angularVelocity;
    var position=target-rotation*(bounds.center-body.position);
    itemCooldown[pickup]=Time.time+.6f;
    body.position=position;body.rotation=rotation*body.rotation;
    pickup.transform.SetPositionAndRotation(body.position,body.rotation);
    body.velocity=velocity;body.angularVelocity=angular;body.WakeUp();entity.EvaluateChunk();
    transitEntity=entity;transitPosition=body.position;transitRotation=body.rotation;transitVelocity=velocity;transitAngular=angular;itemTransit.SendToChunks();
    MelonLogger.Msg("PORTALGUN_ITEM_TRANSIT entity="+entity.Identifier+" speed="+velocity.magnitude);
   }
  }
  void ItemBlocked(NetworkEntity entity,string reason)
  {if(Time.time>=nextItemLog){nextItemLog=Time.time+2;MelonLogger.Msg("PORTALGUN_ITEM_BLOCKED entity="+entity.Identifier+" reason="+reason);}}
  static bool HasExternalConstraint(Joint joint,Transform root)
  {
   if(joint.connectedBody!=null&&joint.connectedBody.transform.IsChildOf(root))return false;
   var configurable=joint as ConfigurableJoint;
   if(configurable!=null&&configurable.xMotion==ConfigurableJointMotion.Free&&configurable.yMotion==ConfigurableJointMotion.Free&&configurable.zMotion==ConfigurableJointMotion.Free&&configurable.angularXMotion==ConfigurableJointMotion.Free&&configurable.angularYMotion==ConfigurableJointMotion.Free&&configurable.angularZMotion==ConfigurableJointMotion.Free)return false;
   return true;
  }
  static bool HeldBy(Collider collider,Player player)
  {var pickup=collider.GetComponentInParent<Pickup>();return pickup!=null&&pickup.Interactors.Any(i=>i!=null&&i.Controller!=null&&i.Controller.PlayerController!=null&&i.Controller.PlayerController.NetworkPlayer==player);}
  void OnDestroy(){if(material!=null)Object.Destroy(material);}
 }
}
