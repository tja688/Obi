// MechanismBase.cs
using UnityEngine;
using System.Collections;
using Obi;

/// <summary>
/// 所有交互机关的抽象基类。
/// 实现了状态机、玩家感知、激活和复位的基础逻辑。
/// </summary>
public abstract class MechanismBase : MonoBehaviour, IMechanism
{
    #region Inspector 可配置参数

    [Header("激活条件")]
    [Tooltip("机关的碰撞体，用于检测玩家距离和接触。若为空，则在启动时自动获取自身挂载的ObiCollider。")]
    public ObiColliderBase mechanismCollider;

    [Tooltip("玩家需要进入此距离范围内，机关才会从'待机'进入'待激活'状态。")]
    public float activationDistance = 5f;

    [Header("摄像机设置")]
    [Tooltip("是否在机关激活时启用特殊的摄像机模式。")]
    public bool enableCameraMode = true;

    [Tooltip("摄像机在机关模式下的观察点位。若不指定，则不改变摄像机模式。")]
    public Transform cameraViewpoint;
    
    #endregion

    #region 状态机与内部变量

    /// <summary>
    /// 机关的运行状态
    /// </summary>
    protected enum MechanismState
    {
        Idle,       // 待机：完全不活动，仅检测玩家距离
        Standby,    // 待激活：玩家已进入范围，等待物理接触
        Active,     // 激活：玩家已接触，机关正在运行
        Resetting   // 复位：玩家已离开，机关正在执行复位逻辑
    }

    protected MechanismState currentState { get; private set; } = MechanismState.Idle;

    // 玩家信息
    protected ObiActor playerActor;
    protected ObiSolver playerSolver;

    // 内部状态标志
    private bool isPlayerColliding = false;
    private Coroutine resetCoroutine;

    #endregion

    #region Unity 生命周期方法

    protected virtual void Awake()
    {
        // 1. 获取机关自身的碰撞体 (需求 #6)
        if (mechanismCollider == null)
        {
            mechanismCollider = GetComponent<ObiColliderBase>();
        }
    }

    protected virtual void Start()
    {
        // 2. 订阅玩家变换事件，以始终保持对当前玩家的引用 (需求 #4)
        EventCenter.AddListener<IControllable>("PlayerChange", OnPlayerChanged);

        // 3. 尝试获取初始玩家（如果游戏开始时已经存在）
        if (PlayerController.instance != null && PlayerController.instance.CurrentControlledObject != null)
        {
            OnPlayerChanged(PlayerController.instance.CurrentControlledObject);
        }
    }

    protected virtual void OnDestroy()
    {
        // 清理事件订阅
        EventCenter.RemoveListener<IControllable>("PlayerChange", OnPlayerChanged);
        
        // 确保在对象销毁时，如果还在监听碰撞，则取消监听
        if (playerSolver != null)
        {
            playerSolver.OnCollision -= HandlePlayerCollision;
        }
    }
    
    /// <summary>
    /// 主更新循环，根据当前状态执行不同逻辑
    /// </summary>
    protected virtual void Update()
    {
        // 如果没有玩家信息，则不执行任何逻辑
        if (playerActor == null) return;

        switch (currentState)
        {
            case MechanismState.Idle:
                // 待机状态下，只做一件事：检测玩家距离 (需求 #6)
                float distance = Vector3.Distance(transform.position, playerActor.transform.position);
                if (distance < activationDistance)
                {
                    ChangeState(MechanismState.Standby);
                }
                break;

            case MechanismState.Standby:
                // 待激活状态下，检测与玩家的碰撞，以及玩家是否离开范围
                if (isPlayerColliding)
                {
                    // 玩家进入碰撞体，激活机关 (需求 #8)
                    ChangeState(MechanismState.Active);
                }
                else if (Vector3.Distance(transform.position, playerActor.transform.position) > activationDistance)
                {
                    // 玩家离开了范围，返回待机
                    ChangeState(MechanismState.Idle);
                }
                break;

            case MechanismState.Active:
                // 激活状态下，检测玩家是否离开碰撞体
                if (!isPlayerColliding)
                {
                    // 玩家离开碰撞体，开始复位 (需求 #9)
                    ChangeState(MechanismState.Resetting);
                }
                break;

            case MechanismState.Resetting:
                // 复位状态的逻辑由协程处理，Update中无需操作
                break;
        }
    }

    #endregion

    #region 状态机核心逻辑

