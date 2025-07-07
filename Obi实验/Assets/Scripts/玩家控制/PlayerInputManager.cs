using UnityEngine;
using System;

/// <summary>
/// 玩家输入处理中心（使用旧版输入系统）
/// 这是一个单例，负责监听所有玩家输入，并提供事件供其他脚本订阅。
/// 还提供了启用和禁用输入的方法，方便在UI界面或过场动画中控制。
/// 
/// 使用方法:
/// 1. 将此脚本挂载到场景中一个持久化的GameObject上（例如 "Managers"）。
/// 2. 在其他需要监听输入的脚本中，通过 PlayerInputManager.Instance 访问单例。
/// 3. 订阅需要的事件，例如：PlayerInputManager.Instance.onJump += YourJumpMethod;
/// 4. 在不再需要时（例如OnDestroy），取消订阅：PlayerInputManager.Instance.onJump -= YourJumpMethod;
/// </summary>
public class PlayerInputManager : MonoBehaviour
{
    #region Singleton

    public static PlayerInputManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // DontDestroyOnLoad(gameObject); // 如果需要跨场景保持，请取消此行注释
        }
        else
        {
            Destroy(gameObject);
        }
    }

    #endregion

    #region Public Events

    // 移动输入事件 (Vector2: x为水平轴, y为垂直轴)
    public event Action<Vector2> onMove;

    // 鼠标移动事件 (Vector2: 鼠标移动的增量)
    public event Action<Vector2> onMouseMove;

    // 跳跃事件 (空格键)
    public event Action onJump;

    // 交互事件 (E键)
    public event Action onInteract;

    // Q键按下事件
    public event Action onQPressed;

    // 鼠标左键点击事件
    public event Action onMouseLeftClick;

    // 鼠标右键点击事件
    public event Action onMouseRightClick;

    #endregion

    private bool _isInputEnabled = true;

    /// <summary>
    /// 启用输入处理
    /// </summary>
    public void EnableInput()
    {
        _isInputEnabled = true;
    }

    /// <summary>
    /// 禁用输入处理
    /// </summary>
    public void DisableInput()
    {
        _isInputEnabled = false;
    }

    private void Update()
    {
        if (!_isInputEnabled)
        {
            return;
        }

        HandleMovementInput();
        HandleMouseMovementInput();
        HandleActionInput();
    }

    /// <summary>
    /// 处理移动相关的输入
    /// </summary>
    private void HandleMovementInput()
    {
        // WASD 移动
        float moveX = Input.GetAxis("Horizontal"); // A/D, 左/右箭头
        float moveY = Input.GetAxis("Vertical");   // W/S, 上/下箭头

        Vector2 moveInput = new Vector2(moveX, moveY);
        // 只有在有实际输入时才触发事件
        if (moveInput.sqrMagnitude > 0.01f)
        {
            onMove?.Invoke(moveInput);
        }
    }

    /// <summary>
    /// 处理鼠标移动输入
    /// </summary>
    private void HandleMouseMovementInput()
    {
        // 鼠标移动
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        Vector2 mouseDelta = new Vector2(mouseX, mouseY);
        // 只有在鼠标有移动时才触发事件
        if (mouseDelta.sqrMagnitude > 0.01f)
        {
            onMouseMove?.Invoke(mouseDelta);
        }
    }

    /// <summary>
    /// 处理所有按键动作输入
    /// </summary>
    private void HandleActionInput()
    {
        // 跳跃 (空格)
        if (Input.GetKeyDown(KeyCode.Space))
        {
            onJump?.Invoke();
        }

        // 交互 (E)
        if (Input.GetKeyDown(KeyCode.E))
        {
            onInteract?.Invoke();
        }

        // Q键
        if (Input.GetKeyDown(KeyCode.Q))
        {
            onQPressed?.Invoke();
        }

        // 鼠标左键点击 (0代表左键)
        if (Input.GetMouseButtonDown(0))
        {
            onMouseLeftClick?.Invoke();
        }

        // 鼠标右键点击 (1代表右键)
        if (Input.GetMouseButtonDown(1))
        {
            onMouseRightClick?.Invoke();
        }
    }
}