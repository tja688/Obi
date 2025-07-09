using UnityEngine;
using Obi;
using System;
using System.Collections; // 1. 引入命名空间以使用协程

/// <summary>
/// 一个通用的Obi碰撞触发器脚本。
/// 当一个特定的ObiActor与该对象上的ObiCollider发生碰撞时，会触发一个全局静态事件。
/// </summary>
[RequireComponent(typeof(ObiCollider))]
public class ObiCollisionTriggerTool : MonoBehaviour
{
    // --- 公开静态事件 ---
    public static event Action<ObiCollider, ObiActor> OnObiCollisionTriggered;

    [Header("目标设置 (可选)")]
    [Tooltip("要监听其碰撞事件的求解器。如果留空，将自动尝试获取玩家的Solver。")]
    [SerializeField] private ObiSolver targetSolver;

    [Tooltip("要检测其碰撞的目标Actor。如果留空，将自动尝试获取玩家的Actor。")]
    [SerializeField] private ObiActor targetActor;

    // --- 内部变量 ---
    private ObiCollider thisCollider;

    // 2. Start方法现在变得更简洁
    void Start()
    {
        // 获取本地组件的操作可以立即执行
        thisCollider = GetComponent<ObiCollider>();
        if (thisCollider == null)
        {
            Debug.LogError($"[ObiCollisionTrigger] 在对象 '{name}' 上找不到 ObiCollider 组件！脚本将不会运行。", this);
            enabled = false;
            return;
        }
        
        // 启动协程，将真正的初始化逻辑延迟一帧执行
        StartCoroutine(DelayedInitialization());
    }
    
    // 3. 新增的协程方法
    /// <summary>
    /// 延迟一帧执行初始化，以确保其他单例（如PlayerControl_Ball）已准备就绪。
    /// </summary>
    private IEnumerator DelayedInitialization()
    {
        // 等待下一帧的开始
        yield return null;

        // --- 以下是原Start方法中的逻辑 ---

        // 自动配置目标Solver和Actor (如果用户没有手动指定)
        if (targetSolver == null || targetActor == null)
        {
            Debug.Log($"[ObiCollisionTrigger] 在 '{name}' 上未指定目标，将自动查找玩家...", this);
            if (PlayerControl_Ball.instance != null)
            {
                // 使用单例来获取玩家的Solver和Actor
                targetSolver = PlayerControl_Ball.instance.playerSolver;
                targetActor = PlayerControl_Ball.instance.actor;
            }
        }
        
        // 最终验证并注册回调
        if (targetSolver != null && targetActor != null)
        {
            Debug.Log($"[ObiCollisionTrigger] 在 '{name}' 上配置成功。监听目标: Actor '{targetActor.name}'，监听器位于 Solver '{targetSolver.name}'。", this);
            // 注册碰撞事件回调
            targetSolver.OnCollision += HandleSolverCollision;
        }
        else
        {
            Debug.LogError($"[ObiCollisionTrigger] 在对象 '{name}' 上最终未能配置好Solver或Actor！脚本将不会运行。", this);
            enabled = false;
        }
    }

    private void OnDestroy()
    {
        // 在对象销毁时，务必取消注册，防止内存泄漏
        if (targetSolver != null)
        {
            targetSolver.OnCollision -= HandleSolverCollision;
        }
    }

    /// <summary>
    /// Obi Solver触发的原始碰撞处理函数。
    /// </summary>
    private void HandleSolverCollision(ObiSolver solver, ObiNativeContactList contacts)
    {
        // 使用我们之前创建的工具类来解析碰撞对
        for (int i = 0; i < contacts.count; ++i)
        {
            if (ObiCollisionUtils.TryParseActorColliderPair(contacts[i], solver, out ObiActor hitActor, out ObiColliderBase hitCollider))
            {
                // 检查解析出的Actor和Collider是否是我们关心的目标
                if (hitActor == targetActor && hitCollider == thisCollider)
                {
                    // 确认碰撞！触发全局静态事件
                    Debug.Log($"<color=lime>[ObiCollisionTrigger] 事件触发! Actor '{hitActor.name}' <--> Collider '{thisCollider.name}'</color>");
                    OnObiCollisionTriggered?.Invoke(thisCollider, targetActor);

                    // 通常，一次有效的碰撞触发一次事件就足够了，可以立即返回以提高性能
                    return;
                }
            }
        }
    }
}