// CameraManager.cs
using UnityEngine;
using System.Collections;
using Cysharp.Threading.Tasks;
using System.Threading;

public class CameraManager : MonoBehaviour
{
    #region Singleton
    public static CameraManager instance { get; private set; }
    #endregion

    public enum CameraState { PlayerMode, MechanismMode, Transitioning }

    [Header("通用设置")]
    [SerializeField] private CameraState currentState = CameraState.PlayerMode;
    public float transitionDuration = 1.5f;

    [Header("玩家模式设置")]
    public Transform target;
    [Range(0, 1)] public float linearSpeed = 0.02f;
    [Min(0)] public float distanceFromTarget = 5;
    public float mouseSensitivity = 0.1f;
    public Vector2 pitchClamp = new Vector2(-10, 45);

    [Header("移动预判")]
    public float extrapolation = 12;
    [Range(0, 1)] public float smoothness = 0.5f;
    
    // 【修改】碰撞处理部分
    [Header("碰撞处理")]
    [Tooltip("摄像机需要进行碰撞检测的层。")]
    public LayerMask collisionLayers;
    [Tooltip("用于球形碰撞检测的摄像机半径，防止边缘穿模。建议值 0.2-0.4。")]
    public float cameraRadius = 0.3f; // 【替换】使用半径代替简单的偏移

    [Header("机关模式设置")]
    [Tooltip("在鼠标跟随模式下，摄像机小范围转动的角度限制（X:上下, Y:左右）。")]
    public Vector2 mechanismAngleLimit = new Vector2(10f, 15f);
    [Tooltip("在机关模式下，摄像机跟随的平滑速度。")]
    public float mechanismFollowSpeed = 5f;

    private Vector3 lastPosition;
    private Vector3 extrapolatedPos;
    private float yaw, pitch;
    private Transform mechanismTargetTransform;
    private Transform mechanismFollowTarget;
    private Quaternion mechanismBaseRotation;
    
    private CancellationTokenSource transitionCts;
    private new Camera camera;

    private void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
        
        EventCenter.AddListener<IControllable>("PlayerChange", OnPlayerChanged);
        
