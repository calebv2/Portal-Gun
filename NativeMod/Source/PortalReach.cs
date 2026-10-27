using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
namespace PortalGunNative
{
 // Render-only slicing of the existing animated avatar. No hand controller,
 // grab ownership, collider or IK target is moved to the other location.
 public sealed class PortalReach:MonoBehaviour
 {
  sealed class Slice
  {
   public SkinnedMeshRenderer Original;public Mesh Mesh;public GameObject Near,Far;
   public Material[] NearMaterials,FarMaterials;public bool WasHidden;
  }
  readonly Dictionary<SkinnedMeshRenderer,Slice> slices=new Dictionary<SkinnedMeshRenderer,Slice>();
  bool linked;Matrix4x4 map;Quaternion exitRotation;Vector3 exitPosition;
  float width=.72f,height=1.08f;bool rectangular;
  public void SetAperture(float w,float h,bool rectangle){width=w;height=h;rectangular=rectangle;}
  public void SetExit(bool ready,Vector3 p,Vector3 n,Vector3 u)
  {linked=ready&&n.sqrMagnitude>.5f&&u.sqrMagnitude>.5f;exitPosition=p;if(linked)exitRotation=Quaternion.LookRotation(n,u);}
  void LateUpdate()
  {
   var wanted=new HashSet<SkinnedMeshRenderer>();
   if(linked&&Core.SliceShader!=null)
   {
    map=Matrix4x4.TRS(exitPosition,exitRotation,Vector3.one)*Matrix4x4.Rotate(Quaternion.Euler(0,180,0))*transform.worldToLocalMatrix;
    foreach(var player in Player.AllPlayers.OfType<Player>())
    {
     var pc=player.PlayerController;if(pc==null||pc.Head==null)continue;
     var head=transform.InverseTransformPoint(pc.Head.position);
     if(head.z<0||head.z>1.5f||Mathf.Abs(head.x)>1.5f||Mathf.Abs(head.y)>2)continue;
     if(!Reaching(pc.LeftController)&&!Reaching(pc.RightController))continue;
     foreach(var skin in pc.GetComponentsInChildren<SkinnedMeshRenderer>())
     {
      if(skin.sharedMesh==null||!skin.enabled||(!slices.ContainsKey(skin)&&skin.forceRenderingOff))continue;
      wanted.Add(skin);Slice slice;
      if(!slices.TryGetValue(skin,out slice)){slice=Create(skin);slices.Add(skin,slice);}
      skin.BakeMesh(slice.Mesh);skin.forceRenderingOff=true;
      slice.Far.transform.SetPositionAndRotation(map.MultiplyPoint(skin.transform.position),exitRotation*Quaternion.Euler(0,180,0)*Quaternion.Inverse(transform.rotation)*skin.transform.rotation);
      slice.Far.transform.localScale=skin.transform.lossyScale;
      foreach(var m in slice.NearMaterials)m.SetMatrix("_SliceMatrix",transform.worldToLocalMatrix);
      foreach(var m in slice.FarMaterials)m.SetMatrix("_SliceMatrix",transform.worldToLocalMatrix*map.inverse);
     }
    }
   }
   foreach(var key in slices.Keys.Where(k=>k==null||!wanted.Contains(k)).ToArray()){Release(slices[key]);slices.Remove(key);}
  }
  bool Reaching(Controller controller)
  {
   if(controller==null||controller.GripPoint==null)return false;
   var p=transform.InverseTransformPoint(controller.GripPoint.position);
   return p.z<.08f&&p.z>-.85f&&(rectangular?(Mathf.Abs(p.x)<width&&Mathf.Abs(p.y)<height):p.x*p.x/(width*width)+p.y*p.y/(height*height)<1);
  }
  Slice Create(SkinnedMeshRenderer skin)
  {
   var s=new Slice{Original=skin,Mesh=new Mesh(),WasHidden=skin.forceRenderingOff};
   s.NearMaterials=Materials(skin.sharedMaterials,1);s.FarMaterials=Materials(skin.sharedMaterials,-1);
   s.Near=Proxy("Portal arm entry",s.Mesh,s.NearMaterials,skin.gameObject.layer);s.Near.transform.SetParent(skin.transform,false);
   s.Far=Proxy("Portal arm exit",s.Mesh,s.FarMaterials,skin.gameObject.layer);
   return s;
  }
  static GameObject Proxy(string name,Mesh mesh,Material[] mats,int layer)
  {
   var go=new GameObject(name);go.layer=layer;go.AddComponent<MeshFilter>().sharedMesh=mesh;
   var r=go.AddComponent<MeshRenderer>();r.sharedMaterials=mats;r.shadowCastingMode=ShadowCastingMode.Off;return go;
  }
  static Material[] Materials(Material[] original,float side)
  {
   return original.Select(m=>{var copy=new Material(Core.SliceShader){enableInstancing=true};
    if(m!=null){if(m.HasProperty("_MainTex")){copy.mainTexture=m.mainTexture;copy.mainTextureScale=m.mainTextureScale;copy.mainTextureOffset=m.mainTextureOffset;}if(m.HasProperty("_Color"))copy.color=m.color;}
    copy.SetFloat("_SliceSide",side);return copy;}).ToArray();
  }
  static void Release(Slice s)
  {if(s.Original!=null)s.Original.forceRenderingOff=s.WasHidden;Object.Destroy(s.Near);Object.Destroy(s.Far);Object.Destroy(s.Mesh);foreach(var m in s.NearMaterials)Object.Destroy(m);foreach(var m in s.FarMaterials)Object.Destroy(m);}
  void OnDisable(){foreach(var s in slices.Values)Release(s);slices.Clear();}
 }
}
