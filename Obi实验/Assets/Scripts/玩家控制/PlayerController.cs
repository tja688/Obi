// PlayerController.cs
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections;

[RequireComponent(typeof(PlayerInput))]
public class PlayerController : MonoBehaviour
{
    // ... (Singleton, Awake, Initialize, Start 等保持不变) ...
    #region Singleton & Persistence
    public static PlayerController instance { get; private set; }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            Initialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    #endregion

    // 【修改】增加新的状态
    public enum ControlState { Gameplay3D, Gameplay2D, Disabled, Grappled }

    [Header("玩家设置")]
    [Tooltip("在编辑器中预先指定一个默认玩家对象。如果为空，将在启动时自动查找。")]
    public GameObject defaultPlayerObject;

    private PlayerInput playerInput;
    private IControllable currentControlledObject;
    private ControlState currentState = ControlState.Gameplay3D;

    public Vector2 lookInput { get; private set; }
    public IControllable CurrentControlledObject => currentControlledObject;
    public ControlState CurrentState => currentState;

    private void Initialize()
    {
        playerInput = GetComponent<PlayerInput>();
    }

    private void Start()
    {
        SetupDefaultPlayer();
    }

    // ... (事件订阅 OnEnable/OnDisable 保持不变) ...
    #region 事件订阅与注销
    private void OnEnable()
    {
        playerInput.actions["Move"].performed += HandleMove;
        playerInput.actions["Move"].canceled += HandleMove;
        playerInput.actions["Move2D"].performed += HandleMove2D;
        playerInput.actions["Move2D"].canceled += HandleMove2D;
        playerInput.actions["Jump"].performed += HandleJump;
        playerInput.actions["Interact"].performed += HandleInteract;
        playerInput.actions["Look"].performed += HandleLook;
        playerInput.actions["Look"].canceled += HandleLook;
        playerInput.actions["Restart"].performed += HandleRestart;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        playerInput.actions["Move"].performed -= HandleMove;
        playerInput.actions["Move"].canceled -= HandleMove;
        playerInput.actions["Move2D"].performed -= HandleMove2D;
        playerInput.actions["Move2D"].canceled -= HandleMove2D;
        playerInput.actions["Jump"].performed -= HandleJump;
        playerInput.actions["Interact"].performed -= HandleInteract;
        playerInput.actions["Look"].performed -= HandleLook;
        playerInput.actions["Look"].canceled -= HandleLook;
        playerInput.actions["Restart"].performed -= HandleRestart;

        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    #endregion
    
    // ... (RegisterPlayer 保持不变) ...
    #region 公共方法
    public void RegisterPlayer(IControllable newPlayer)
    {
        if (newPlayer == currentControlledObject) return;
        currentControlledObject = null; 
        currentControlledObject = newPlayer;

        if (newPlayer != null)
        {
            Debug.Log($"[PlayerController] 新的玩家已注册: {newPlayer.controlledGameObject.name}");
            EventCenter.TriggerEvent<IControllable>("PlayerChange", currentControlledObject);
        }
        else
        {
            Debug.LogWarning("[PlayerController] 注册了一个空的玩家对象。");
        }
    }
    
    public void RequestStateChange(ControlState newState)
    {
        if (currentState == newState) return;
        Debug.Log($"[PlayerController] 状态改变: 从 {currentState} 到 {newState}");
        currentState = newState;
        lookInput = Vector2.zero;
        if (currentControlledObject != null)
        {
            // 在状态切换时清除移动输入，防止角色卡在移动状态
            currentControlledObject.Move(Vector2.zero); 
        }
    }

    /// <summary>
    /// 【新增】外部系统请求抓取当前玩家的公共接口。
    /// </summary>
    /// <param name="grabber">抓取者的Transform。</param>
    public void RequestGrab(Transform grabber)
    {
        if (currentControlledObject == null || currentState == ControlState.Grappled || grabber == null)
        {
            Debug.LogWarning("[PlayerController] 抓取请求被拒绝：玩家为空或已被抓取。");
            return;
        }

        // 1. 转换到抓取状态
        RequestStateChange(ControlState.Grappled);
        
        // 2. 通知当前控制对象执行被抓取的具体逻辑
        currentControlledObject.BeGrabbed(grabber);
    }

    /// <summary>
    /// 【新增】外部系统请求释放当前玩家的公共接口。
    /// </summary>
    public void RequestRelease()
    {
        if (currentControlledObject == null || currentState != ControlState.Grappled)
        {
            Debug.LogWarning("[PlayerController] 释放请求被拒绝：玩家为空或未被抓取。");
            return;
        }
        
        // 1. 通知当前控制对象执行被释放的具体逻辑
        currentControlledObject.BeReleased();
        
        // 2. 恢复到默认的3D游戏状态 (可以根据需要改为2D或其他)
        RequestStateChange(ControlState.Gameplay3D);
    }
    #endregion

    #region 输入处理
    private void HandleLook(InputAction.CallbackContext context)
    {
        // 只要不是完全禁用，视角就可以控制
        if (currentState == ControlState.Disabled)
        {
            lookInput = Vector2.zero;
            return;
        }
        lookInput = context.ReadValue<Vector2>();
    }
    
    private void HandleMove(InputAction.CallbackContext context)
    {
        // 当状态不是 Gameplay3D 时，此处的判断会直接返回，从而阻断移动输入
        if (currentControlledObject == null || currentState != ControlState.Gameplay3D) return;
        currentControlledObject.Move(context.ReadValue<Vector2>());
    }

    private void HandleMove2D(InputAction.CallbackContext context)
    {
        // 当状态不是 Gameplay2D 时，此处的判断会直接返回
        if (currentControlledObject == null || currentState != ControlState.Gameplay2D) return;
        currentControlledObject.Move(context.ReadValue<Vector2>());
    }

    private void HandleJump(InputAction.CallbackContext context)
    {
        if (currentControlledObject == null) return;
        // 此处的判断确保只有在 3D 或 2D 模式下才能跳跃，Grappled 状态下无法跳跃
        if (currentState == ControlState.Gameplay3D || currentState == ControlState.Gameplay2D)
        {
            currentControlledObject.Jump();
        }
    }

    private void HandleInteract(InputAction.CallbackContext context)
    {
        // 交互不受状态影响，始终可以触发
        Debug.Log("[PlayerController] 玩家尝试交互，发布 PlayerInteracted 事件。");
        EventCenter.TriggerEvent("PlayerInteracted");
    }

    private void HandleRestart(InputAction.CallbackContext context)
    {
        Debug.Log("[PlayerController] 玩家请求重启，发布 GameRestartRequested 事件。");
        EventCenter.TriggerEvent("GameRestartRequested");
    }
    #endregion
    
    // ... (场景加载逻辑 OnSceneLoaded, FindPlayerInNewSceneCoroutine, SetupDefaultPlayer 保持不变) ...
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StopAllCoroutines();
        StartCoroutine(FindPlayerInNewSceneCoroutine());
    }
    private IEnumerator FindPlayerInNewSceneCoroutine()
    {
        yield return null; 

        Debug.Log($"[PlayerController] 延迟一帧后，在新场景 '{SceneManager.GetActiveScene().name}' 中查找玩家...");
        SetupDefaultPlayer();
    }
    private void SetupDefaultPlayer()
    {
        IControllable target = null;
        if (defaultPlayerObject != null)
        {
            target = defaultPlayerObject.GetComponent<IControllable>();
        }

        if (target != null)
        {
            RegisterPlayer(target);
        }
        else
        {
            Debug.LogWarning("[PlayerController] 未在Inspector中配置默认玩家。正在场景中自动查找...");
            if (FindObjectOfType<MonoBehaviour>(true) is IControllable playerComponent)
            {
                RegisterPlayer(playerComponent);
            }
            else
            {
                Debug.LogError("[PlayerController] 严重警告: 即使延迟一帧后，在新加载的场景中仍然找不到任何实现了IControllable接口的对象！");
                RegisterPlayer(null);
            }
        }
    }
}