// CameraManager.cs
using UnityEngine;
using System.Collections;
using Cysharp.Threading.Tasks;
using System.Threading;

public class CameraManager : MonoBehaviour
{
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

    // --- 【新增】机关模式专属设置 ---
    [Header("机关模式设置")]
    [Tooltip("在机关模式下，摄像机跟随鼠标旋转的平滑速度。")]
    public float mechanismLookSpeed = 5f;
    [Tooltip("在机关模式下，摄像机看向鼠标时，目标点与摄像机的距离。")]
    public float mechanismLookDistance = 15f;
    // ---

    private Vector3 lastPosition;
    private Vector3 extrapolatedPos;
    private float yaw, pitch;
    private Transform mechanismTargetTransform;
    
    // 【修改】变量重命名，使其意义更明确
    private bool followMouseInMechanismMode = false;
    
    private CancellationTokenSource transitionCts;
    private new Camera camera; // 【新增】缓存摄像机组件

    private void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
        
        EventCenter.AddListener<IControllable>("PlayerChange", OnPlayerChanged);
        
        transitionCts = new CancellationTokenSource();
        camera = GetComponent<Camera>(); // 【新增】获取并缓存Camera组件
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
    #endregion

    #region 公共接口 (已优化)
    
    /// <summary>
    /// 进入机关视角模式。
    /// </summary>
    /// <param name="viewTransform">机关提供的摄像机点位和初始朝向。</param>
    /// <param name="followMouse">【修改】是否在机关模式下让摄像机平滑跟随鼠标。</param>
    public async void EnterMechanismMode(Transform viewTransform, bool followMouse = false)
    {
        transitionCts?.Cancel();
        transitionCts = new CancellationTokenSource();

        PlayerController.instance.CurrentControlledObject?.ClearMove();
        PlayerController.instance.RequestStateChange(PlayerController.ControlState.Disabled);

        mechanismTargetTransform = viewTransform;
        // 【修改】使用新的变量名和参数
        followMouseInMechanismMode = followMouse;
        
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
        // 您原来的代码已注释，保持不变
        // transitionCts?.Cancel();
        // transitionCts = new CancellationTokenSource();
        // ...

        currentState = CameraState.PlayerMode;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    #endregion
    
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
                // --- 【核心修改】 ---
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

    // --- 【核心修改】 ---
    /// <summary>
    /// 处理机关模式下的摄像机行为。
    /// 如果 followMouseInMechanismMode 为true，摄像机将平滑地转向鼠标指针在3D空间中的投影点。
    /// </summary>
    private void HandleMechanismMode()
    {
        if (followMouseInMechanismMode)
        {
            // 1. 从摄像机发出一条经过鼠标指针的射线
            Ray ray = camera.ScreenPointToRay(Input.mousePosition);

            // 2. 计算射线上距离摄像机 mechanismLookDistance 远的点作为目标观看点
            Vector3 lookTargetPoint = ray.GetPoint(mechanismLookDistance);

            // 3. 计算从当前位置看向目标点的目标旋转值
            Quaternion targetRotation = Quaternion.LookRotation(lookTargetPoint - transform.position);

            // 4. 使用Slerp平滑地将当前旋转插值到目标旋转，实现丝滑的跟随效果
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, mechanismLookSpeed * Time.deltaTime);
        }
        // 如果 followMouseInMechanismMode 为 false，则不执行任何操作，摄像机保持在运镜结束时的固定旋转状态。
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
            // 如果转换被取消，我们需要根据当前模式决定鼠标状态
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
    #endregion
}