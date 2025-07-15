// PlayerController.cs (最终修正版)
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections; // 【新增】引入协程命名空间

[RequireComponent(typeof(PlayerInput))]
public class PlayerController : MonoBehaviour
{
    // ... (所有已有变量和 Awake, Initialize, Start 方法保持不变) ...
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

    public enum ControlState { Gameplay3D, Gameplay2D, Disabled }

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

    #region 事件订阅与注销
    private void OnEnable()
    {
        // ... (所有 playerInput.actions 的订阅保持不变) ...
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
        // ... (所有 playerInput.actions 的取消订阅保持不变) ...
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
            currentControlledObject.Move(Vector2.zero);
        }
    }
    #endregion

    #region 输入处理
    private void HandleLook(InputAction.CallbackContext context)
    {
        if (currentState == ControlState.Disabled)
        {
            lookInput = Vector2.zero;
            return;
        }
        lookInput = context.ReadValue<Vector2>();
    }
    
    private void HandleMove(InputAction.CallbackContext context)
    {
        if (currentControlledObject == null || currentState != ControlState.Gameplay3D) return;
        currentControlledObject.Move(context.ReadValue<Vector2>());
    }

    private void HandleMove2D(InputAction.CallbackContext context)
    {
        if (currentControlledObject == null || currentState != ControlState.Gameplay2D) return;
        currentControlledObject.Move(context.ReadValue<Vector2>());
    }

    private void HandleJump(InputAction.CallbackContext context)
    {
        if (currentControlledObject == null) return;
        if (currentState == ControlState.Gameplay3D || currentState == ControlState.Gameplay2D)
        {
            currentControlledObject.Jump();
        }
    }

    private void HandleInteract(InputAction.CallbackContext context)
    {
        Debug.Log("[PlayerController] 玩家尝试交互，发布 PlayerInteracted 事件。");
        EventCenter.TriggerEvent("PlayerInteracted");
    }

    private void HandleRestart(InputAction.CallbackContext context)
    {
        Debug.Log("[PlayerController] 玩家请求重启，发布 GameRestartRequested 事件。");
        EventCenter.TriggerEvent("GameRestartRequested");
    }
    #endregion

    /// <summary>
    /// 【修改】当场景加载后，启动一个协程来延迟执行玩家查找。
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 停止之前的协程，以防万一
        StopAllCoroutines();
        // 启动新协程
        StartCoroutine(FindPlayerInNewSceneCoroutine());
    }

    /// <summary>
    /// 【新增】延迟一帧查找玩家的协程。
    /// </summary>
    private IEnumerator FindPlayerInNewSceneCoroutine()
    {
        // 等待一帧，确保新场景中的所有对象都已完成初始化
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