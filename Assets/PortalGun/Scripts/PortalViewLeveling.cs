using System.Collections.Generic;
using UnityEngine;
namespace PortalGunNative
{
 // Render-only correction. Never rotate the tracked player or its collider.
 // Both eyes use the same rigid correction, preserving their separation.
 [ExecuteAlways]
 public sealed class PortalViewLeveling:MonoBehaviour
 {
  sealed class Saved
  {
   public Matrix4x4 View,Left,Right,Culling;public float Near;public bool Stereo,CustomView;
  }
  static PortalViewLeveling instance;
  readonly Dictionary<Camera,Saved> saved=new Dictionary<Camera,Saved>();
  Transform head;System.Func<Vector3> bodyCenter;Quaternion correction;Vector3 offset;float started;
  const float Duration=.4f;
  public static void Begin(Transform trackedHead,System.Func<Vector3> center,Quaternion rotation,Vector3 targetHead)
  {
   if(instance==null){var go=new GameObject("Portal view leveling");if(Application.isPlaying)Object.DontDestroyOnLoad(go);else go.hideFlags=HideFlags.DontSave;instance=go.AddComponent<PortalViewLeveling>();}
   instance.head=trackedHead;instance.bodyCenter=center;instance.correction=rotation;instance.started=Time.time;
   var pivot=center();instance.offset=targetHead-(pivot+rotation*(trackedHead.position-pivot));
  }
  public static void Cancel(){if(instance!=null){instance.head=null;instance.bodyCenter=null;instance.RestoreAll();}}
  void OnEnable(){Camera.onPreCull+=Before;Camera.onPostRender+=After;}
  void Update(){if(head!=null&&Time.time-started>=Duration){head=null;bodyCenter=null;RestoreAll();}}
  void Before(Camera camera)
  {
   if(head==null||bodyCenter==null||Time.time-started>=Duration||camera.gameObject.hideFlags==HideFlags.HideAndDontSave)return;
   if(Vector3.Distance(camera.transform.position,head.position)>.5f||saved.ContainsKey(camera))return;
   var state=new Saved{View=camera.worldToCameraMatrix,Culling=camera.cullingMatrix,Near=camera.nearClipPlane,Stereo=camera.stereoEnabled};
   var automatic=Matrix4x4.Scale(new Vector3(1,1,-1))*camera.transform.worldToLocalMatrix;
   state.CustomView=!Close(state.View,automatic);
   if(state.Stereo){state.Left=camera.GetStereoViewMatrix(Camera.StereoscopicEye.Left);state.Right=camera.GetStereoViewMatrix(Camera.StereoscopicEye.Right);}
   saved.Add(camera,state);
   float remaining=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((Time.time-started)/Duration));
   var pivot=bodyCenter();
   var delta=PortalTransitMath.LevelingOffset(pivot,offset,correction,remaining);
   camera.nearClipPlane=Mathf.Min(state.Near,.005f);
   camera.worldToCameraMatrix=state.View*delta.inverse;
   if(state.Stereo)
   {
    camera.SetStereoViewMatrix(Camera.StereoscopicEye.Left,state.Left*delta.inverse);
    camera.SetStereoViewMatrix(Camera.StereoscopicEye.Right,state.Right*delta.inverse);
   }
   camera.cullingMatrix=state.Culling*delta.inverse;
  }
  void After(Camera camera)
  {
   Saved state;if(!saved.TryGetValue(camera,out state))return;
   camera.nearClipPlane=state.Near;
   if(state.CustomView)camera.worldToCameraMatrix=state.View;else camera.ResetWorldToCameraMatrix();
   if(state.Stereo)camera.ResetStereoViewMatrices();
   camera.ResetCullingMatrix();saved.Remove(camera);
  }
  static bool Close(Matrix4x4 a,Matrix4x4 b){for(int i=0;i<16;i++)if(Mathf.Abs(a[i]-b[i])>.0001f)return false;return true;}
  void RestoreAll(){var cameras=new List<Camera>(saved.Keys);foreach(var c in cameras)if(c!=null)After(c);saved.Clear();}
  void OnDisable(){Camera.onPreCull-=Before;Camera.onPostRender-=After;RestoreAll();}
 }
}
