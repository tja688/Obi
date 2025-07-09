using UnityEngine;
using System;
using System.Collections.Generic;
using Obi;

/// <summary>
/// Obi 碰撞管理中心 (单例)
/// 负责集中处理所有Obi对象的碰撞注册与回调分发。
/// [已根据Obi源码修复API调用错误]
/// </summary>
public class ObiCollisionManager : MonoBehaviour
{
    // --- 单例实现 ---
    public static ObiCollisionManager Instance { get; private set; }

    // --- 内部数据结构 ---

    /// <summary>
    /// 用于存储一次具体的碰撞注册请求
    /// </summary>
    private class CollisionRegistration
    {
        public ObiColliderBase TargetCollider; // **修复**: 使用基类ObiColliderBase
        public ObiActor TargetActor;
        public Action<Oni.Contact> Callback;
    }

    /// <summary>
    /// 主数据结构：将每个Solver映射到其下所有碰撞注册的列表
    /// </summary>
    private Dictionary<ObiSolver, List<CollisionRegistration>> _registrations = new Dictionary<ObiSolver, List<CollisionRegistration>>();
    
    // --- Unity 生命周期 ---

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // --- 公开 API ---

    /// <summary>
    /// 外部脚本通过此方法注册一个期望的碰撞回调。
    /// </summary>
    /// <param name="targetCollider">你希望监听的碰撞体 (e.g., 按钮、柱子)。</param>
    /// <param name="onCollisionCallback">碰撞发生时要执行的逻辑。</param>
    /// <param name="targetActor">你希望与之碰撞的Actor。如果为null，则自动设为玩家。</param>
    public void RegisterCollisionCallback(ObiColliderBase targetCollider, Action<Oni.Contact> onCollisionCallback, ObiActor targetActor = null)
    {
        if (targetCollider == null || onCollisionCallback == null)
        {
            Debug.LogError("注册失败：targetCollider 或 onCollisionCallback 不能为空！");
            return;
        }

        if (targetActor == null)
        {
            if (PlayerControl_Ball.instance != null)
            {
                targetActor = PlayerControl_Ball.instance.GetComponent<ObiActor>();
            }
            if (targetActor == null)
            {
                Debug.LogError("注册失败：无法自动获取玩家Actor！");
                return;
            }
        }
        
        ObiSolver solver = targetActor.solver;
        if (solver == null)
        {
            Debug.LogError($"注册失败：Actor '{targetActor.name}' 没有关联的Solver！");
            return;
        }

        if (!_registrations.ContainsKey(solver))
        {
            _registrations[solver] = new List<CollisionRegistration>();
            solver.OnCollision += OnSolverCollision;
            Debug.Log($"[ObiCollisionManager] 开始监听 Solver: {solver.name}");
        }

        var newRegistration = new CollisionRegistration
        {
            TargetCollider = targetCollider, // **修复**: 使用基类
            TargetActor = targetActor,
            Callback = onCollisionCallback
        };
        _registrations[solver].Add(newRegistration);
        Debug.Log($"成功注册碰撞: [{targetCollider.name}] <--> [{targetActor.name}]");
    }

    /// <summary>
    /// 外部脚本在销毁时应调用此方法，以取消注册，防止内存泄漏。
    /// </summary>
    /// <param name="targetCollider">要取消注册的碰撞体。</param>
    /// <param name="onCollisionCallback">要取消注册的回调（必须是同一个方法实例）。</param>
    public void UnregisterCollisionCallback(ObiColliderBase targetCollider, Action<Oni.Contact> onCollisionCallback)
    {
        if (targetCollider == null || onCollisionCallback == null) return;

        // 使用List来暂存需要移除的Solver，避免在遍历字典时修改它
        List<ObiSolver> solversToRemove = new List<ObiSolver>();

        foreach (var kvp in _registrations)
        {
            var solver = kvp.Key;
            var registrationList = kvp.Value;
            
            for (int i = registrationList.Count - 1; i >= 0; i--)
            {
                if (registrationList[i].TargetCollider == targetCollider && registrationList[i].Callback == onCollisionCallback)
                {
                    registrationList.RemoveAt(i);
                    Debug.Log($"成功取消碰撞注册: [{targetCollider.name}]");
                }
            }

            if (registrationList.Count == 0)
            {
                solversToRemove.Add(solver);
            }
        }

        // 在循环外统一处理停止监听和移除字典条目的操作
        foreach (var solver in solversToRemove)
        {
            solver.OnCollision -= OnSolverCollision;
            _registrations.Remove(solver);
            Debug.Log($"[ObiCollisionManager] 停止监听 Solver: {solver.name}");
        }
    }


    // --- 内部核心逻辑 ---

    /// <summary>
    /// 这是所有被监听的Solver共享的唯一回调函数。
    /// [已重写以正确处理 Oni.Contact]
    /// </summary>
    private void OnSolverCollision(ObiSolver solver, ObiNativeContactList contacts)
    {
        if (!_registrations.ContainsKey(solver)) return;

        var solverRegistrations = _registrations[solver];
        if (solverRegistrations.Count == 0) return;

        for (int i = 0; i < contacts.count; ++i)
        {
            var contact = contacts[i];

            // 尝试解析接触的双方
            var actorA = GetActorFromParticle(solver, contact.bodyA);
            var colliderA = GetColliderFromContact(contact.bodyA);
            var actorB = GetActorFromParticle(solver, contact.bodyB);
            var colliderB = GetColliderFromContact(contact.bodyB);

            // 遍历此Solver下的所有注册请求，看是否有匹配的
            foreach (var registration in solverRegistrations)
            {
                // 检查两种可能的碰撞组合
                // 组合1: A是Actor, B是Collider
                if (actorA == registration.TargetActor && colliderB == registration.TargetCollider)
                {
                    registration.Callback?.Invoke(contact);
                    break; // 此接触点已处理，继续检查下一个接触点
                }

                // 组合2: B是Actor, A是Collider
                if (actorB == registration.TargetActor && colliderA == registration.TargetCollider)
                {
                    registration.Callback?.Invoke(contact);
                    break; // 此接触点已处理，继续检查下一个接触点
                }
            }
        }
    }

    // --- 辅助函数 ---

    private ObiActor GetActorFromParticle(ObiSolver solver, int particleIndex)
    {
        if (particleIndex < 0 || particleIndex >= solver.particleToActor.Length) return null;
        var handle = solver.particleToActor[particleIndex];
        return handle?.actor;
    }

    // **修复**: 返回类型改为ObiColliderBase，以匹配ObiColliderWorld的API
    private ObiColliderBase GetColliderFromContact(int colliderIndex)
    {
        var world = ObiColliderWorld.GetInstance();
        if (world == null || colliderIndex < 0 || colliderIndex >= world.colliderHandles.Count) return null;
        return world.colliderHandles[colliderIndex].owner;
    }
}