        transitionCts = new CancellationTokenSource();
        camera = GetComponent<Camera>();
    }

    private void Start()
    {
        InitializeFromTarget();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    
    private void OnDestroy()
    {
        EventCenter.RemoveListener<IControllable>("PlayerChange", OnPlayerChanged);

        transitionCts?.Cancel();
        transitionCts?.Dispose();
    }

    #region 公共接口
    
    public async void EnterMechanismMode(Transform viewTransform, Transform followTarget = null)
    {
        transitionCts?.Cancel();
        transitionCts = new CancellationTokenSource();

        PlayerController.instance.CurrentControlledObject?.ClearMove();
        PlayerController.instance.RequestStateChange(PlayerController.ControlState.Disabled);

        mechanismTargetTransform = viewTransform;
        this.mechanismFollowTarget = followTarget;
        
        if (followTarget == null)
        {
            mechanismBaseRotation = viewTransform.rotation;
        }
        
        await TransitionToAsync(viewTransform.position, viewTransform.rotation, CameraState.MechanismMode);
        
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        PlayerController.instance.RequestStateChange(PlayerController.ControlState.Gameplay3D);
    }

    public async void EnterPlayerMode()
    {
        currentState = CameraState.PlayerMode;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    #endregion
    
    private void OnPlayerChanged(IControllable newPlayer)
    {
        if (newPlayer == null) return;
        target = newPlayer.controlledGameObject.transform;
        Debug.Log($"Camera Manager: Target set to {target.name}");
        InitializeFromTarget();
    }

    private void InitializeFromTarget()
    {
        if (target != null)
        {
            lastPosition = target.position;
            extrapolatedPos = target.position;
        }
        Vector3 startAngles = transform.eulerAngles;
        yaw = startAngles.y;
        pitch = startAngles.x;
    }

    private void FixedUpdate()
    {
        if (currentState != CameraState.PlayerMode || !target) return;
        var positionDelta = target.position - lastPosition;
        positionDelta.y = 0;
        extrapolatedPos = Vector3.Lerp(target.position + positionDelta * extrapolation, extrapolatedPos, smoothness);
        lastPosition = target.position;
    }

    private void LateUpdate()
    {
        switch (currentState)
        {
            case CameraState.PlayerMode:
                HandlePlayerMode();
                break;
            case CameraState.MechanismMode:
                HandleMechanismMode();
                break;
        }
    }

    /// <summary>
    /// 【重构】处理玩家模式下的摄像机行为，使用 SphereCast 进行碰撞检测。
    /// </summary>
    private void HandlePlayerMode()
    {
        if (!target || PlayerController.instance == null) return;
        
        // 1. 根据输入计算期望的旋转
        Vector2 lookInput = PlayerController.instance.lookInput;
        yaw += lookInput.x * mouseSensitivity;
        pitch -= lookInput.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, pitchClamp.x, pitchClamp.y);
        
        Quaternion desiredRotation = Quaternion.Euler(pitch, yaw, 0);
        
        // 2. 计算不考虑碰撞的理想摄像机位置和方向
        Vector3 idealPosition = extrapolatedPos - (desiredRotation * Vector3.forward * distanceFromTarget);
        Vector3 castOrigin = extrapolatedPos;
        Vector3 castDirection = (idealPosition - castOrigin).normalized;
        float castDistance = Vector3.Distance(idealPosition, castOrigin);

        // 3. 【修改为 SphereCast】进行碰撞检测
        RaycastHit hit;
        Vector3 finalPosition;

        if (Physics.SphereCast(castOrigin, cameraRadius, castDirection, out hit, castDistance, collisionLayers))
        {
            // 如果球形投射击中物体，最终位置为碰撞点回退一点，确保摄像机球体刚好在障碍物外
            finalPosition = castOrigin + castDirection * hit.distance;
        }
        else
        {
            // 如果没有击中任何物体，则使用理想位置
            finalPosition = idealPosition;
        }

        // 4. 应用旋转和最终计算出的位置
        transform.rotation = desiredRotation;
        transform.position = Vector3.Lerp(transform.position, finalPosition, linearSpeed);
    }

    private void HandleMechanismMode()
    {
        if (mechanismFollowTarget != null)
        {
            Vector3 directionToTarget = mechanismFollowTarget.position - transform.position;
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, mechanismFollowSpeed * Time.deltaTime);
        }
        else
        {
            float mouseX = (Input.mousePosition.x / Screen.width) - 0.5f;
            float mouseY = (Input.mousePosition.y / Screen.height) - 0.5f;

            float yawOffset = mouseX * mechanismAngleLimit.y * 2f;
            float pitchOffset = -mouseY * mechanismAngleLimit.x * 2f;

            Quaternion offsetRotation = Quaternion.Euler(pitchOffset, yawOffset, 0);
            Quaternion targetRotation = mechanismBaseRotation * offsetRotation;

            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, mechanismFollowSpeed * Time.deltaTime);
        }
    }

    private async UniTask TransitionToAsync(Vector3 targetPosition, Quaternion targetRotation, CameraState stateAfterTransition)
    {
        currentState = CameraState.Transitioning;
        float time = 0;
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        
        try
        {
            while (time < transitionDuration)
            {
                float t = Mathf.SmoothStep(0f, 1f, time / transitionDuration);
                transform.position = Vector3.Lerp(startPos, targetPosition, t);
                transform.rotation = Quaternion.Slerp(startRot, targetRotation, t);
                time += Time.deltaTime;
                
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: transitionCts.Token);
            }
        }
        catch (System.OperationCanceledException)
        {
            Debug.Log("Camera transition was cancelled.");
            if(stateAfterTransition == CameraState.MechanismMode)
            {
                 Cursor.lockState = CursorLockMode.None;
                 Cursor.visible = true;
            }
            return;
        }

        transform.position = targetPosition;
        transform.rotation = targetRotation;
        currentState = stateAfterTransition;
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