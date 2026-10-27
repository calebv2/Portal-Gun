using UnityEngine;

public class PortalEndpoint : MonoBehaviour
{
    public PortalEndpoint Other;
    public Collider Surface;
    Vector3 anchorPosition, anchorNormal, anchorUp;
    LineRenderer ring;
    Material ringMaterial, fillMaterial;
    public void Place(RaycastHit hit, Color color)
    {
        Surface = hit.collider;
        var up = Vector3.ProjectOnPlane(Vector3.up,hit.normal);
        if (up.sqrMagnitude < .01f) up = Vector3.ProjectOnPlane(Vector3.forward,hit.normal);
        transform.SetPositionAndRotation(hit.point + hit.normal * .04f,Quaternion.LookRotation(hit.normal,up.normalized));
        anchorPosition = Surface.transform.InverseTransformPoint(transform.position);
        anchorNormal = Surface.transform.InverseTransformDirection(transform.forward);
        anchorUp = Surface.transform.InverseTransformDirection(transform.up);
        if (ring == null)
        {
            ring = gameObject.AddComponent<LineRenderer>(); ring.useWorldSpace=false; ring.loop=true; ring.positionCount=64; ring.widthMultiplier=.055f;
            ringMaterial=new Material(Shader.Find("Unlit/Color")); ring.sharedMaterial=ringMaterial;
            for(int i=0;i<64;i++) { float a=i*Mathf.PI*2/64; ring.SetPosition(i,new Vector3(Mathf.Cos(a)*.72f,Mathf.Sin(a)*1.08f,0)); }
            var fill = GameObject.CreatePrimitive(PrimitiveType.Quad); fill.name="Portal surface"; fill.transform.SetParent(transform,false);
            DestroyImmediate(fill.GetComponent<Collider>());
            // A circular mesh keeps the animated rim and center the same shape.
            var mesh=new Mesh(); var vertices=new Vector3[65]; var triangles=new int[64*3];
            for(int i=0;i<64;i++) {float a=i*Mathf.PI*2/64;vertices[i+1]=new Vector3(Mathf.Cos(a)*.7f,Mathf.Sin(a)*1.06f,.002f);triangles[i*3]=0;triangles[i*3+1]=i+1;triangles[i*3+2]=(i+1)%64+1;}
            mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();fill.GetComponent<MeshFilter>().sharedMesh=mesh;
            fillMaterial=new Material(Shader.Find("Unlit/Color"));fillMaterial.color=color*.12f;fill.GetComponent<Renderer>().sharedMaterial=fillMaterial;
        }
        ringMaterial.color=color;
        fillMaterial.color=new Color(color.r*.12f,color.g*.12f,color.b*.12f,1);
    }
    void LateUpdate()
    {
        if (Surface==null || !Surface.enabled || !Surface.gameObject.activeInHierarchy) { Destroy(gameObject); return; }
        transform.SetPositionAndRotation(Surface.transform.TransformPoint(anchorPosition),Quaternion.LookRotation(Surface.transform.TransformDirection(anchorNormal),Surface.transform.TransformDirection(anchorUp)));
        ring.widthMultiplier=.05f + Mathf.Sin(Time.time*4)*.008f;
        if(Other==null || Other.Surface==null)return;
        foreach(var traveller in FindObjectsOfType<PortalTraveller>())
        {
            var p=transform.InverseTransformPoint(traveller.Center);
            float reach=traveller.Support(transform.forward)+.12f;
            // Teleport before the solid supporting wall stops the capsule.
            if(p.z>=0 && p.z<=reach && Mathf.Abs(p.x)<.55f && Mathf.Abs(p.y)<1.0f)
                traveller.TryTransit(this,Other);
        }
    }
    void OnDestroy() { if(ringMaterial!=null)Destroy(ringMaterial);if(fillMaterial!=null)Destroy(fillMaterial); }
}
