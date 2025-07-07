using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class PlayerControl_CameraControl : MonoBehaviour
{
    // [无需改动] 所有字段变量保持不变
    [Header("Camera Settings")]
    [SerializeField] private Camera controlledCamera;
    [SerializeField] private Transform target;
    [SerializeField] private float distance = 5.0f;
    [SerializeField] private float mouseSensitivity = 3.0f;
    [SerializeField] private float minVerticalAngle = -20.0f;
    [SerializeField] private float maxVerticalAngle = 80.0f;
    [SerializeField] private float smoothTime = 0.2f;

    [Header("Collision Settings")]
    [SerializeField] private bool avoidCollisions = true;
    [SerializeField] private LayerMask collisionLayers;
    [SerializeField] private float collisionOffset = 0.3f;
    [SerializeField] private float sphereCastRadius = 0.3f;
    [SerializeField] private float heightAdjustmentSpeed = 2.0f;
    [SerializeField] private float minHeightAdjustment = 0.5f;
    [SerializeField] private float maxHeightAdjustment = 2.0f;
    [SerializeField] private float collisionDamping = 0.5f;
    [SerializeField] private float lookAtHeightRatio = 0.5f;
    [SerializeField] private float lookAtSmoothTime = 0.2f;

    [Header("Follow Mode")]
    [SerializeField] private bool useLazyFollow;
    [SerializeField] [Range(0.01f, 1f)] private float lazyFollowFactor = 0.1f;
    [SerializeField] private bool scaleWithTime = true;
    
    private InputAction lookAction;

    private float currentX;
    private float currentY;
    private float currentDistance;
    private float targetDistance;
    private Vector3 smoothVelocity = Vector3.zero;
    private bool isCameraControlActive;
    private float originalHeightOffset;
    private float currentHeightOffset;
    private float targetHeightOffset;
    private float dampedDistance;
    private float distanceVelocity;
    private float currentLookAtHeightOffset;
    private float lookAtHeightVelocity;
    
    private void OnEnable()
    {
        // 尝试获取 PlayerInput 组件
        var playerInput = GetComponent<PlayerInput>();
        if (playerInput == null)
        {
            Debug.LogError("PlayerInput component not found on this GameObject. Please add one.", this);
            this.enabled = false;
            return;
        }

        // 通过名字找到 "Look" Action
        lookAction = playerInput.actions["Look"];
        if (lookAction != null)
        {
            // 订阅 performed 事件
            lookAction.performed += OnLookPerformed;
            // 启用 Action
            lookAction.Enable();
        }
    }

    // [新增] OnDisable 会在对象禁用或销毁时调用
    private void OnDisable()
    {
        if (lookAction != null)
        {
            // 取消订阅，防止内存泄漏！
            lookAction.performed -= OnLookPerformed;
            // 禁用 Action
            lookAction.Disable();
        }
    }

    protected void Start()
    {
        // Start 方法内容基本不变
        if (controlledCamera == null) controlledCamera = Camera.main;
        if (target == null)
        {
            var player = transform;
            if (player != null) target = player.transform;
        }

        if (controlledCamera == null || target == null) return;

        currentDistance = distance;
        targetDistance = distance;
        dampedDistance = distance;
        var angles = controlledCamera.transform.eulerAngles;
        currentX = angles.y;
        currentY = angles.x;

        originalHeightOffset = controlledCamera.transform.position.y - target.position.y;
        currentHeightOffset = originalHeightOffset;
        targetHeightOffset = originalHeightOffset;

        currentLookAtHeightOffset = 0f;
        isCameraControlActive = true;
    }

    // [新增] 这是事件触发时调用的方法
    private void OnLookPerformed(InputAction.CallbackContext context)
    {
        if (!isCameraControlActive) return;

        // 直接从 context 读取 Vector2 值
        Vector2 lookInput = context.ReadValue<Vector2>();

        // 注意：这里不再需要乘以 Time.deltaTime！
        // 因为 Delta[Mouse] 已经提供了帧之间的变化量。
        // 你可能需要调整 mouseSensitivity 来获得合适的手感。
        currentX += lookInput.x * mouseSensitivity * 0.1f; // 乘以一个小的固定系数来调整手感
        currentY -= lookInput.y * mouseSensitivity * 0.1f;
        currentY = Mathf.Clamp(currentY, minVerticalAngle, maxVerticalAngle);
    }

    // [修改] Update 方法不再处理输入
    private void Update()
    {
        if (!isCameraControlActive || !controlledCamera || !target) return;
        
        // 只保留与输入无关的逻辑
        currentHeightOffset = Mathf.Lerp(currentHeightOffset, targetHeightOffset, Time.deltaTime * heightAdjustmentSpeed);
    }

    private void LateUpdate()
    {
        if (!isCameraControlActive || !controlledCamera || !target) return;
        
        UpdateCameraPosition();
    }

    // UpdateCameraPosition 和其他方法保持完全不变
    private void UpdateCameraPosition()
    {
        // ... 此处代码无需任何改动 ...
        var direction = new Vector3(0, 0, -dampedDistance);
        var rotation = Quaternion.Euler(currentY, currentX, 0);
        var desiredPosition = target.position + rotation * direction;
        desiredPosition.y += currentHeightOffset;

        if (avoidCollisions)
        {
            var directionToCamera = desiredPosition - target.position;
            var distanceToCamera = directionToCamera.magnitude;
            var ray = new Ray(target.position, directionToCamera.normalized);

            if (Physics.SphereCast(ray, sphereCastRadius, out var hit, distanceToCamera, collisionLayers))
            {
                var collisionPosition = hit.point + hit.normal * collisionOffset;
                var actualDistance = Vector3.Distance(target.position, collisionPosition);

                dampedDistance = Mathf.SmoothDamp(dampedDistance, actualDistance, ref distanceVelocity, collisionDamping);

                var distanceRatio = Mathf.InverseLerp(collisionOffset, distance, dampedDistance);
                targetHeightOffset = Mathf.Lerp(maxHeightAdjustment, minHeightAdjustment, distanceRatio);

                collisionPosition.y = Mathf.Max(collisionPosition.y, target.position.y + minHeightAdjustment);
                desiredPosition = collisionPosition;

                currentDistance = Mathf.Lerp(currentDistance, dampedDistance, 0.1f);

                var targetLookAtOffset = (currentHeightOffset - originalHeightOffset) * lookAtHeightRatio;
                currentLookAtHeightOffset = Mathf.SmoothDamp(
                    currentLookAtHeightOffset,
                    targetLookAtOffset,
                    ref lookAtHeightVelocity,
                    lookAtSmoothTime);
            }
            else
            {
                dampedDistance = Mathf.SmoothDamp(dampedDistance, targetDistance, ref distanceVelocity, collisionDamping);
                targetHeightOffset = originalHeightOffset;

                currentLookAtHeightOffset = Mathf.SmoothDamp(
                    currentLookAtHeightOffset,
                    0f,
                    ref lookAtHeightVelocity,
                    lookAtSmoothTime);
            }
        }

        if (useLazyFollow)
        {
            var deltaFactor = scaleWithTime ? lazyFollowFactor * Time.deltaTime * 60f : lazyFollowFactor;
            deltaFactor = Mathf.Clamp01(deltaFactor);

            var currentPos = controlledCamera.transform.position;
            currentPos += (desiredPosition - currentPos) * deltaFactor;
            controlledCamera.transform.position = currentPos;
        }
        else
        {
            controlledCamera.transform.position = Vector3.SmoothDamp(
                controlledCamera.transform.position,
                desiredPosition,
                ref smoothVelocity,
                smoothTime);
        }

        var baseLookAtPosition = target.position;
        var adjustedLookAtPosition = baseLookAtPosition + Vector3.up * currentLookAtHeightOffset;
        controlledCamera.transform.LookAt(adjustedLookAtPosition);

        controlledCamera.nearClipPlane = Mathf.Clamp(
            dampedDistance * 0.1f,
            0.01f,
            0.3f
        );
    }

    private void EnableCameraControl()
    {
        isCameraControlActive = true;
    }

    private void DisableCameraControl()
    {
        isCameraControlActive = false;
    }

    public void SetControlledCamera(Camera newCamera)
    {
        controlledCamera = newCamera;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void SetMouseSensitivity(float sensitivity)
    {
        mouseSensitivity = sensitivity;
    }

    public float GetCurrentX()
    {
        return currentX;
    }



    public float GetCurrentY()
    {
        return currentY;
    }
}

