using UnityEngine;
namespace PortalGunNative
{
 public static class PortalTransitMath
 {
  public static Vector3 HeadAnchoredPoint(Matrix4x4 map,Vector3 head,Vector3 point,Quaternion uprightDelta)
  {return map.MultiplyPoint(head)+uprightDelta*(point-head);}
  public static Quaternion FistRotation(Vector3 wrist,Vector3 middle,Vector3 index,Vector3 little)
  {return Quaternion.LookRotation(middle-wrist,index-little);}
  public static Quaternion TravelHeading(Quaternion body,Vector3 headForward,Quaternion map,Vector3 entryNormal,Vector3 exitNormal)
  {
   var current=Vector3.ProjectOnPlane(headForward,Vector3.up);
   if(current.sqrMagnitude<.01f)current=body*Vector3.forward;
   var desired=Vector3.ProjectOnPlane(map*headForward,Vector3.up);
   // Pitch/roll projections flip heading at vertical. For floor/ceiling pairs
   // preserve yaw; for a horizontal exit from a vertical entry face outward.
   if(Mathf.Abs(exitNormal.y)>.7f)desired=current;
   else if(Mathf.Abs(entryNormal.y)>.7f)desired=Vector3.ProjectOnPlane(exitNormal,Vector3.up);
   if(desired.sqrMagnitude<.01f)desired=current;
   return UprightRotation(Quaternion.AngleAxis(Vector3.SignedAngle(current,desired,Vector3.up),Vector3.up)*body,current);
  }
  public static float BoundsSupport(Vector3 extents,Vector3 axis)
  {return Mathf.Abs(axis.x)*extents.x+Mathf.Abs(axis.y)*extents.y+Mathf.Abs(axis.z)*extents.z;}
  public static bool FitsCapsule(Vector3 center,Vector3 axis,float half,float radius,float width,float height,bool rectangular=false)
  {
   if(rectangular)return Mathf.Abs(center.x)+Mathf.Abs(axis.x)*half+radius<=width&&Mathf.Abs(center.y)+Mathf.Abs(axis.y)*half+radius<=height;
   // Project the two rounded endcaps, not the corners of a box around the
   // whole player. The old box test rejected normal-height wall crossings.
   radius+=.003f;
   for(int end=-1;end<=1;end+=2)
   for(int i=0;i<32;i++)
   {
    float angle=i*Mathf.PI/16;
    float x=center.x+axis.x*half*end+Mathf.Cos(angle)*radius;
    float y=center.y+axis.y*half*end+Mathf.Sin(angle)*radius;
    if(x*x/(width*width)+y*y/(height*height)>1)return false;
   }
   return true;
  }
  public static Vector3 ItemExitCenter(Vector3 center,Vector3 extents,Transform entry,Transform exit)
  {
   var map=exit.localToWorldMatrix*Matrix4x4.Rotate(Quaternion.Euler(0,180,0))*entry.worldToLocalMatrix;
   var target=map.MultiplyPoint(center);
   float reach=BoundsSupport(extents,entry.forward)+.12f;
   return target+exit.forward*Mathf.Max(0,reach-Vector3.Dot(target-exit.position,exit.forward));
  }
  public static Quaternion UprightRotation(Quaternion rotation,Vector3 fallback)
  {
   var forward=Vector3.ProjectOnPlane(rotation*Vector3.forward,Vector3.up);
   if(forward.sqrMagnitude<.001f)forward=Vector3.ProjectOnPlane(fallback,Vector3.up);
   if(forward.sqrMagnitude<.001f)forward=Vector3.forward;
   return Quaternion.LookRotation(forward.normalized,Vector3.up);
  }
  public static Vector3 ClearUprightCenter(Vector3 mappedCenter,Vector3 planePosition,Vector3 normal,float radius,float halfSegment)
  {
   float support=radius+Mathf.Abs(normal.y)*halfSegment;
   float distance=Vector3.Dot(mappedCenter-planePosition,normal);
   return mappedCenter+normal*Mathf.Max(0,support+.06f-distance);
  }
  public static Matrix4x4 LevelingOffset(Vector3 pivot,Vector3 offset,Quaternion rotation,float remaining)
  {
   return Matrix4x4.TRS(pivot+offset*remaining,Quaternion.Slerp(Quaternion.identity,rotation,remaining),Vector3.one)*Matrix4x4.Translate(-pivot);
  }
  public static bool UnsafePassage(float signedCenter,float support,float elapsed)
  {return float.IsNaN(signedCenter)||float.IsInfinity(signedCenter)||signedCenter < -support-.5f||elapsed>8;}
  public static bool CrossesPlane(Vector3 from,Vector3 to,float width,float height,bool rectangular=false)
  {
   if(from.z<=0||to.z>0||width<=0||height<=0)return false;
   var p=Vector3.Lerp(from,to,from.z/(from.z-to.z));
   if(rectangular)return Mathf.Abs(p.x)<=width&&Mathf.Abs(p.y)<=height;
   return p.x*p.x/(width*width)+p.y*p.y/(height*height)<=1;
  }
  public static Quaternion Rotation(Transform entry,Transform exit)
  {return exit.rotation*Quaternion.Euler(0,180,0)*Quaternion.Inverse(entry.rotation);}
  // Intersect the swept leading edge with the portal, including a body already
  // touching it. Tangential misses and approaches from the back stay outside.
  public static bool CrossesAperture(Vector3 from,Vector3 to,float reach,float width,float height,bool rectangular=false)
  {
   if(width<=0||height<=0||from.z<0||to.z>reach)return false;
   Vector3 crossing=to;
   if(from.z>reach)
   {if(to.z>=from.z)return false;crossing=Vector3.Lerp(from,to,(from.z-reach)/(from.z-to.z));}
   else if(to.z<0)crossing=Vector3.Lerp(from,to,from.z/(from.z-to.z));
   return rectangular?Mathf.Abs(crossing.x)<=width&&Mathf.Abs(crossing.y)<=height:crossing.x*crossing.x/(width*width)+crossing.y*crossing.y/(height*height)<=1;
  }
 }
}
