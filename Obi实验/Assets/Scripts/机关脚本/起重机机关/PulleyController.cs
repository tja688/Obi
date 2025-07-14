// PulleyController.cs
using UnityEngine;

/// <summary>
/// 控制滑轮的旋转。它会观察一个目标Transform（如配重块），
/// 并根据其在预设路径上的移动来旋转自身。
/// </summary>
public class PulleyController : MonoBehaviour
{
    [Header("关联对象")]
    [Tooltip("需要观察其位置变化的目标对象，通常是配重块。")]
    public Transform targetToTrack;

    [Header("滑轮参数")]
    [Tooltip("滑轮的半径，用于计算旋转角度。")]
    public float pulleyRadius = 0.5f;

    [Tooltip("滑轮的本地旋转轴。")]
    public Vector3 rotationAxis = Vector3.forward; // 通常是Z轴

    // 私有变量
    private Vector3 lastTargetPosition;
    private Transform pathStart; // 从关联的配重块获取路径信息
    private Transform pathEnd;
    private Vector3 pathDirection;

    void Start()
    {
        if (targetToTrack == null)
        {
            Debug.LogError("请为滑轮指定一个 'Target To Track'！", this);
            enabled = false;
            return;
        }

        // 尝试从目标对象上获取路径信息，实现自动关联
        CounterweightController targetController = targetToTrack.GetComponent<CounterweightController>();
        if (targetController != null)
        {
            pathStart = targetController.pathStart;
            pathEnd = targetController.pathEnd;
        }
        
        if (pathStart == null || pathEnd == null)
        {
            Debug.LogError($"无法从目标'{targetToTrack.name}'上获取路径信息，请确保目标挂载了CounterweightController并配置了路径。", this);
            enabled = false;
            return;
        }

        pathDirection = (pathEnd.position - pathStart.position).normalized;
        lastTargetPosition = targetToTrack.position;
    }

    void LateUpdate()
    {
        // 计算目标对象在当前帧的位移
        Vector3 movementDelta = targetToTrack.position - lastTargetPosition;
        if (movementDelta.sqrMagnitude < 0.00001f)
        {
            return; // 位移过小，无需计算
        }

        // 将位移投影到路径方向上，得到有符号的移动距离
        float distanceMovedAlongPath = Vector3.Dot(movementDelta, pathDirection);

        // 根据公式：旋转角度 = 弧长 / 半径 (结果为弧度)
        // 弧长即为绳索移动的距离
        if (pulleyRadius > 0)
        {
            float angleInRadians = distanceMovedAlongPath / pulleyRadius;
            float angleInDegrees = angleInRadians * Mathf.Rad2Deg;

            // 沿着指定的轴旋转滑轮。注意这里需要反转一下角度，因为通常配重块向下移动，滑轮应做正向旋转
            transform.Rotate(rotationAxis, -angleInDegrees, Space.Self);
        }

        // 更新上一帧的位置
        lastTargetPosition = targetToTrack.position;
    }
}