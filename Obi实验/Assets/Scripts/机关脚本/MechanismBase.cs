// MechanismBase.cs
using UnityEngine;
using System.Collections;
using Obi;

/// <summary>
/// 所有交互机关的抽象基类。
/// 实现了基于【起点(激活触发器)】和【终点(退出触发器)】的纯事件驱动状态机。
/// </summary>
public abstract class MechanismBase : MonoBehaviour, IMechanism
{
    #region Inspector 可配置参数

    [Header("核心触发器设置")]
    [Tooltip("【起点】触发器：玩家与此碰撞体接触后，机关将进入'激活'状态。")]
    public ObiColliderBase activationCollider;

    [Tooltip("【终点】触发器：机关在'激活'状态下，玩家与此碰撞体接触后，将进入'复位'状态。")]
    public ObiColliderBase deactivationCollider;

    [Header("摄像机设置")]
    [Tooltip("是否在机关激活时启用特殊的摄像机模式。")]
    public bool enableCameraMode;

    [Tooltip("摄像机在机关模式下的观察点位。若不指定，则不改变摄像机模式。")]
    public Transform cameraViewpoint;
    
    #endregion

    #region 状态机与内部变量

    protected enum MechanismState
    {
        Standby,    // 待激活：等待玩家触碰【起点】
        Active,     // 激活：等待玩家触碰【终点】或接收到Quit指令
        Resetting   // 复位：正在执行复位逻辑
    }

    protected MechanismState currentState { get; private set; } = MechanismState.Standby;
    
    protected ObiActor playerActor;
    protected ObiSolver playerSolver;

    private Coroutine resetCoroutine;

    #endregion

    #region Unity 生命周期方法

    protected virtual void Awake()
    {
        // 自动获取碰撞体（如果用户未在Inspector中指定）
        if (activationCollider == null) activationCollider = GetComponent<ObiColliderBase>();
        if (deactivationCollider == null) deactivationCollider = GetComponent<ObiColliderBase>();
    }

    protected virtual void Start()
    {
        EventCenter.AddListener<IControllable>("PlayerChange", OnPlayerChanged);
        if (PlayerController.instance != null && PlayerController.instance.CurrentControlledObject != null)
            OnPlayerChanged(PlayerController.instance.CurrentControlledObject);
        
        // 游戏开始时，直接进入待激活状态，并启动“起点”触发器的监听
        ExecuteEnterLogic(MechanismState.Standby);
    }

    protected virtual void OnDestroy()
    {
        EventCenter.RemoveListener<IControllable>("PlayerChange", OnPlayerChanged);
        if (playerSolver != null)
        {
            playerSolver.OnCollision -= HandleActivationTrigger;
            playerSolver.OnCollision -= HandleDeactivationTrigger;
        }
    }
    
    // Update 方法现在是空的，因为所有状态转换都由事件驱动
    protected virtual void Update() { }

    #endregion

    #region 状态机核心逻辑

    protected void ChangeState(MechanismState newState)
    {
        if (currentState == newState) return;
        
        // 状态转换规则: Standby -> Active, Active -> Resetting, Resetting -> Standby
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

    // 状态进入/退出时的逻辑
    protected virtual void OnEnterStandby() 
    {
        // 启动“起点触发器”监听
        if (playerSolver != null) playerSolver.OnCollision += HandleActivationTrigger;
    }
    protected virtual void OnExitStandby()
    {
        // 关闭“起点触发器”监听
        if (playerSolver != null) playerSolver.OnCollision -= HandleActivationTrigger;
    }

    protected virtual void OnEnterActive() 
    {
        MechanismController.instance.RegisterMechanism(this);
        if (cameraViewpoint != null) CameraManager.instance.EnterMechanismMode(cameraViewpoint, enableCameraMode);
        
        // 启动“终点触发器”监听
        if (playerSolver != null) playerSolver.OnCollision += HandleDeactivationTrigger;
    }
    
    protected virtual void OnExitActive()
    {
        // 关闭“终点触发器”监听
        if (playerSolver != null) playerSolver.OnCollision -= HandleDeactivationTrigger;
    }

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
        ChangeState(MechanismState.Standby); // 复位后，回到待激活状态，准备下一次触发
    }
    
    #endregion
    
    #region 事件与碰撞处理

    private void OnPlayerChanged(IControllable newPlayer)
    {
        // 如果之前有监听，先注销旧的
        if (playerSolver != null)
        {
            playerSolver.OnCollision -= HandleActivationTrigger;
            playerSolver.OnCollision -= HandleDeactivationTrigger;
        }

        if (newPlayer == null) return;
        
        playerActor = newPlayer.controlledGameObject.GetComponent<ObiActor>();
        if (playerActor != null) 
        {
            playerSolver = playerActor.solver;
            // 根据当前状态，重新订阅事件
            ExecuteEnterLogic(currentState); 
        }
        else 
        {
            playerSolver = null;
        }
    }

    /// <summary>
    /// 【起点触发器】: 检测到玩家触碰 activationCollider 后，触发一次并由状态机注销。
    /// </summary>
    private void HandleActivationTrigger(ObiSolver solver, ObiNativeContactList e)
    {
        foreach (var contact in e)
        {
            if (contact.distance > 0.01f && 
                ObiCollisionUtils.TryParseActorColliderPair(contact, solver, out var actor, out var collider) &&
                actor == playerActor && collider == activationCollider)
            {
                ChangeState(MechanismState.Active);
                return; // 找到即触发，无需继续遍历
            }
        }
    }
    
    /// <summary>
    /// 【终点触发器】: 检测到玩家触碰 deactivationCollider 后，触发一次并由状态机注销。
    /// </summary>
    private void HandleDeactivationTrigger(ObiSolver solver, ObiNativeContactList e)
    {
        foreach (var contact in e)
        {
            if (contact.distance > 0.01f && 
                ObiCollisionUtils.TryParseActorColliderPair(contact, solver, out var actor, out var collider) &&
                actor == playerActor && collider == deactivationCollider)
            {
                ChangeState(MechanismState.Resetting);
                return; // 找到即触发，无需继续遍历
            }
        }
    }
    
    #endregion

    #region IMechanism 接口实现
    
    public GameObject mechanismGameObject => this.gameObject;
    public virtual void OnMouseMove(Vector2 position) { }
    public virtual void OnLeftButton(bool isPressed) { }
    public virtual void OnRightButton(bool isPressed) { }
    public virtual void OnMouseWheel(Vector2 scroll) { }

    /// <summary>
    /// 【新增】处理中途退出逻辑。
    /// </summary>
    public virtual void OnQuit() 
    {
        // 只有在激活状态下，Quit指令才有效
        if (currentState == MechanismState.Active)
        {
            Debug.Log("[Mechanism] 接收到Quit指令，强制进入复位状态。");
            ChangeState(MechanismState.Resetting);
        }
    }
    
    #endregion
}