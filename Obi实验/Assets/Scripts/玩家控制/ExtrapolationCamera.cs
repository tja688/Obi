using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExtrapolationCamera : MonoBehaviour
{
    public Transform target = null;

    public float extrapolation = 10;

    [Range(0, 1)]
    public float smoothness = 0.8f;

    [Range(0, 1)]
    public float linearSpeed = 1;

    [Range(0, 1)]
    public float rotationalSpeed = 1;

    [Min(0)]
    public float distanceFromTarget = 4;

    private Vector3 lastPosition;
    private Vector3 extrapolatedPos;
    
    private void Start()
    {
        target = PlayerControl_Ball.instance.actorTrans;
        
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

    private void LateUpdate()
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

