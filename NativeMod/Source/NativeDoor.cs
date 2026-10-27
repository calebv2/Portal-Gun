using System.Collections.Generic;
using System.Linq;
using Alta.Networking;
using MelonLoader;
using UnityEngine;
namespace PortalGunNative
{
 // A/B are fixed prefab identities. Restored world instances rejoin the pair
 // during native initialization; no gun or session-spawned gate is required.
 public sealed class NativeDoor:NetworkEntityBehaviour
 {
  public int EndIndex;
  static readonly List<NativeDoor> doors=new List<NativeDoor>();
  NativeGate gate;float nextPairCheck;
  public override void Initialize()
  {
   base.Initialize();ConfigureCollision(gameObject);gate=GetComponent<NativeGate>();
   foreach(var r in GetComponentsInChildren<Renderer>())if(r.name.StartsWith("Rune"))
   {var block=new MaterialPropertyBlock();block.SetColor("_Color",EndIndex==0?new Color(.08f,.55f,.9f):new Color(1,.38f,.08f));r.SetPropertyBlock(block);}
   if(!NetworkSceneManager.IsServer)return;
   var backing=transform.Find("DoorVisual/PortalDoorBacking").GetComponent<Collider>();
   gate.ConfigureDoor(backing,EndIndex);doors.Add(this);Pair();
   MelonLogger.Msg("PORTALDOOR_READY endpoint="+(EndIndex==0?"A":"B")+" entity="+Entity.Identifier);
  }
  internal static void ConfigureCollision(GameObject root)
  {
   int ground=LayerMask.NameToLayer("Ground");
   if(ground<0)throw new System.InvalidOperationException("ATT Ground collision layer missing");
   int count=0;
   foreach(var collider in root.GetComponentsInChildren<Collider>(true))
   {
    if(collider.isTrigger)continue;
    collider.gameObject.layer=ground;collider.enabled=true;count++;
   }
   if(count<3)throw new System.InvalidOperationException("Door frame colliders missing");
  }
  static void Pair()
  {
   doors.RemoveAll(d=>d==null||d.Entity==null||d.Entity.IsBeingDestroyed);
   foreach(var a in doors.Where(d=>d.EndIndex==0&&d.gate!=null&&d.gate.Other==null).ToArray())
   {
    if(doors.Any(d=>d.EndIndex==0&&d.gate!=null&&d.gate.Other!=null&&ReferenceEquals(d.Entity.Scene,a.Entity.Scene)))continue;
    if(Vector3.Dot(a.transform.up,Vector3.up)<.98f)continue;
    var b=doors.FirstOrDefault(d=>d.EndIndex==1&&d.gate!=null&&d.gate.Other==null&&ReferenceEquals(d.Entity.Scene,a.Entity.Scene)&&Vector3.Dot(d.transform.up,Vector3.up)>=.98f&&(d.transform.position-a.transform.position).sqrMagnitude>=6.25f);
    if(b==null)continue;
    a.gate.Other=b.gate;b.gate.Other=a.gate;a.gate.Broadcast();b.gate.Broadcast();
    MelonLogger.Msg("PORTALDOOR_LINKED A="+a.Entity.Identifier+" B="+b.Entity.Identifier);
   }
  }
  void Update()
  {
   if(!NetworkSceneManager.IsServer||gate==null||Time.time<nextPairCheck)return;nextPairCheck=Time.time+.5f;
   if(gate.Other!=null&&(Vector3.Dot(transform.up,Vector3.up)<.98f||(gate.Other.transform.position-transform.position).sqrMagnitude<6.25f))
   {var other=gate.Other;gate.Other=null;if(other.Other==gate)other.Other=null;gate.Broadcast();other.Broadcast();}
   if(gate.Other==null)Pair();
  }
  void OnDestroy()
  {
   doors.Remove(this);
   if(!NetworkSceneManager.IsServer||gate==null)return;
   var other=gate.Other;gate.Other=null;
   if(other!=null&&other.Other==gate)
   {other.Other=null;if(other.Entity!=null&&!other.Entity.IsBeingDestroyed)other.Broadcast();}
   Pair();
  }
 }
}
