// Filename: InputStateManager.cs
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;

public class InputStateManager : MonoBehaviour
{
    // --- 单例模式 ---
    public static InputStateManager Instance { get; private set; }

    [Header("核心配置")]
    [Tooltip("关联场景中的PlayerInput组件")]
    [SerializeField] private PlayerInput playerInput;

    [Tooltip("作为游戏默认基础操作的Action Map名称")]
    [SerializeField] private string defaultActionMap = "Player";

    // --- 状态栈 ---
    private readonly Stack<InputState> stateStack = new Stack<InputState>();

    private void Awake()
    {
        // 设置单例
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // 可选，如果你的管理器需要跨场景存在

        // 初始化状态栈
        InitializeDefaultState();
    }

    private void InitializeDefaultState()
    {
        // 创建一个代表默认状态的InputState对象，并压入栈底
        InputState baseState = new InputState
        {
            StateName = "Default",
            ActionMapsToEnable = new[] { defaultActionMap },
            DisableBaseInputs = false // 默认状态自己就是基础，所以这个无所谓
        };
        stateStack.Push(baseState);
        ApplyCurrentState();
    }

    /// <summary>
    /// 请求推入一个新的输入状态到栈顶
    /// </summary>
    /// <param name="newState">要推入的新状态</param>
    public void PushState(InputState newState)
    {
        if (newState == null) return;
        
        stateStack.Push(newState);
        Debug.Log($"[InputManager] Pushed State: {newState.StateName}. New depth: {stateStack.Count}");
        ApplyCurrentState();
    }

    /// <summary>
    /// 请求从栈顶弹出一个指定的状态
    /// </summary>
    /// <param name="stateToPop">期望弹出的状态</param>
    public void PopState(InputState stateToPop)
    {
        if (stateStack.Count <= 1)
        {
            Debug.LogWarning("[InputManager] Cannot pop the default state.");
            return;
        }

        // 健壮性检查：只在栈顶是期望的状态时才弹出
        if (stateStack.Peek() == stateToPop)
        {
            var poppedState = stateStack.Pop();
            Debug.Log($"[InputManager] Popped State: {poppedState.StateName}. New depth: {stateStack.Count}");
            ApplyCurrentState();
        }
        else
        {
            Debug.LogWarning($"[InputManager] Mismatched pop request! Tried to pop '{stateToPop.StateName}' but the top state is '{stateStack.Peek().StateName}'. Aborting pop.");
        }
    }

    /// <summary>
    /// 应用当前栈顶状态的输入配置
    /// </summary>
    private void ApplyCurrentState()
    {
        if (playerInput == null)
        {
            Debug.LogError("[InputManager] PlayerInput component is not assigned!");
            return;
        }

        InputState currentState = stateStack.Peek();
        
        // 1. 先禁用所有Action Maps，确保一个干净的状态
        foreach (var map in playerInput.actions.actionMaps)
        {
            map.Disable();
        }

        // 2. 如果当前状态不禁用基础输入，则激活默认的Action Map
        if (!currentState.DisableBaseInputs)
        {
            playerInput.actions.FindActionMap(defaultActionMap)?.Enable();
        }
        
        // 3. 激活当前状态要求的所有Action Maps
        if (currentState.ActionMapsToEnable != null)
        {
            foreach (var mapName in currentState.ActionMapsToEnable)
            {
                var mapToEnable = playerInput.actions.FindActionMap(mapName);
                if (mapToEnable != null)
                {
                    mapToEnable.Enable();
                }
                else
                {
                    Debug.LogWarning($"[InputManager] Action Map '{mapName}' not found in PlayerInput asset.");
                }
            }
        }
    }
}