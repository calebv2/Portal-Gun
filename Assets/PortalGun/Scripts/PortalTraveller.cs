using UnityEngine;

// Prototype controller adapter. ATT will need its own server-authoritative adapter.
[RequireComponent(typeof(CharacterController))]
public class PortalTraveller : MonoBehaviour
{
    public float NextTransitTime { get; private set; }
    public CharacterController Body => GetComponent<CharacterController>();
    public Vector3 Center => transform.TransformPoint(Body.center);
    public float Support(Vector3 normal) => Body.radius + Mathf.Abs(Vector3.Dot(normal, Vector3.up)) * Mathf.Max(0, Body.height * .5f - Body.radius);
    public bool TryTransit(PortalEndpoint entrance, PortalEndpoint exit)
    {
        if (Time.time < NextTransitTime) return false;
        var rotation = exit.transform.rotation * Quaternion.Euler(0,180,0) * Quaternion.Inverse(entrance.transform.rotation);
        Vector3 center = exit.transform.position + exit.transform.forward * (Support(exit.transform.forward) + .15f);
        float half = Mathf.Max(0, Body.height * .5f - Body.radius);
        // Include the destination surface: offset must actually clear it, including curved surfaces.
        foreach (var c in Physics.OverlapCapsule(center + Vector3.up * half, center - Vector3.up * half, Body.radius, ~0, QueryTriggerInteraction.Ignore))
            if (!c.transform.IsChildOf(transform)) return false;
        var forward = Vector3.ProjectOnPlane(rotation * transform.forward, Vector3.up);
        bool enabled = Body.enabled; Body.enabled = false;
        transform.position = center - Vector3.up * Body.center.y;
        if (forward.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(forward,Vector3.up);
        Body.enabled = enabled;
        NextTransitTime = Time.time + .65f;
        var player = GetComponent<PortalDemoPlayer>(); if (player != null) player.ResetFall();
        return true;
    }
}
