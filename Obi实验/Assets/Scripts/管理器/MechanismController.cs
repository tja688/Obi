// MechanismController.cs
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic; // 使用HashSet需要此命名空间

[RequireComponent(typeof(PlayerInput))]
public class MechanismController : MonoBehaviour
{
    #region Singleton
    public static MechanismController instance { get; private set; }

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

    public enum MechanismState { Active, Inactive }
    public MechanismState CurrentState { get; private set; } = MechanismState.Inactive;

    private PlayerInput playerInput;
    
    // 使用 HashSet 来跟踪所有当前激活的机关，它能自动处理重复并且提供高效的增删查操作。
    private readonly HashSet<IMechanism> activeMechanisms = new HashSet<IMechanism>();
    
    private const string ActionMapName = "Item"; // 将Action Map的名称定义为常量，便于维护

    private void Initialize()
    {
        playerInput = GetComponent<PlayerInput>();
    }

    private void Start()
    {
        // 确保游戏开始时，机关的Action Map是禁用的
        playerInput.actions.FindActionMap(ActionMapName).Disable();
    }

    #region 事件订阅与注销
    private void OnEnable()
    {
        if (playerInput == null) return;
        
        playerInput.actions["Quit"].performed += HandleQuit;
        playerInput.actions["MouseMove"].performed += HandleMouseMove;
        playerInput.actions["LeftButton"].performed += HandleLeftButton;
        playerInput.actions["LeftButton"].canceled += HandleLeftButton;
        playerInput.actions["RightButton"].performed += HandleRightButton;
        playerInput.actions["RightButton"].canceled += HandleRightButton;
        playerInput.actions["MouseWheel"].performed += HandleMouseWheel;
    }

    private void OnDisable()
    {
        if (playerInput == null) return;
        
        playerInput.actions["Quit"].performed -= HandleQuit;
        playerInput.actions["MouseMove"].performed -= HandleMouseMove;
        playerInput.actions["LeftButton"].performed -= HandleLeftButton;
        playerInput.actions["LeftButton"].canceled -= HandleLeftButton;
        playerInput.actions["RightButton"].performed -= HandleRightButton;
        playerInput.actions["RightButton"].canceled -= HandleRightButton;
        playerInput.actions["MouseWheel"].performed -= HandleMouseWheel;
    }
    #endregion

    #region 公共接口 (供机关调用)
    
    /// <summary>
    /// 注册一个机关，使其开始接收输入。
    /// </summary>
    /// <param name="mechanism">要注册的机关实例。</param>
    public void RegisterMechanism(IMechanism mechanism)
    {
        if (mechanism == null) return;
        
        // 如果当前没有任何已激活的机关，说明这是第一个，我们需要激活状态和输入。
        if (activeMechanisms.Count == 0)
        {
            ChangeState(MechanismState.Active);
        }
        
        // 将新机关添加到激活集合中
        if (activeMechanisms.Add(mechanism))
        {
             Debug.Log($"[MechanismController] 机关 '{mechanism.mechanismGameObject.name}' 已注册。当前激活机关数量: {activeMechanisms.Count}");
        }
    }

    /// <summary>
    /// 注销一个机关，使其停止接收输入。
    /// </summary>
    /// <param name="mechanism">要注销的机关实例。</param>
    public void DeregisterMechanism(IMechanism mechanism)
    {
        if (mechanism == null) return;

        // 从激活集合中移除该机关
        if (activeMechanisms.Remove(mechanism))
        {
             Debug.Log($"[MechanismController] 机关 '{mechanism.mechanismGameObject.name}' 已注销。当前激活机关数量: {activeMechanisms.Count}");
        }

        // 如果移除后，集合为空，说明最后一个机关已关闭，我们需要禁用状态和输入。
        if (activeMechanisms.Count == 0)
        {
            ChangeState(MechanismState.Inactive);
        }
    }

    #endregion

    #region 内部状态管理
    private void ChangeState(MechanismState newState)
    {
        if (CurrentState == newState) return;
        
        CurrentState = newState;
        Debug.Log($"[MechanismController] 状态变更为: {newState}");

        if (CurrentState == MechanismState.Active)
        {
            // 激活 "Item" Action Map，使其与玩家默认的Action Map同时生效
            playerInput.actions.FindActionMap(ActionMapName).Enable();
            Debug.Log($"[MechanismController] '{ActionMapName}' Action Map 已启用。");
        }
        else
        {
            // 禁用 "Item" Action Map
            playerInput.actions.FindActionMap(ActionMapName).Disable();
            Debug.Log($"[MechanismController] '{ActionMapName}' Action Map 已禁用。");
        }
    }
    #endregion
    
    #region 输入处理与分发
    
    // 在每个处理函数中，我们遍历所有已激活的机关，并将输入事件分发给它们。
    
    private void HandleQuit(InputAction.CallbackContext context)
    {
        if (CurrentState == MechanismState.Inactive) return;
        Debug.Log("Quit action triggered.");
        foreach (var mechanism in activeMechanisms)
        {
            mechanism.OnQuit();
        }
    }

    private void HandleMouseMove(InputAction.CallbackContext context)
    {
        if (CurrentState == MechanismState.Inactive) return;
        foreach (var mechanism in activeMechanisms)
        {
            mechanism.OnMouseMove(context.ReadValue<Vector2>());
        }
    }

    private void HandleLeftButton(InputAction.CallbackContext context)
    {
        if (CurrentState == MechanismState.Inactive) return;
        bool isPressed = context.ReadValueAsButton();
        foreach (var mechanism in activeMechanisms)
        {
            mechanism.OnLeftButton(isPressed);
        }
    }

    private void HandleRightButton(InputAction.CallbackContext context)
    {
        if (CurrentState == MechanismState.Inactive) return;
        bool isPressed = context.ReadValueAsButton();
        foreach (var mechanism in activeMechanisms)
        {
            mechanism.OnRightButton(isPressed);
        }
    }

    private void HandleMouseWheel(InputAction.CallbackContext context)
    {
        if (CurrentState == MechanismState.Inactive) return;
        foreach (var mechanism in activeMechanisms)
        {
            mechanism.OnMouseWheel(context.ReadValue<Vector2>());
        }
    }
    
    #endregion
}