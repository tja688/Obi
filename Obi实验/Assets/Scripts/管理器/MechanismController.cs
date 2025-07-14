// MechanismController.cs
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq; // 需要引入Linq来方便地创建副本

[RequireComponent(typeof(PlayerInput))]
public class MechanismController : MonoBehaviour
{
    #region Unchanged Region
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
    
    private readonly HashSet<IMechanism> activeMechanisms = new HashSet<IMechanism>();
    
    private const string ActionMapName = "Item"; 

    private void Initialize()
    {
        playerInput = GetComponent<PlayerInput>();
    }

    private void Start()
    {
        playerInput.actions.FindActionMap(ActionMapName).Disable();
    }

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

    public void RegisterMechanism(IMechanism mechanism)
    {
        if (mechanism == null) return;
        
        if (activeMechanisms.Count == 0)
        {
            ChangeState(MechanismState.Active);
        }
        
        if (activeMechanisms.Add(mechanism))
        {
             Debug.Log($"[MechanismController] 机关 '{mechanism.mechanismGameObject.name}' 已注册。当前激活机关数量: {activeMechanisms.Count}");
        }
    }

    public void DeregisterMechanism(IMechanism mechanism)
    {
        if (mechanism == null) return;

        if (activeMechanisms.Remove(mechanism))
        {
             Debug.Log($"[MechanismController] 机关 '{mechanism.mechanismGameObject.name}' 已注销。当前激活机关数量: {activeMechanisms.Count}");
        }

        if (activeMechanisms.Count == 0)
        {
            ChangeState(MechanismState.Inactive);
        }
    }

    private void ChangeState(MechanismState newState)
    {
        if (CurrentState == newState) return;
        
        CurrentState = newState;
        Debug.Log($"[MechanismController] 状态变更为: {newState}");

        if (CurrentState == MechanismState.Active)
        {
            playerInput.actions.FindActionMap(ActionMapName).Enable();
            Debug.Log($"[MechanismController] '{ActionMapName}' Action Map 已启用。");
        }
        else
        {
            playerInput.actions.FindActionMap(ActionMapName).Disable();
            Debug.Log($"[MechanismController] '{ActionMapName}' Action Map 已禁用。");
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

    private void HandleQuit(InputAction.CallbackContext context)
    {
        Debug.Log("[PlayerController] 玩家请求退出机关，发布 MechanismQuitRequested 事件。");
        EventCenter.TriggerEvent("MechanismQuitRequested");
    }
}