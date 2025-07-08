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
    [Tooltip("鼠标移动的灵敏度。")]
    public float mouseSensitivity = 0.8f;

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

        // 从相机的初始角度初始化yaw和pitch，防止开始时跳变
        Vector3 startAngles = transform.eulerAngles;
        yaw = startAngles.y;
        pitch = startAngles.x;

        // 锁定并隐藏鼠标指针，以获得更好的游戏体验
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void FixedUpdate()
    {
        if (!target) return;

        // 这部分逻辑保持不变：计算并平滑一个预判的目标位置
        // 这使得相机跟随的中心点更加平滑，而不是生硬地跟着目标
        var positionDelta = target.position - lastPosition;
        positionDelta.y = 0; // 可以忽略垂直方向的移动，让预判更稳定

        extrapolatedPos = Vector3.Lerp(target.position + positionDelta * extrapolation, extrapolatedPos, smoothness);

        lastPosition = target.position;
    }

    public void LateUpdate()
    {
        if (!target) return;

        // --- 步骤 1: 根据鼠标输入更新相机旋转 ---
        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity; // Y轴输入取反是标准操作

        // 限制垂直角度，防止相机翻转
        pitch = Mathf.Clamp(pitch, pitchClamp.x, pitchClamp.y);

        // 直接应用计算出的旋转。这提供了即时响应的瞄准手感
        transform.rotation = Quaternion.Euler(pitch, yaw, 0);

        // --- 步骤 2: 更新相机位置以跟随目标 ---
        // 计算相机的期望位置：它应该在目标（平滑后的预判点）的后方，并保持指定距离
        Vector3 desiredPosition = extrapolatedPos - (transform.forward * distanceFromTarget);

        // 使用Lerp平滑地将相机移动到期望位置，这创造了自然的“懒人”跟随效果
        transform.position = Vector3.Lerp(transform.position, desiredPosition, linearSpeed);
    }

    public void Teleport(Vector3 position, Quaternion rotation)
    {
        transform.position = position;
        transform.rotation = rotation;

        if (target != null)
            extrapolatedPos = lastPosition = target.position;

        // 重要：传送后，必须同步更新yaw和pitch的值以匹配新的朝向
        Vector3 newAngles = rotation.eulerAngles;
        yaw = newAngles.y;
        pitch = newAngles.x;
    }
}