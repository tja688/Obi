using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target = null;

    public float extrapolation = 12;

    [Range(0, 1)]
    public float smoothness = 0.9f;

    [Range(0, 1)]
    public float linearSpeed = 0.05f;

    [Range(0, 1)]
    public float rotationalSpeed = 0.05f;

    [Min(0)]
    public float distanceFromTarget = 6;

    Vector3 lastPosition;
    private Vector3 extrapolatedPos;

    private void Start()
    {
        if (target != null)
            lastPosition = target.position;
    }

    private void FixedUpdate()
    {
        if (!target) return;
        
        var positionDelta = target.position - lastPosition;
        
        positionDelta.y = 0;

        extrapolatedPos = Vector3.Lerp(target.position + positionDelta * extrapolation, extrapolatedPos, smoothness);

        lastPosition = target.position;
    }

    public void LateUpdate()
    {
        if (!target) return;
        
        var toTarget = extrapolatedPos - transform.position;

        transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(toTarget), rotationalSpeed);

        toTarget.y = 0;

        transform.position += toTarget.normalized * ((toTarget.magnitude - distanceFromTarget) * linearSpeed);
    }

    public void Teleport(Vector3 position, Quaternion rotation)
    {
        transform.position = position;
        transform.rotation = rotation;

        if (target != null)
            extrapolatedPos = lastPosition = target.position;
    }
}


