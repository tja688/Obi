// PlayerController.cs
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class PlayerController : MonoBehaviour
{
    #region Singleton & Persistence
    public static PlayerController instance { get; private set; }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
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
        if (playerInput == null) return;
        playerInput.actions["Move"].performed += HandleMove;
        playerInput.actions["Move"].canceled += HandleMove;
        playerInput.actions["Move2D"].performed += HandleMove2D;
        playerInput.actions["Move2D"].canceled += HandleMove2D;
        playerInput.actions["Jump"].performed += HandleJump;
        playerInput.actions["Interact"].performed += HandleInteract;
        playerInput.actions["Look"].performed += HandleLook;
        playerInput.actions["Look"].canceled += HandleLook;
    }

    private void OnDisable()
    {
        if (playerInput == null) return;
        playerInput.actions["Move"].performed -= HandleMove;
        playerInput.actions["Move"].canceled -= HandleMove;
        playerInput.actions["Move2D"].performed -= HandleMove2D;
        playerInput.actions["Move2D"].canceled -= HandleMove2D;
        playerInput.actions["Jump"].performed -= HandleJump;
        playerInput.actions["Interact"].performed -= HandleInteract;
        playerInput.actions["Look"].performed -= HandleLook;
        playerInput.actions["Look"].canceled -= HandleLook;
    }
    #endregion

    #region 公共方法
    public void RegisterPlayer(IControllable newPlayer)
    {
        if (newPlayer == currentControlledObject) return;
        currentControlledObject = newPlayer;

        // 【已修正】使用新的属性名 ControlledGameObject，避免堆栈溢出
        Debug.Log($"[PlayerController] 新的玩家已注册: {newPlayer.controlledGameObject.name}");
        EventCenter.TriggerEvent<IControllable>("PlayerChange", currentControlledObject);
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
        // 【修改】不再直接调用机关逻辑，而是发布一个全局事件
        Debug.Log("[PlayerController] 玩家尝试交互，发布 PlayerInteracted 事件。");
        EventCenter.TriggerEvent("PlayerInteracted");
    }

    #endregion

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
            target = FindObjectOfType<MonoBehaviour>(true) as IControllable;
            if (target != null)
            {
                RegisterPlayer(target);
            }
            else
            {
                Debug.LogError("[PlayerController] 严重警告: 场景中找不到任何实现了IControllable接口的对象！控制器将不会有任何作用。");
            }
        }
    }
}