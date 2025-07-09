using UnityEngine;
using System;
using System.Collections.Generic;
using Obi;

/// <summary>
/// Obi 碰撞管理中心 (单例)
/// [已添加完善的Debug日志系统]
/// </summary>
public class ObiCollisionManager : MonoBehaviour
{
    [Header("调试设置")]
    [Tooltip("勾选以在控制台打印详细的注册、注销及碰撞处理日志")]
    [SerializeField] private bool enableDebugLogging = true; // 在Inspector中控制此开关

    // --- 单例实现 ---
    public static ObiCollisionManager Instance { get; private set; }

    // --- 内部数据结构 ---
    private class CollisionRegistration
    {
        public ObiColliderBase TargetCollider;
        public ObiActor TargetActor;
        public Action<Oni.Contact> Callback;
    }
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

    // --- 调试日志辅助函数 ---
    private void Log(string message)
    {
        if (enableDebugLogging)
        {
            // 使用颜色来区分管理器的日志，使其在控制台中更显眼
            Debug.Log($"<color=#00ffff>[ObiCollisionManager]</color> {message}");
        }
    }

    // --- 公开 API ---
    public void RegisterCollisionCallback(ObiColliderBase targetCollider, Action<Oni.Contact> onCollisionCallback, ObiActor targetActor = null)
    {
        if (targetCollider == null || onCollisionCallback == null)
        {
            Debug.LogError("[ObiCollisionManager] 注册失败：targetCollider 或 onCollisionCallback 不能为空！");
            return;
        }

        if (targetActor == null)
        {
            if (PlayerControl_Ball.instance != null)
                targetActor = PlayerControl_Ball.instance.GetComponent<ObiActor>();
            
            if (targetActor == null)
            {
                Debug.LogError("[ObiCollisionManager] 注册失败：无法自动获取玩家Actor！");
                return;
            }
        }
        
        ObiSolver solver = targetActor.solver;
        if (solver == null)
        {
            Debug.LogError($"[ObiCollisionManager] 注册失败：Actor '{targetActor.name}' 没有关联的Solver！");
            return;
        }

        Log($"收到注册请求: Collider '{targetCollider.name}' <--> Actor '{targetActor.name}'");

        if (!_registrations.ContainsKey(solver))
        {
            _registrations[solver] = new List<CollisionRegistration>();
            solver.OnCollision += OnSolverCollision;
            Log($"<color=lime>首次监听 Solver '{solver.name}' 的 OnCollision 事件。</color>");
        }

        var newRegistration = new CollisionRegistration
        {
            TargetCollider = targetCollider,
            TargetActor = targetActor,
            Callback = onCollisionCallback
        };
        _registrations[solver].Add(newRegistration);
        Log($"注册成功！当前 Solver '{solver.name}' 有 {_registrations[solver].Count} 个注册项。");
    }

    public void UnregisterCollisionCallback(ObiColliderBase targetCollider, Action<Oni.Contact> onCollisionCallback)
    {
        if (targetCollider == null || onCollisionCallback == null) return;

        Log($"收到注销请求: Collider '{targetCollider.name}'");
        List<ObiSolver> solversToRemove = new List<ObiSolver>();

        foreach (var kvp in _registrations)
        {
            var solver = kvp.Key;
            var registrationList = kvp.Value;
            
            int removedCount = registrationList.RemoveAll(reg => reg.TargetCollider == targetCollider && reg.Callback == onCollisionCallback);

            if (removedCount > 0)
            {
                Log($"从 Solver '{solver.name}' 中成功注销 {removedCount} 个匹配项。");
            }

            if (registrationList.Count == 0)
            {
                solversToRemove.Add(solver);
            }
        }

        foreach (var solver in solversToRemove)
        {
            solver.OnCollision -= OnSolverCollision;
            _registrations.Remove(solver);
            Log($"<color=orange>已无注册项，停止监听 Solver '{solver.name}'。</color>");
        }
    }

    // --- 内部核心逻辑 ---
    private void OnSolverCollision(ObiSolver solver, ObiNativeContactList contacts)
    {
        Log($"\n<color=yellow>----------- OnCollision 事件触发: Solver '{solver.name}' -----------</color>");
        
        if (!_registrations.ContainsKey(solver))
        {
            Log("错误：收到了未注册的Solver发来的事件。");
            return;
        }

        var solverRegistrations = _registrations[solver];
        Log($"处理中... 关联注册项: {solverRegistrations.Count} 个。 接触点数量: {contacts.count} 个。");

        if (solverRegistrations.Count == 0) return;

        for (int i = 0; i < contacts.count; ++i)
        {
            var contact = contacts[i];
            
            Log($"  [接触点 {i+1}/{contacts.count}] bodyA: {contact.bodyA}, bodyB: {contact.bodyB}");

            // 尝试解析接触的双方
            var actorA = GetActorFromParticle(solver, contact.bodyA);
            var colliderA = GetColliderFromContact(contact.bodyA);
            var actorB = GetActorFromParticle(solver, contact.bodyB);
            var colliderB = GetColliderFromContact(contact.bodyB);

            // 打印详细的解析结果
            string bodyAResult = $"bodyA -> Actor: {(actorA != null ? actorA.name : "null")}, Collider: {(colliderA != null ? colliderA.name : "null")}";
            string bodyBResult = $"bodyB -> Actor: {(actorB != null ? actorB.name : "null")}, Collider: {(colliderB != null ? colliderB.name : "null")}";
            Log($"    解析: {bodyAResult} | {bodyBResult}");

            foreach (var registration in solverRegistrations)
            {
                // 检查两种可能的碰撞组合
                if ((actorA == registration.TargetActor && colliderB == registration.TargetCollider) ||
                    (actorB == registration.TargetActor && colliderA == registration.TargetCollider))
                {
                    Log($"    <color=lime>---> 匹配成功! <--</color> 触发注册在 Collider '{registration.TargetCollider.name}' 上的回调。");
                    registration.Callback?.Invoke(contact);
                    // 通常一个接触点只应触发一个回调，如果需要支持多个可以移除break。
                    break; 
                }
            }
        }
        Log("<color=yellow>----------- 事件处理完毕 -----------</color>");
    }

    // --- 辅助函数 ---
    private ObiActor GetActorFromParticle(ObiSolver solver, int particleIndex)
    {
        if (particleIndex < 0 || particleIndex >= solver.particleToActor.Length) return null;
        var handle = solver.particleToActor[particleIndex];
        return handle?.actor;
    }

    private ObiColliderBase GetColliderFromContact(int colliderIndex)
    {
        var world = ObiColliderWorld.GetInstance();
        if (world == null || colliderIndex < 0 || colliderIndex >= world.colliderHandles.Count) return null;
        return world.colliderHandles[colliderIndex].owner;
    }
}