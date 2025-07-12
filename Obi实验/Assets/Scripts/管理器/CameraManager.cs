// CameraManager.cs
using UnityEngine;
using System.Collections;

public class CameraManager : MonoBehaviour
{
    #region Singleton
    public static CameraManager instance { get; private set; }
    #endregion

    public enum CameraState
    {
        PlayerMode,      // 默认跟随玩家模式
        MechanismMode,   // 机关/固定视角模式
        Transitioning    // 切换中状态
    }

    [Header("通用设置")]
    [Tooltip("相机当前状态")]
    [SerializeField] private CameraState currentState = CameraState.PlayerMode;
    [Tooltip("切换视角时的移动速度")]
    public float transitionDuration = 1.5f;

    [Header("玩家模式设置")]
    public Transform target; // 玩家目标
    [Range(0, 1)] public float linearSpeed = 0.02f;
    [Min(0)] public float distanceFromTarget = 5;
    public float mouseSensitivity = 0.1f;
    public Vector2 pitchClamp = new Vector2(-10, 45);

    [Header("移动预判")]
    public float extrapolation = 12;
    [Range(0, 1)] public float smoothness = 0.5f;

    // --- 私有变量 ---
    private Vector3 lastPosition;
    private Vector3 extrapolatedPos;
    private float yaw;
    private float pitch;
    
    private Coroutine transitionCoroutine;
    private Transform mechanismTargetTransform;
    private bool lookAtPlayerInMechanismMode = false;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        InitializeFromTarget();
        EventCenter.AddListener<IControllable>("PlayerChange", OnPlayerChanged);

        // 游戏开始时，自动获取PlayerController中的当前玩家
        if (PlayerController.instance != null && PlayerController.instance.CurrentControlledObject != null)
        {
            OnPlayerChanged(PlayerController.instance.CurrentControlledObject);
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    
    private void OnDestroy()
    {
        EventCenter.RemoveListener<IControllable>("PlayerChange", OnPlayerChanged);
    }
    
    private void OnPlayerChanged(IControllable newPlayer)
    {
        target = newPlayer.gameObject.transform;
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
        // 预判逻辑只在玩家模式下运行
        if (currentState != CameraState.PlayerMode || !target) return;

        var positionDelta = target.position - lastPosition;
        positionDelta.y = 0;
        extrapolatedPos = Vector3.Lerp(target.position + positionDelta * extrapolation, extrapolatedPos, smoothness);
        lastPosition = target.position;
    }

    private void LateUpdate()
    {
        // 状态机驱动相机行为
        switch (currentState)
        {
            case CameraState.PlayerMode:
                HandlePlayerMode();
                break;
            case CameraState.MechanismMode:
                HandleMechanismMode();
                break;
            case CameraState.Transitioning:
                // 在过渡期间不执行任何操作
                break;
        }
    }

    private void HandlePlayerMode()
    {
        if (!target || PlayerController.instance == null) return;

        Vector2 lookInput = PlayerController.instance.LookInput;
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
        // 位置在过渡动画结束后就已固定，此处无需更新
    }

    #region 公共接口
    /// <summary>
    /// 请求进入机关/固定视角模式
    /// </summary>
    /// <param name="viewTransform">相机要移动到的目标Transform</param>
    /// <param name="lookAtPlayer">在固定位置时是否持续朝向玩家</param>
    public void EnterMechanismMode(Transform viewTransform, bool lookAtPlayer = false)
    {
        if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
        
        mechanismTargetTransform = viewTransform;
        lookAtPlayerInMechanismMode = lookAtPlayer;

        transitionCoroutine = StartCoroutine(TransitionTo(viewTransform.position, viewTransform.rotation, CameraState.MechanismMode));
    }

    /// <summary>
    /// 请求返回玩家跟随模式
    /// </summary>
    public void EnterPlayerMode()
    {
        if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);

        // 计算返回玩家模式时的理想位置和旋转
        Quaternion targetRotation = Quaternion.Euler(pitch, yaw, 0);
        Vector3 targetPosition = extrapolatedPos - (targetRotation * Vector3.forward * distanceFromTarget);

        transitionCoroutine = StartCoroutine(TransitionTo(targetPosition, targetRotation, CameraState.PlayerMode));
    }

    #endregion

    private IEnumerator TransitionTo(Vector3 targetPosition, Quaternion targetRotation, CameraState stateAfterTransition)
    {
        currentState = CameraState.Transitioning;
        float time = 0;
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        while (time < transitionDuration)
        {
            float t = time / transitionDuration;
            // 使用 SmoothStep 实现缓启缓停效果
            t = Mathf.SmoothStep(0f, 1f, t);
            
            transform.position = Vector3.Lerp(startPos, targetPosition, t);
            transform.rotation = Quaternion.Slerp(startRot, targetRotation, t);
            
            time += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPosition;
        transform.rotation = targetRotation;
        currentState = stateAfterTransition;
        transitionCoroutine = null;
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