    /// <summary>
    /// 切换机关状态，并执行相应的进入/退出逻辑。
    /// </summary>
    protected void ChangeState(MechanismState newState)
    {
        if (currentState == newState) return;

        // 验证状态转换规则 (需求 #5)
        bool isValidTransition = (currentState == MechanismState.Idle && newState == MechanismState.Standby) ||
                                 (currentState == MechanismState.Standby && (newState == MechanismState.Active || newState == MechanismState.Idle)) ||
                                 (currentState == MechanismState.Active && newState == MechanismState.Resetting) ||
                                 (currentState == MechanismState.Resetting && newState == MechanismState.Idle);

        if (!isValidTransition)
        {
            Debug.LogWarning($"[Mechanism] 不允许的状态转换: 从 {currentState} 到 {newState}", gameObject);
            return;
        }

        Debug.Log($"[Mechanism] 状态变更: {currentState} -> {newState}", gameObject);
        currentState = newState;
        
        // 根据新状态执行进入逻辑
        switch (currentState)
        {
            case MechanismState.Idle:
                OnEnterIdle();
                break;
            case MechanismState.Standby:
                OnEnterStandby();
                break;
            case MechanismState.Active:
                OnEnterActive();
                break;
            case MechanismState.Resetting:
                OnEnterResetting();
                break;
        }
    }

    // 为子类提供可重写的状态进入方法 (需求 #5)
    protected virtual void OnEnterIdle() 
    {
        // 当从复位状态返回待机时，之前的碰撞订阅已经取消，这里无需操作
    }

    protected virtual void OnEnterStandby() 
    {
        // 进入待激活状态，开始监听玩家的碰撞事件 (需求 #7)
        if (playerSolver != null)
        {
            playerSolver.OnCollision += HandlePlayerCollision;
        }
    }
    protected virtual void OnEnterActive() 
    {
        // 激活机关，注册到控制器以接收输入 (需求 #2)
        MechanismController.instance.RegisterMechanism(this);

        // 处理摄像机模式 (需求 #10)
        if (cameraViewpoint != null)
        {
            CameraManager.instance.EnterMechanismMode(cameraViewpoint, enableCameraMode);
        }
    }
    protected virtual void OnEnterResetting() 
    {
        // 开始复位，注销输入，返还摄像机控制权
        MechanismController.instance.DeregisterMechanism(this);
        if (cameraViewpoint != null)
        {
            CameraManager.instance.EnterPlayerMode();
        }

        // 取消碰撞监听，因为我们不再关心碰撞状态，直到回到Standby (需求 #9 - 健壮性处理)
        if (playerSolver != null)
        {
            playerSolver.OnCollision -= HandlePlayerCollision;
        }
        
        // 启动复位协程
        if(resetCoroutine != null) StopCoroutine(resetCoroutine);
        resetCoroutine = StartCoroutine(ResetSequence());
    }
    
    /// <summary>
    /// 默认的复位序列。子类可以重写以实现复杂的复位动画或逻辑。(需求 #9)
    /// </summary>
    protected virtual IEnumerator ResetSequence()
    {
        // 默认情况下，复位是瞬间完成的
        yield return null;
        ChangeState(MechanismState.Idle);
    }
    
    #endregion
    
    #region 事件处理

    /// <summary>
    /// 当主玩家变更时，更新我们对玩家Actor和Solver的引用。
    /// </summary>
    private void OnPlayerChanged(IControllable newPlayer)
    {
        if (newPlayer == null) return;
        
        // 尝试从新玩家对象上获取ObiActor组件
        playerActor = newPlayer.controlledGameObject.GetComponent<ObiActor>();
        if (playerActor != null)
        {
            playerSolver = playerActor.solver;
            Debug.Log($"[Mechanism] 已锁定新玩家: {playerActor.name}, Solver: {playerSolver.name}", gameObject);
        }
        else
        {
            playerSolver = null;
            Debug.LogWarning($"[Mechanism] 新的被控对象 {newPlayer.controlledGameObject.name} 身上没有找到 ObiActor 组件!", gameObject);
        }
    }

    /// <summary>
    /// 处理玩家求解器的碰撞事件。
    /// </summary>
    private void HandlePlayerCollision(ObiSolver solver, ObiNativeContactList e)
    {
        // 采用健壮的布尔值逻辑，先假设未碰撞 (需求 #8)
        isPlayerColliding = false; 

        foreach (var contact in e)
        {
            if (contact.distance <= 0.01f) continue;
            
            // 使用工具类安全解析碰撞对 (需求 #7)
            if (ObiCollisionUtils.TryParseActorColliderPair(contact, solver, out var actor, out var collider))
            {
                // 检查是否是我们关心的玩家和机关碰撞体
                if (actor == playerActor && collider == mechanismCollider)
                {
                    isPlayerColliding = true;
                    break; // 找到一个接触点就足够了
                }
            }
        }
    }
    
    #endregion

    #region IMechanism 接口实现 (需求 #3)

    // 将接口实现为可供子类重写的虚方法
    public GameObject mechanismGameObject => this.gameObject;
    public virtual void OnMouseMove(Vector2 position) { }
    public virtual void OnLeftButton(bool isPressed) { }
    public virtual void OnRightButton(bool isPressed) { }
    public virtual void OnMouseWheel(Vector2 scroll) { }
    public virtual void OnQuit() { }
    
    #endregion
}