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

    [Header("机关模式设置")]
    [Tooltip("在鼠标跟随模式下，摄像机小范围转动的角度限制（X:上下, Y:左右）。")]
    public Vector2 mechanismAngleLimit = new Vector2(10f, 15f);
    [Tooltip("在机关模式下，摄像机跟随的平滑速度。")]
    public float mechanismFollowSpeed = 5f;

    private Vector3 lastPosition;
    private Vector3 extrapolatedPos;
    private float yaw, pitch;
    private Transform mechanismTargetTransform;
    private Transform mechanismFollowTarget; // 【新增】用于存储机关模式下要跟随的特定目标
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
    
    /// <summary>
    /// 【修改】进入机关视角模式。
    /// </summary>
    /// <param name="viewTransform">机关提供的摄像机点位和初始朝向。</param>
    /// <param name="followTarget">【新】一个可选的特定跟随目标。如果为null，则启用有限的鼠标跟随；否则，摄像机将朝向此目标。</param>
    public async void EnterMechanismMode(Transform viewTransform, Transform followTarget = null)
    {
        transitionCts?.Cancel();
        transitionCts = new CancellationTokenSource();

        PlayerController.instance.CurrentControlledObject?.ClearMove();
        PlayerController.instance.RequestStateChange(PlayerController.ControlState.Disabled);

        mechanismTargetTransform = viewTransform;
        this.mechanismFollowTarget = followTarget; // 保存传入的跟随目标
        
        // 只有在跟随鼠标时(即没有特定目标时)，才需要记录基准旋转用于计算偏移
        if (followTarget == null)
        {
            mechanismBaseRotation = viewTransform.rotation;
        }
        
        await TransitionToAsync(viewTransform.position, viewTransform.rotation, CameraState.MechanismMode);
        
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        PlayerController.instance.RequestStateChange(PlayerController.ControlState.Gameplay3D);
    }

    /// <summary>
    /// 返回玩家视角模式。
    /// </summary>
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

    private void HandlePlayerMode()
    {
        if (!target || PlayerController.instance == null) return;
        Vector2 lookInput = PlayerController.instance.lookInput;
        yaw += lookInput.x * mouseSensitivity;
        pitch -= lookInput.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, pitchClamp.x, pitchClamp.y);
        Quaternion desiredRotation = Quaternion.Euler(pitch, yaw, 0);
        Vector3 desiredPosition = extrapolatedPos - (desiredRotation * Vector3.forward * distanceFromTarget);
        transform.rotation = desiredRotation;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, linearSpeed);
    }

    /// <summary>
    /// 【重构】处理机关模式下的摄像机行为。
    /// 如果有特定跟随目标(mechanismFollowTarget)，则朝向该目标。
    /// 否则，执行有限范围的鼠标跟随。
    /// </summary>
    private void HandleMechanismMode()
    {
        // 情况一：如果设置了特定的跟随目标
        if (mechanismFollowTarget != null)
        {
            // 1. 计算从摄像机指向目标的向量
            Vector3 directionToTarget = mechanismFollowTarget.position - transform.position;
            
            // 2. 根据这个向量计算出目标旋转值
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
            
            // 3. 平滑地将摄像机朝向目标
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, mechanismFollowSpeed * Time.deltaTime);
        }
        // 情况二：如果没有特定目标，则执行有限范围的鼠标跟随
        else
        {
            // 1. 获取鼠标相对于屏幕中心的偏移量 (范围: -0.5 到 0.5)
            float mouseX = (Input.mousePosition.x / Screen.width) - 0.5f;
            float mouseY = (Input.mousePosition.y / Screen.height) - 0.5f;

            // 2. 根据偏移量和角度限制，计算目标旋转的偏移角度
            float yawOffset = mouseX * mechanismAngleLimit.y * 2f;
            float pitchOffset = -mouseY * mechanismAngleLimit.x * 2f;

            // 3. 在基准旋转的基础上，应用这个小范围的偏移
            Quaternion offsetRotation = Quaternion.Euler(pitchOffset, yawOffset, 0);
            Quaternion targetRotation = mechanismBaseRotation * offsetRotation;

            // 4. 平滑地将当前旋转插值到目标旋转
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