// CameraManager.cs
using UnityEngine;
using System.Collections;

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

    private Vector3 lastPosition;
    private Vector3 extrapolatedPos;
    private float yaw, pitch;
    private Coroutine transitionCoroutine;
    private Transform mechanismTargetTransform;
    private bool lookAtPlayerInMechanismMode = false;

    private void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
        
        EventCenter.AddListener<IControllable>("PlayerChange", OnPlayerChanged);

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
    }
    
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

    #region 公共接口
    public void EnterMechanismMode(Transform viewTransform, bool lookAtPlayer = false)
    {
        if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
        mechanismTargetTransform = viewTransform;
        lookAtPlayerInMechanismMode = lookAtPlayer;
        transitionCoroutine = StartCoroutine(TransitionTo(viewTransform.position, viewTransform.rotation, CameraState.MechanismMode));
    }

    public void EnterPlayerMode()
    {
        if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
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
            float t = Mathf.SmoothStep(0f, 1f, time / transitionDuration);
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