using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using Obi;

/// <summary>
/// 负责角色的死亡与重生逻辑。
/// 该脚本经过优化，可以自动查找并设置所需组件和重生点。
/// </summary>
public class RebirthAndDeath : MonoBehaviour
{
    // 保留了需要手动在Inspector中指定的碰撞体
    [Header("碰撞体设置")]
    public ObiCollider deathPitCollider;
    public ObiCollider finishCollider;

    // 保留了可供策划配置的UnityEvent
    [Header("事件回调")]
    public UnityEvent onDeath = new UnityEvent();
    public UnityEvent onFinish = new UnityEvent();
    public UnityEvent onRestart = new UnityEvent();

    // 私有字段，用于存储动态查找到的组件
    private ObiSolver solver;
    private ObiSoftbody softbody;
    private Camera mainCamera;
    private CameraFollow cameraFollow;

    // 用于存储初始重生位置和旋转
    private Vector3 playerSpawnPosition;
    private Quaternion playerSpawnRotation;
    private Vector3 cameraSpawnPosition;
    private Quaternion cameraSpawnRotation;
    
    // 状态标志
    private bool isInitialized = false;
    private bool restartRequested = false;

    /// <summary>
    /// 使用协程延迟一帧执行初始化，以确保所有其他对象的 Awake 和 Start 已完成。
    /// </summary>
    private void Start()
    {
        StartCoroutine(DelayedInitialization());
    }

    /// <summary>
    /// 延迟一帧后执行的初始化方法。
    /// 自动查找组件、设置重生点并订阅事件。
    /// </summary>
    private IEnumerator DelayedInitialization()
    {
        // 等待一帧，确保PlayerInputManager.instance可用
        yield return null;

        // --- 1. 自动查找组件 ---
        if (PlayerInputManager.instance == null)
        {
            Debug.LogError("场景中未找到 PlayerInputManager 实例！", this);
            yield break; // 中断协程
        }

        // 从PlayerInputManager的子对象中查找Solver和Softbody
        solver = PlayerInputManager.instance.GetComponentInParent<ObiSolver>();
        softbody = PlayerInputManager.instance.GetComponentInChildren<ObiSoftbody>();
        mainCamera = Camera.main; // 查找场景中的主摄像机

        // --- 验证查找到的组件 ---
        if (solver == null)
        {
            Debug.LogError("在 PlayerInputManager 的子对象中未找到 ObiSolver！", this);
            yield break;
        }
        if (softbody == null)
        {
            Debug.LogError("在 PlayerInputManager 的子对象中未找到 ObiSoftbody！", this);
            yield break;
        }
        if (mainCamera == null)
        {
            Debug.LogError("场景中未找到带有'MainCamera'标签的摄像机！", this);
            yield break;
        }

        cameraFollow = mainCamera.GetComponent<CameraFollow>();
        if (cameraFollow == null)
        {
            Debug.LogError("主摄像机上未找到 CameraFollow 组件！", this);
            yield break;
        }
        
        // --- 2. 获取初始位置作为重生点 ---
        playerSpawnPosition = softbody.transform.position;
        playerSpawnRotation = softbody.transform.rotation;
        cameraSpawnPosition = mainCamera.transform.position;
        cameraSpawnRotation = mainCamera.transform.rotation;
        
        Debug.Log("重生点设置成功！玩家初始位置：" + playerSpawnPosition);

        // --- 3. 订阅事件 ---
        // 将事件订阅移至此处，确保solver不为null
        solver.OnCollision += Solver_OnCollision;
        solver.OnSimulationStart += Solver_OnSimulationStart;
        PlayerInputManager.instance.OnOnReStart += RequestRestart;
        
        isInitialized = true; // 标记初始化完成
    }
    
    private void OnDestroy()
    {
        // 在销毁时取消订阅，防止内存泄漏
        if (PlayerInputManager.instance != null)
        {
            PlayerInputManager.instance.OnOnReStart -= RequestRestart;
        }
        
        // 如果初始化已完成，也取消订阅solver事件
        if (isInitialized && solver != null)
        {
            solver.OnCollision -= Solver_OnCollision;
            solver.OnSimulationStart -= Solver_OnSimulationStart;
        }
    }

    /// <summary>
    /// 接收来自输入管理器的重启请求。
    /// </summary>
    private void RequestRestart()
    {
        // 只有初始化完成后才接受重启请求
        if (isInitialized)
        {
            restartRequested = true;
        }
    }

    /// <summary>
    /// 在物理模拟开始前检查是否需要执行传送。
    /// </summary>
    private void Solver_OnSimulationStart(ObiSolver s, float timeToSimulate, float substepTime)
    {
        if (!isInitialized || !softbody) return;

        // 仅在请求重启后才执行传送逻辑
        if (restartRequested)
        {
            // 传送玩家和摄像机到记录的重生点
            softbody.Teleport(playerSpawnPosition, playerSpawnRotation);
            cameraFollow.Teleport(cameraSpawnPosition, cameraSpawnRotation);

            restartRequested = false; // 重置请求标志
            onRestart.Invoke(); // 触发重启事件
        }
    }

    /// <summary>
    /// 处理与死亡区域和终点的碰撞。
    /// </summary>
    private void Solver_OnCollision(ObiSolver s, ObiNativeContactList e)
    {
        if (!isInitialized) return;
        
        var world = ObiColliderWorld.GetInstance();
        foreach (var contact in e)
        {
            if (!(contact.distance > 0.01)) continue;

            var col = world.colliderHandles[contact.bodyB].owner;

            if (col == deathPitCollider)
            {
                onDeath.Invoke();
                return; // 找到死亡区域后立即返回
            }

            if (col == finishCollider)
            {
                onFinish.Invoke();
                return; // 到达终点后立即返回
            }
        }
    }
}