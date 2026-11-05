using UnityEngine;

public static class GizmosUtils
{
    #region Public Methods
    public static void DrawGizmosCircle(Vector3 pos, Vector3 normal, float radius, int numSegments = 32)
    {
        Vector3 temp = (normal.x < normal.z) ? new Vector3(1f, 0f, 0f) : new Vector3(0f, 0f, 1f);
        Vector3 forward = Vector3.Cross(normal, temp).normalized;
        Vector3 right = Vector3.Cross(forward, normal).normalized;

        Vector3 prevPt = pos + (forward * radius);
        float angleStep = (Mathf.PI * 2f) / numSegments;
        for (int i = 0; i < numSegments; i++)
        {
            float angle = (i == numSegments - 1) ? 0f : (i + 1) * angleStep;

            Vector3 nextPtLocal = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
            Vector3 nextPt = pos + (right * nextPtLocal.x) + (forward * nextPtLocal.z);

            Gizmos.DrawLine(prevPt, nextPt);

            prevPt = nextPt;
        }
    }
    #endregion
}