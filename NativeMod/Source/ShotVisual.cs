using UnityEngine;
namespace PortalGunNative
{
 public sealed class ShotVisual:MonoBehaviour
 {
  Vector3 start,end;float born;LineRenderer trail;Transform pulse;Material material;Color color;
  public static void Play(Vector3 a,Vector3 b,bool blue)
  {
   if(Core.ShotShader==null)return;
   var fx=new GameObject("Portal shot").AddComponent<ShotVisual>();fx.start=a;fx.end=b;fx.born=Time.time;
   fx.color=blue?new Color(.08f,.6f,1,1):new Color(1,.4f,.06f,1);
   fx.material=new Material(Core.ShotShader){enableInstancing=true};fx.material.color=fx.color;
   fx.trail=fx.gameObject.AddComponent<LineRenderer>();fx.trail.sharedMaterial=fx.material;
   fx.trail.useWorldSpace=true;fx.trail.positionCount=2;fx.trail.startWidth=.035f;fx.trail.endWidth=.015f;
   fx.trail.SetPosition(0,a);fx.trail.SetPosition(1,a);
   var ball=GameObject.CreatePrimitive(PrimitiveType.Sphere);ball.name="Portal energy pulse";
   ball.GetComponent<Collider>().enabled=false;Object.Destroy(ball.GetComponent<Collider>());
   ball.transform.SetParent(fx.transform,false);ball.GetComponent<Renderer>().sharedMaterial=fx.material;fx.pulse=ball.transform;
   fx.pulse.position=a;fx.pulse.localScale=Vector3.one*.09f;Object.Destroy(fx.gameObject,.35f);
  }
  void Update()
  {
   float t=(Time.time-born)/.18f;float head=Mathf.Clamp01(t);float tail=Mathf.Clamp01(t-.35f);
   trail.SetPosition(0,Vector3.Lerp(start,end,tail));trail.SetPosition(1,Vector3.Lerp(start,end,head));
   pulse.position=Vector3.Lerp(start,end,head);pulse.localScale=Vector3.one*(t<1?.09f:.09f+(t-1)*.25f);
   var c=color;c.a=1-Mathf.Clamp01((t-1)/.8f);material.color=c;
  }
  void OnDestroy(){if(material!=null)Object.Destroy(material);}
 }
}
