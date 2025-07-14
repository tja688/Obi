// MechanismBase.cs
using UnityEngine;
using System.Collections;
using UnityEngine.Serialization;

/// <summary>
/// 所有交互机关的抽象基类。
/// 【已重构】: 使用Unity标准触发器(OnTriggerEnter/Exit)和全局事件(EventCenter)来驱动。
/// </summary>
[RequireComponent(typeof(Collider))] // 【新增】确保对象上总有一个碰撞体
public abstract class MechanismBase : MonoBehaviour, IMechanism
{
    #region Inspector 可配置参数
    
    [FormerlySerializedAs("enableCameraMode")]
    [Header("摄像机设置")]
    [Tooltip("是否在机关激活时启用特殊的摄像机模式。")]
    public bool enablePlayerFollowMode;

    [Tooltip("摄像机在机关模式下的观察点位。若不指定，则不改变摄像机模式。")]
    public Transform cameraViewpoint;
    
    #endregion

    #region 状态机与内部变量

    protected enum MechanismState
    {
        Standby,    // 待激活：等待玩家交互
        Active,     // 激活：玩家正在操作机关
        Resetting   // 复位：正在执行复位逻辑
    }

    protected MechanismState currentState { get; private set; } = MechanismState.Standby;
    
    private Coroutine resetCoroutine;
    private bool isPlayerInside = false; // 【新增】用于追踪玩家是否在触发器内

    #endregion

    #region Unity 生命周期方法

    protected virtual void Awake()
    {
        // 确保触发器已设置
        Collider col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            Debug.LogWarning($"机关 '{gameObject.name}' 的碰撞体未设置为 'Is Trigger'，已自动设置。", this);
            col.isTrigger = true;
        }
    }

    protected virtual void Start()
    {
        // 【修改】订阅新的全局事件
        EventCenter.AddListener("PlayerInteracted", OnPlayerInteraction);
        EventCenter.AddListener("MechanismQuitRequested", OnQuitRequested);
        
        ExecuteEnterLogic(MechanismState.Standby);
    }

    protected virtual void OnDestroy()
    {
        // 【修改】注销新的全局事件
        EventCenter.RemoveListener("PlayerInteracted", OnPlayerInteraction);
        EventCenter.RemoveListener("MechanismQuitRequested", OnQuitRequested);

        // 如果对象销毁时玩家还在里面，确保交互提示被关闭
        if(isPlayerInside)
        {
            EventCenter.TriggerEvent("WaitingForInteraction", false);
        }
    }
    
    protected virtual void Update() { }

    #endregion

    #region 状态机核心逻辑

    protected void ChangeState(MechanismState newState)
    {
        if (currentState == newState) return;
        
        Debug.Log($"[Mechanism] 状态变更: {currentState} -> {newState}", gameObject);
        
        ExecuteExitLogic(currentState);
        currentState = newState;
        ExecuteEnterLogic(currentState);
    }

    private void ExecuteEnterLogic(MechanismState state)
    {
        switch (state)
        {
            case MechanismState.Standby: OnEnterStandby(); break;
            case MechanismState.Active: OnEnterActive(); break;
            case MechanismState.Resetting: OnEnterResetting(); break;
        }
    }
    
    private void ExecuteExitLogic(MechanismState state)
    {
        switch (state)
        {
            case MechanismState.Standby: OnExitStandby(); break;
            case MechanismState.Active: OnExitActive(); break;
        }
    }

    // --- 状态进入/退出时的具体逻辑 ---
    protected virtual void OnEnterStandby() 
    {
        // 【修改】进入待机状态时，如果玩家已经在触发器里，立即显示交互提示
        if (isPlayerInside)
        {
            EventCenter.TriggerEvent("WaitingForInteraction", true);
        }
    }

    protected virtual void OnExitStandby()
    {
        // 【修改】离开待机状态（通常是进入激活时），隐藏交互提示
        EventCenter.TriggerEvent("WaitingForInteraction", false);
    }

    protected virtual void OnEnterActive() 
    {
        MechanismController.instance.RegisterMechanism(this);
        if (cameraViewpoint != null) CameraManager.instance.EnterMechanismMode(cameraViewpoint, enablePlayerFollowMode);
    }
    
    protected virtual void OnExitActive() { }

    protected virtual void OnEnterResetting() 
    {
        MechanismController.instance.DeregisterMechanism(this);
        if (cameraViewpoint != null) CameraManager.instance.EnterPlayerMode();
        
        if(resetCoroutine != null) StopCoroutine(resetCoroutine);
        resetCoroutine = StartCoroutine(ResetSequence());
    }
    
    protected virtual IEnumerator ResetSequence()
    {
        yield return null;
        // 【修改】复位后，直接回到待激活状态。OnEnterStandby会处理后续逻辑。
        ChangeState(MechanismState.Standby); 
    }
    
    #endregion
    
    #region 事件与碰撞处理 (已重构)

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInside = true;
            // 如果当前是待机状态，则告诉UI可以显示交互提示了
            if (currentState == MechanismState.Standby)
            {
                Debug.Log("[Mechanism] 玩家进入触发器，发送 WaitingForInteraction(true) 事件。", gameObject);
                EventCenter.TriggerEvent("WaitingForInteraction", true);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInside = false;
            Debug.Log("[Mechanism] 玩家退出触发器。", gameObject);
            
            // 无论处于何种状态，玩家离开了就不能交互了，关闭提示
            EventCenter.TriggerEvent("WaitingForInteraction", false);

            // 如果机关是激活状态，玩家离开则自动复位
            if (currentState == MechanismState.Active)
            {
                Debug.Log("[Mechanism] 玩家在激活状态下离开，自动复位。", gameObject);
                ChangeState(MechanismState.Resetting);
            }
        }
    }

    /// <summary>
    /// 当接收到全局的玩家交互事件时调用
    /// </summary>
    private void OnPlayerInteraction()
    {
        // 只有在待机状态且玩家在触发器内时，交互才有效
        if (currentState == MechanismState.Standby && isPlayerInside)
        {
            Debug.Log("[Mechanism] 收到交互请求且条件满足，激活机关。", gameObject);
            ChangeState(MechanismState.Active);
        }
    }
    
    /// <summary>
    /// 当接收到全局的退出机关事件时调用
    /// </summary>
    private void OnQuitRequested()
    {
        OnQuit();
    }
    
    #endregion

    #region IMechanism 接口实现
    
    public GameObject mechanismGameObject => this.gameObject;
    public virtual void OnMouseMove(Vector2 position) { }
    public virtual void OnLeftButton(bool isPressed) { }
    public virtual void OnRightButton(bool isPressed) { }
    public virtual void OnMouseWheel(Vector2 scroll) { }
    
    /// <summary>
    /// 【修改】处理中途退出逻辑。现在由事件 OnQuitRequested 触发。
    /// </summary>
    public virtual void OnQuit() 
    {
        // 只有在激活状态下，Quit指令才有效
        if (currentState == MechanismState.Active)
        {
            Debug.Log("[Mechanism] 接收到Quit指令，强制进入复位状态。", gameObject);
            ChangeState(MechanismState.Resetting);
        }
    }
    
    #endregion
}