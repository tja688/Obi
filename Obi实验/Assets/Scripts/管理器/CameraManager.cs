// CameraManager.cs
using UnityEngine;
using System.Collections;
using Cysharp.Threading.Tasks;
using System.Threading;

public class CameraManager : MonoBehaviour
{
    // ... (所有属性和Awake, Start, OnDestroy等方法保持不变) ...
    #region Unchanged Region 1
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

    private Vector3 lastPosition;
    private Vector3 extrapolatedPos;
    private float yaw, pitch;
    private Transform mechanismTargetTransform;
    private bool lookAtPlayerInMechanismMode = false;
    
    private CancellationTokenSource transitionCts;

    private void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
        
        EventCenter.AddListener<IControllable>("PlayerChange", OnPlayerChanged);
        
        transitionCts = new CancellationTokenSource();
    }

    private void Start()
    {
        InitializeFromTarget();
        // 初始状态下锁定并隐藏鼠标
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    
    private void OnDestroy()
    {
        EventCenter.RemoveListener<IControllable>("PlayerChange", OnPlayerChanged);

        transitionCts?.Cancel();
        transitionCts?.Dispose();
    }
    #endregion

    #region 公共接口 (已优化)
    
    /// <summary>
    /// 进入机关视角模式。
    /// </summary>
    public async void EnterMechanismMode(Transform viewTransform, bool lookAtPlayer = false)
    {
        transitionCts?.Cancel();
        transitionCts = new CancellationTokenSource();

        PlayerController.instance.CurrentControlledObject?.ClearMove();
        PlayerController.instance.RequestStateChange(PlayerController.ControlState.Disabled);

        mechanismTargetTransform = viewTransform;
        lookAtPlayerInMechanismMode = lookAtPlayer;
        
        await TransitionToAsync(viewTransform.position, viewTransform.rotation, CameraState.MechanismMode);
        
        // 【优化】运镜到机关视角后，显示并解锁鼠标，以供机关交互 (需求 #1)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        // 需要提供对玩家的控制恢复，如有禁用需要应在具体机关中进行禁用请求。
        PlayerController.instance.RequestStateChange(PlayerController.ControlState.Gameplay3D);
    }

    /// <summary>
    /// 返回玩家视角模式。
    /// </summary>
    public async void EnterPlayerMode()
    {
        // transitionCts?.Cancel();
        // transitionCts = new CancellationTokenSource();
        //
        // PlayerController.instance.CurrentControlledObject?.ClearMove();
        // PlayerController.instance.RequestStateChange(PlayerController.ControlState.Disabled);
        //
        // // 【修正】在计算目标位置前，强制刷新相机对玩家当前状态的认知
        // if (PlayerController.instance.CurrentControlledObject != null)
        // {
        //     OnPlayerChanged(PlayerController.instance.CurrentControlledObject);
        // }
        //
        // // 现在，基于最新的玩家状态来计算返回目标点
        // Quaternion targetRotation = Quaternion.Euler(pitch, yaw, 0);
        // Vector3 targetPosition = extrapolatedPos - (targetRotation * Vector3.forward * distanceFromTarget);
        //
        // await TransitionToAsync(targetPosition, targetRotation, CameraState.PlayerMode);
        //
        // PlayerController.instance.RequestStateChange(PlayerController.ControlState.Gameplay3D);

        currentState = CameraState.PlayerMode;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    #endregion
    
    // ... (其他所有方法，如OnPlayerChanged, LateUpdate, TransitionToAsync等，保持不变) ...
    #region Unchanged Region 2
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

    private void HandleMechanismMode()
    {
        if (lookAtPlayerInMechanismMode && target != null)
        {
            transform.rotation = Quaternion.LookRotation(target.position - transform.position);
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
    #endregion
}