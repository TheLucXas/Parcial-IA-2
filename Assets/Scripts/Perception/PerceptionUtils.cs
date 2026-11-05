using UnityEngine;

public static class PerceptionUtils
{
    #region Constants
    public const float EyeHeight = 1f;
    #endregion

    #region Public Methods
    public static Vector3 Flat(Vector3 position) => new(position.x, 0f, position.z);

    public static bool InRange(Vector3 origin, Vector3 target, float radius) => (Flat(target) - Flat(origin)).sqrMagnitude <= radius * radius;

    public static bool InViewAngle(Transform viewer, Vector3 target, float viewAngle)
    {
        Vector3 dir = Flat(target) - Flat(viewer.position);
        if (dir.sqrMagnitude < 0.0001f) return true;
        return Vector3.Angle(viewer.forward, dir) <= viewAngle * 0.5f;
    }

    public static bool InLineOfSight(Vector3 origin, Vector3 target, LayerMask wallMask)
    {
        Vector3 offset = Vector3.up * EyeHeight;
        return !Physics.Linecast(Flat(origin) + offset, Flat(target) + offset, wallMask, QueryTriggerInteraction.Ignore);
    }

    public static bool InLineOfSight(Vector3 origin, Vector3 target, LayerMask wallMask, out Vector3 hitPoint)
    {
        Vector3 offset = Vector3.up * EyeHeight;
        bool blocked = Physics.Linecast(Flat(origin) + offset, Flat(target) + offset, out RaycastHit hit, wallMask, QueryTriggerInteraction.Ignore);
        hitPoint = blocked ? Flat(hit.point) : Flat(target);
        return !blocked;
    }

    public static bool InFieldOfView(Transform viewer, Vector3 target, float radius, float viewAngle, LayerMask wallMask)
    {
        return InRange(viewer.position, target, radius) && InViewAngle(viewer, target, viewAngle) && InLineOfSight(viewer.position, target, wallMask);
    }

    public static bool IsPathClear(Vector3 origin, Vector3 target, float radius, LayerMask wallMask)
    {
        Vector3 offset = Vector3.up * EyeHeight;
        Vector3 start = Flat(origin) + offset;
        Vector3 dir = Flat(target) + offset - start;
        float distance = dir.magnitude;
        if (distance < 0.001f) return true;
        if (Physics.CheckSphere(start, radius, wallMask, QueryTriggerInteraction.Ignore)) return false;
        return !Physics.SphereCast(start, radius, dir / distance, out _, distance, wallMask, QueryTriggerInteraction.Ignore);
    }
    #endregion
}
