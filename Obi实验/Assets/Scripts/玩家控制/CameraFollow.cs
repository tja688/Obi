// Filename: CameraFollow.cs
// CORRECTED VERSION - Fixed initial spinning issue

using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    // --- 追踪目标 ---
    [Header("追踪设置")]
    public Transform target = null;

    // --- 跟随设置 ---
    [Tooltip("控制相机位置跟随目标的平滑度，值越小越平滑。")]
    [Range(0, 1)]
    public float linearSpeed = 0.02f;

    [Tooltip("相机与目标保持的距离。")]
    [Min(0)]
    public float distanceFromTarget = 5;

    // --- 鼠标观察 (准星) 设置 ---
    [Header("鼠标观察设置")]
    [Tooltip("鼠标移动的灵敏度。由于直接使用鼠标增量，这个值通常需要设置得比较小，例如 0.1。")]
    public float mouseSensitivity = 0.001f; // 建议从一个较小的值开始调试，例如0.1

    [Tooltip("垂直方向（俯仰角）的最小和最大角度限制。")]
    public Vector2 pitchClamp = new Vector2(-10, 45);

    // --- 移动预判，让跟随更自然 ---
    [Header("移动预判")]
    [Tooltip("对目标未来位置的预判强度，让相机移动更具前瞻性。")]
    public float extrapolation = 12;

    [Tooltip("预判位置的平滑度。")]
    [Range(0, 1)]
    public float smoothness = 0.5f;

    // --- 私有变量 ---
    private Vector3 lastPosition;
    private Vector3 extrapolatedPos;
    private float yaw;   // 水平旋转角度 (Y轴)
    private float pitch; // 垂直旋转角度 (X轴)

    private void Start()
    {
        if (target != null)
        {
            lastPosition = target.position;
            extrapolatedPos = target.position;
        }

        Vector3 startAngles = transform.eulerAngles;
        yaw = startAngles.y;
        pitch = startAngles.x;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
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

        // --- 步骤 1: 从玩家控制器获取输入并更新相机旋转 ---
        Vector2 lookInput = PlayerControl_Ball.instance != null ? PlayerControl_Ball.instance.lookInput : Vector2.zero;
        
        yaw += lookInput.x * mouseSensitivity;
        pitch -= lookInput.y * mouseSensitivity; 

        pitch = Mathf.Clamp(pitch, pitchClamp.x, pitchClamp.y);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0);

        // --- 步骤 2: 更新相机位置以跟随目标 ---
        Vector3 desiredPosition = extrapolatedPos - (transform.forward * distanceFromTarget);
        transform.position = Vector3.Lerp(transform.position, desiredPosition, linearSpeed);
    }
    
    public void Teleport(Vector3 position, Quaternion rotation)
    {
        transform.position = position;
        transform.rotation = rotation;

        if (target != null)
            extrapolatedPos = lastPosition = target.position;

        Vector3 newAngles = rotation.eulerAngles;
        yaw = newAngles.y;
        pitch = newAngles.x;
    }
}