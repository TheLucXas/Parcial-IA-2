using System;
using UnityEngine;

public static class SteeringUtils
{
    #region Public Methods
    public static Vector3 CalculateSeek(Vector3 currentPosition, Vector3 velocity, Vector3 targetPosition, float maxSpeed, float maxForce)
    {
        Vector3 desired = (targetPosition - currentPosition).normalized * maxSpeed;
        return CalculateSteering(velocity, desired, maxForce);
    }

    public static Vector3 CalculateFlee(Vector3 currentPosition, Vector3 velocity, Vector3 targetPosition, float maxSpeed, float maxForce)
    {
        Vector3 desired = (targetPosition - currentPosition).normalized * maxSpeed;
        return CalculateSteering(velocity, -desired, maxForce);
    }

    public static Vector3 CalculateArrive(Vector3 currentPosition, Vector3 velocity, Vector3 targetPosition, float maxSpeed, float maxForce, float arriveRadius)
    {
        Vector3 dir = targetPosition - currentPosition;
        float distance = dir.magnitude;
        float speed = maxSpeed;

        if (distance <= arriveRadius)
        {
            speed *= distance / arriveRadius;
        }

        Vector3 desired = dir.normalized * speed;
        return CalculateSteering(velocity, desired, maxForce);
    }

    public static Vector3 GetFuturePosition(Vector3 currentPosition, float maxSpeed, Vector3 targetPosition, Vector3 targetVelocity)
    {
        float distanceToTarget = (targetPosition - currentPosition).magnitude;
        float predictedTime = distanceToTarget / (maxSpeed + targetVelocity.magnitude);
        return targetPosition + targetVelocity * predictedTime;
    }

    public static Vector3 CalculatePursuit(Vector3 currentPosition, Vector3 velocity, float maxSpeed, float maxForce, Vector3 targetPosition, Vector3 targetVelocity)
    {
        Vector3 futurePos = GetFuturePosition(currentPosition, maxSpeed, targetPosition, targetVelocity);
        return CalculateSeek(currentPosition, velocity, futurePos, maxSpeed, maxForce);
    }

    public static Vector3 CalculateEvade(Vector3 currentPosition, Vector3 velocity, float maxSpeed, float maxForce, Vector3 targetPosition, Vector3 targetVelocity)
    {
        Vector3 futurePos = GetFuturePosition(currentPosition, maxSpeed, targetPosition, targetVelocity);
        return CalculateFlee(currentPosition, velocity, futurePos, maxSpeed, maxForce);
    }

    public static Vector3 CalculateSteering(Vector3 velocity, Vector3 desired, float maxForce)
    {
        Vector3 steering = desired - velocity;
        steering = Vector3.ClampMagnitude(steering, maxForce);
        return steering * Time.deltaTime;
    }
    public static void ApplyVelocity(Transform transform, ref Vector3 velocity, float radius, LayerMask wallMask, Func<Vector3, bool> isForbidden = null)
    {
        velocity.y = 0f;
        Vector3 position = transform.position;
        Vector3 delta = velocity * Time.deltaTime;

        if (IsFree(position + delta, radius, wallMask, isForbidden)) transform.position = position + delta;
        else if (IsFree(position + new Vector3(delta.x, 0f, 0f), radius, wallMask, isForbidden))
        {
            transform.position = position + new Vector3(delta.x, 0f, 0f);
            velocity.z = 0f;
        }
        else if (IsFree(position + new Vector3(0f, 0f, delta.z), radius, wallMask, isForbidden))
        {
            transform.position = position + new Vector3(0f, 0f, delta.z);
            velocity.x = 0f;
        }
        else velocity = Vector3.zero;

        if (velocity.sqrMagnitude > 0.0001f) transform.forward = velocity.normalized;
    }

    private static bool IsFree(Vector3 position, float radius, LayerMask wallMask, Func<Vector3, bool> isForbidden)
    {
        if (isForbidden != null && isForbidden(position)) return false;
        return !Physics.CheckSphere(position + Vector3.up * PerceptionUtils.EyeHeight, radius, wallMask, QueryTriggerInteraction.Ignore);
    }
    #endregion
}
