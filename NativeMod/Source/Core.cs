using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Alta;
using Alta.Inventory;
using Alta.Networking;
using MelonLoader;
using SyncLib.Items;
using UnityEngine;
using Object=UnityEngine.Object;
[assembly:MelonInfo(typeof(PortalGunNative.Core),"PortalGun","0.2.5","caleb")]
[assembly:MelonGame("Alta","A Township Tale")]
[assembly:MelonAdditionalDependencies("SyncLib")]
namespace PortalGunNative
{
 public sealed class Core:MelonMod
 {
  internal static NetworkPrefab GatePrefab;
  internal static Shader PortalShader;
  internal static Shader ShotShader;
  internal static Shader SliceShader;
  GameObject model;GameObject[] doorModels;bool attempted;
#if SERVER
  const bool Server=true;
#else
  const bool Server=false;
#endif
  public override void OnInitializeMelon(){NetworkPrefabRegistry.RegisterPrefabs+=RegisterPrefabs;LoggerInstance.Msg("PORTALGUN_START version=0.2.5 role="+(Server?"server":"client"));}
  public override void OnLateInitializeMelon(){MelonCoroutines.Start(Load());}
  IEnumerator Load()
  {
   byte[] bytes;using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("PortalGun.Models"))using(var m=new MemoryStream()){s.CopyTo(m);bytes=m.ToArray();}
   var request=AssetBundle.LoadFromMemoryAsync(bytes);yield return request;
   var bundle=request.assetBundle;if(bundle==null){LoggerInstance.Error("PORTALGUN_ASSET_FAILED");yield break;}
   var asset=bundle.LoadAssetAsync<GameObject>("PortalGun");yield return asset;model=asset.asset as GameObject;
   doorModels=new[]{bundle.LoadAsset<GameObject>("PortalDoorA"),bundle.LoadAsset<GameObject>("PortalDoorB")};
   PortalShader=bundle.LoadAsset<Shader>("PortalSurface");ShotShader=bundle.LoadAsset<Shader>("PortalShot");SliceShader=bundle.LoadAsset<Shader>("PortalSlice");bundle.Unload(false);
   LoggerInstance.Msg("PORTALGUN_ASSETS_READY model="+(model!=null)+" shader="+(PortalShader!=null));
  }
  internal static FieldInfo Field(Type type,string name,bool stat=false)
  {for(;type!=null;type=type.BaseType){var f=type.GetField(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly|(stat?BindingFlags.Static:BindingFlags.Instance));if(f!=null)return f;}throw new MissingFieldException(name);}
  internal static void Set(object obj,string name,object value){Field(obj.GetType(),name).SetValue(obj,value);}
  void RegisterPrefabs()
  {
   if(attempted)return;attempted=true;
   try
   {
    if(NetworkSceneManager.IsServer!=Server)throw new Exception("Wrong role DLL");
    if(model==null||PortalShader==null)throw new Exception("Model assets not ready at registration");
    var donor=PrefabManager.AllPrefabs.First(p=>p!=null&&p.Hash==19180);
    var staging=new GameObject("PortalGun templates");staging.SetActive(false);Object.DontDestroyOnLoad(staging);
    var root=Object.Instantiate(donor.gameObject,staging.transform,false);root.name="PortalGun";root.SetActive(true);
    var original=root.GetComponentsInChildren<Renderer>(true);foreach(var r in original)r.forceRenderingOff=true;
    var keeper=root.AddComponent<HiddenDonor>();keeper.Renderers=original;
    var visual=Object.Instantiate(model.transform.Find("Visual").gameObject,root.transform,false);visual.name="PortalGunVisual";
    visual.transform.localRotation=Quaternion.Euler(0,180,0);
    // Rotate the centering offset with the mesh; otherwise its body shifts 29.5 cm
    // away from its grip/collider when reversing the OBJ's muzzle direction.
    visual.transform.localPosition=Quaternion.Euler(0,180,0)*model.transform.Find("Visual").localPosition;
    var collider=root.AddComponent<BoxCollider>();collider.size=model.GetComponent<BoxCollider>().size;collider.center=Quaternion.Euler(0,180,0)*model.GetComponent<BoxCollider>().center;
    root.AddComponent<NativeGun>();
    var oldBody=root.GetComponent<NetworkRigidbody>();
    var prefab=this.RegisterCustomPrefab(root,"PortalGun");if(prefab==null)throw new Exception("SyncLib rejected PortalGun (server registration must succeed first)");
    foreach(var b in root.GetComponents<NetworkRigidbody>())if(oldBody!=null&&b!=oldBody)Object.DestroyImmediate(b);
    var pickup=root.GetComponent<Pickup>();var item=Object.Instantiate(pickup.Item);item.name="PortalGun";item.Prefab=prefab;
    // Pickup.Awake populates its collision set from this serialized array.
    // The donor's array cannot include the box added above. Native hand
    // collision suppression must see it before any spawned instance awakens.
    Set(pickup,"childrenColliders",root.GetComponentsInChildren<Collider>(true));
    var grip=new GrabPoint();
    Set(grip,"position",new Vector3(0,-.035f,-.22f));
    // PortalGrip resolves the anatomical pointing frame on each grab.
    Set(grip,"rotationEuler",Vector3.zero);
    // A fixed rear grip replaces the donor's 48 cm sliding pole grip.
    Set(pickup,"grabPoints",new[]{grip});Set(pickup,"grabPointsScale",1f);
    Set(item,"components",new List<ItemComponent>(item.Components));Set(item,"isStackable",false);Set(item,"isStackableWhenCrafted",false);
    Set(item,"isSnappingToHand",true);
    HashedGeneralValue<Item>.CheckItems();var items=(Dictionary<uint,Item>)Field(typeof(HashedGeneralValue<Item>),"items",true).GetValue(null);
    if(items.ContainsKey(prefab.Hash))throw new Exception("PortalGun item hash collision "+prefab.Hash);
    Set(item,"hash",(int)prefab.Hash);items.Add(prefab.Hash,item);Field(typeof(HashedGeneralValue<Item>),"itemsByName",true).SetValue(null,null);pickup.Item=item;
    BindNetworkBehaviours(root);
    var gate=new GameObject("PortalGunGate");gate.transform.SetParent(staging.transform,false);gate.AddComponent<NetworkEntity>();gate.AddComponent<NetworkPrefab>();gate.AddComponent<NativeGate>();
    GatePrefab=this.RegisterCustomPrefab(gate,"PortalGunGate");if(GatePrefab==null)throw new Exception("SyncLib rejected PortalGunGate");
    BindNetworkBehaviours(gate);
    if(doorModels==null||doorModels.Any(d=>d==null))throw new Exception("Portal doorway model missing from bundle");
    for(int index=0;index<2;index++)
    {
     var door=new GameObject(index==0?"PortalDoorA":"PortalDoorB");door.transform.SetParent(staging.transform,false);
     var geometry=Object.Instantiate(doorModels[index],door.transform,false);geometry.name="DoorVisual";
     var body=door.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;
     door.AddComponent<NetworkEntity>();door.AddComponent<NetworkPrefab>();door.AddComponent<NativeGate>();
     door.AddComponent<NativeDoor>().EndIndex=index;
     var registered=this.RegisterCustomPrefab(door,door.name);if(registered==null)throw new Exception("SyncLib rejected "+door.name);
     NativeDoor.ConfigureCollision(door);
     BindNetworkBehaviours(door);
     LoggerInstance.Msg("PORTALDOOR_REGISTERED name="+door.name+" hash="+registered.Hash);
    }
    LoggerInstance.Msg("PORTALGUN_REGISTERED gun="+prefab.Hash+" gate="+GatePrefab.Hash);
   }catch(Exception e){LoggerInstance.Error("PORTALGUN_REGISTRATION_FAILED "+e);}
  }
  static void BindNetworkBehaviours(GameObject root)
  {
   // Native donor components already have serialized references. Re-running
   // their editor-only CustomMethodApply callbacks crashes in the player build.
   var entity=root.GetComponent<NetworkEntity>();
   var behaviours=root.GetComponents<MonoBehaviour>().Where(b=>b is INetworkEntityBehaviour).ToArray();
   foreach(var b in behaviours.OfType<NetworkEntityBehaviour>())Set(b,"entity",entity);
   Set(entity,"networkEntityBehaviours",behaviours);
   Set(entity,"hasNetworkBehaviours",behaviours.Length>0);
   if(!behaviours.Any(b=>b is NativeGun||b is NativeGate))throw new Exception("Portal behavior missing from network initialization list");
  }
 }
 public sealed class HiddenDonor:MonoBehaviour
 {public Renderer[] Renderers;void LateUpdate(){if(Renderers!=null)foreach(var r in Renderers)if(r!=null)r.forceRenderingOff=true;}}
}
