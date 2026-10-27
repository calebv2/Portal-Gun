using UnityEngine;
using System.Collections.Generic;
namespace PortalGunNative
{
 [ExecuteAlways]
 public sealed class PortalView:MonoBehaviour
 {
  static bool rendering;
  const int CaptureSize=512;
  readonly Dictionary<int,int> capturedFrames=new Dictionary<int,int>();
  Transform entry;Material material;Camera camera;
  RenderTexture left,right;Matrix4x4 exit;bool linked;
  public void Setup(Transform origin,Material mat){entry=origin;material=mat;}
  public void SetExit(bool ready,Vector3 p,Vector3 n,Vector3 u)
  {
   linked=ready&&!float.IsNaN(p.sqrMagnitude)&&!float.IsInfinity(p.sqrMagnitude)&&n.sqrMagnitude>.5f&&u.sqrMagnitude>.5f;
   if(linked)exit=Matrix4x4.TRS(p,Quaternion.LookRotation(n,u),Vector3.one);
   if(!linked&&material!=null)material.SetFloat("_HasView",0);
  }
  void OnWillRenderObject()
  {
   var source=Camera.current;
   if(rendering||source==null||entry==null||material==null)return;
   // Ignore reflection/preview cameras. Nested portal captures are guarded above.
   if(source.cameraType!=CameraType.Game&&source.cameraType!=CameraType.SceneView)return;
   if(!linked||Vector3.Dot(source.transform.position-entry.position,entry.forward)<=.0001f)
   {material.SetFloat("_HasView",0);return;}
   // Multipass XR and auxiliary callbacks can visit the same surface twice.
   // We already capture BOTH eyes in one visit, so do that only once per viewer frame.
   int sourceId=source.GetInstanceID();int capturedFrame;
   if(Application.isPlaying&&capturedFrames.TryGetValue(sourceId,out capturedFrame)&&capturedFrame==Time.frameCount)return;
   if(camera==null)
   {
    var go=new GameObject("Portal view camera");go.hideFlags=HideFlags.HideAndDontSave;
    camera=go.AddComponent<Camera>();camera.enabled=false;
    left=MakeTexture("Portal left eye");right=MakeTexture("Portal right eye");
    material.SetTexture("_ViewLeft",left);material.SetTexture("_ViewRight",right);
   }
   rendering=true;float previousCapture=Shader.GetGlobalFloat("_PortalCapture");
   var previousShadows=QualitySettings.shadows;
   try
   {
    // Discard portal pixels in the capture shader; disabling a renderer from
    // OnWillRenderObject also removes it from the outer camera's current draw.
    Shader.SetGlobalFloat("_PortalCapture",1);
    // Avoid rendering the world's shadow maps again for every portal eye.
    // Restore synchronously before the real player's camera continues.
    QualitySettings.shadows=ShadowQuality.Disable;
    camera.CopyFrom(source);camera.enabled=false;camera.stereoTargetEye=StereoTargetEyeMask.None;
    camera.allowMSAA=false;camera.allowHDR=false;camera.depthTextureMode=DepthTextureMode.None;camera.useOcclusionCulling=false;camera.rect=new Rect(0,0,1,1);
    var map=exit*Matrix4x4.Rotate(Quaternion.Euler(0,180,0))*entry.worldToLocalMatrix;
    if(source.stereoEnabled)
    {
     RenderEye(source.GetStereoViewMatrix(Camera.StereoscopicEye.Left),source.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left),map,left);
     RenderEye(source.GetStereoViewMatrix(Camera.StereoscopicEye.Right),source.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right),map,right);
    }
    else RenderEye(source.worldToCameraMatrix,source.projectionMatrix,map,left);
    material.SetFloat("_HasView",1);
    if(Application.isPlaying){if(capturedFrames.Count>16)capturedFrames.Clear();capturedFrames[sourceId]=Time.frameCount;}
   }
   finally {QualitySettings.shadows=previousShadows;Shader.SetGlobalFloat("_PortalCapture",previousCapture);rendering=false;}
  }
  static RenderTexture MakeTexture(string name)
  {var rt=new RenderTexture(CaptureSize,CaptureSize,16,RenderTextureFormat.ARGB32){name=name,antiAliasing=1};rt.Create();return rt;}
  void RenderEye(Matrix4x4 sourceView,Matrix4x4 projection,Matrix4x4 map,RenderTexture target)
  {
   var view=sourceView*map.inverse;var world=view.inverse;
   camera.transform.SetPositionAndRotation(world.MultiplyPoint(Vector3.zero),Quaternion.LookRotation(world.MultiplyVector(Vector3.back),world.MultiplyVector(Vector3.up)));
   camera.worldToCameraMatrix=view;camera.projectionMatrix=projection;
   var normal=exit.MultiplyVector(Vector3.forward);var position=exit.MultiplyPoint(Vector3.zero)+normal*.025f;
   var cp=view.MultiplyPoint(position);var cn=view.MultiplyVector(normal).normalized;
   camera.projectionMatrix=camera.CalculateObliqueMatrix(new Vector4(cn.x,cn.y,cn.z,-Vector3.Dot(cp,cn)));
   camera.targetTexture=target;camera.Render();camera.targetTexture=null;
  }
  void OnDestroy()
  {
   if(camera!=null)ReleaseObject(camera.gameObject);
   if(left!=null){left.Release();ReleaseObject(left);}if(right!=null){right.Release();ReleaseObject(right);}
  }
  static void ReleaseObject(Object value){if(Application.isPlaying)Object.Destroy(value);else Object.DestroyImmediate(value);}
 }
}
