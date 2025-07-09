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

    public static PlayerInputManager instance { get; private set; }

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

    #endregion

    #region Public Events

    public event Action<Vector2> OnOnMove;

    public event Action<Vector2> OnOnMouseMove;

    public event Action OnOnJump;

    public event Action OnOnInteract;
    
    public event Action OnOnReStart;

    // Q键按下事件
    public event Action OnOnQPressed;

    // 鼠标左键点击事件
    public event Action OnOnMouseLeftClick;

    // 鼠标右键点击事件
    public event Action OnOnMouseRightClick;

    #endregion

    private bool isInputEnabled = true;

    /// <summary>
    /// 启用输入处理
    /// </summary>
    public void EnableInput()
    {
        isInputEnabled = true;
    }

    /// <summary>
    /// 禁用输入处理
    /// </summary>
    public void DisableInput()
    {
        isInputEnabled = false;
    }

    private void Update()
    {
        if (!isInputEnabled)
        {
            OnOnMove?.Invoke(Vector2.zero);
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
        var moveX = Input.GetAxis("Horizontal"); // A/D, 左/右箭头
        var moveY = Input.GetAxis("Vertical");   // W/S, 上/下箭头
        var moveInput = new Vector2(moveX, moveY);
        
        OnOnMove?.Invoke(moveInput);
    }

    /// <summary>
    /// 处理鼠标移动输入
    /// </summary>
    private void HandleMouseMovementInput()
    {
        var mouseX = Input.GetAxis("Mouse X");
        var mouseY = Input.GetAxis("Mouse Y");
        var mouseDelta = new Vector2(mouseX, mouseY);
        
        if (mouseDelta.sqrMagnitude > 0)
        {
            OnOnMouseMove?.Invoke(mouseDelta);
        }
    }

    /// <summary>
    /// 处理所有按键动作输入
    /// </summary>
    private void HandleActionInput()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            OnOnJump?.Invoke();
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            OnOnInteract?.Invoke();
        }

        // Q键
        if (Input.GetKeyDown(KeyCode.Q))
        {
            OnOnQPressed?.Invoke();
        }

        if (Input.GetMouseButtonDown(0))
        {
            OnOnMouseLeftClick?.Invoke();
        }

        if (Input.GetMouseButtonDown(1))
        {
            OnOnMouseRightClick?.Invoke();
        }
        
        if (Input.GetKeyDown(KeyCode.R))
        {
            OnOnReStart?.Invoke();
        }
    }


}