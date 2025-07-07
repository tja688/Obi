using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
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
        if (PlayerControl_Ball.instance == null || PlayerControl_Ball.instance.actorTrans == null) return;
        target = PlayerControl_Ball.instance.actorTrans;
        lastPosition = target.position;
        extrapolatedPos = target.position; // 初始化 extrapolatedPos
    }

    private void LateUpdate()
    {
        if (!target)
        {
            // 如果目标丢失，尝试重新获取
            if (PlayerControl_Ball.instance && PlayerControl_Ball.instance.actorTrans)
            {
                target = PlayerControl_Ball.instance.actorTrans;
            }
            else
            {
                return; // 如果仍然没有目标，则不执行任何操作
            }
        }

        // --- 1. 计算预测位置 (原 FixedUpdate 的逻辑) ---
        var positionDelta = target.position - lastPosition;
        positionDelta.y = 0; // 忽略Y轴变化，使镜头在水平面上更稳定

        // 使用 Lerp 平滑地更新预测位置
        extrapolatedPos = Vector3.Lerp(target.position + positionDelta * extrapolation, extrapolatedPos, smoothness);

        lastPosition = target.position; // 更新上一帧的位置

        // --- 2. 更新摄像机位置和旋转 (原 LateUpdate 的逻辑) ---
        var toTarget = extrapolatedPos - transform.position;

        // 使用 Slerp (球面线性插值) 以获得更平滑的旋转
        if (toTarget != Vector3.zero) // 防止 LookRotation 报错
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toTarget), rotationalSpeed);
        }

        toTarget.y = 0; // 同样忽略Y轴，让摄像机与目标的距离计算基于水平面

        // 平滑地移动摄像机到目标距离
        if (toTarget.magnitude > 0.01f) // 避免在原地做不必要的计算
        {
            transform.position += toTarget.normalized * ((toTarget.magnitude - distanceFromTarget) * linearSpeed);
        }
    }

    public void Teleport(Vector3 position, Quaternion rotation)
    {
        transform.position = position;
        transform.rotation = rotation;

        if (target != null)
            extrapolatedPos = lastPosition = target.position;
    }
}

