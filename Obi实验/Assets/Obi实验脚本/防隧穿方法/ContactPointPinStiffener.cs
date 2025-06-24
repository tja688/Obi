using UnityEngine;
using Obi;
using System.Collections.Generic;

/// <summary>
/// (V3.3 - Final Version)
/// 修复了只在移除约束时才更新可视化的逻辑错误。
/// 新增 "enableDebugLogs" 开关，可以方便地在编辑器中开启或关闭所有性能敏感的日志输出。
/// </summary>
[RequireComponent(typeof(ObiSoftbody))]
public class EventDrivenPinStiffener_Final : MonoBehaviour
{
    [Header("核心功能配置")]
    [Tooltip("只对拥有此Tag的物体加强碰撞。留空则对所有碰撞生效。")]
    public string colliderTag;
    [Tooltip("预分配的 Pin 约束池大小。")]
    public int pinPoolSize = 64;

    [Header("固定模式与强度")]
    [Tooltip("Pin约束的硬度 (0-1)。")]
    [Range(0f, 1f)]
    public float pinStiffness = 1f;

    [Header("可视化与调试")]
    [Tooltip("启用后，被固定的粒子会改变颜色。")]
    public bool enableVisualization = true;
    public Color pinnedParticleColor = Color.green;
    [Tooltip("启用后，将在控制台输出详细的调试日志。")]
    public bool enableDebugLogs = false; // <<< 日志总开关

    // C# 层的状态追踪器
    private class PinInfo
    {
        public int ParticleSolverIndex;
        public int ColliderHandleIndex;
        public bool IsVerifiedThisFrame;
    }
    private PinInfo[] pinInfos;

    // 底层组件与数据结构
    private ObiSoftbody softbody;
    private ObiSolver solver;
    private ObiPinConstraintsData pinConstraintsData;
    private ObiPinConstraintsBatch dynamicPinBatch;
    private Dictionary<long, int> collisionPairToBatchIndex = new Dictionary<long, int>();
    private readonly Dictionary<int, Color> originalParticleColors = new Dictionary<int, Color>();

    // 脏标记，用于追踪约束列表是否发生变化
    private bool constraintsHaveChanged = false;

    #region --- Unity生命周期与Obi初始化 ---

    void OnEnable()
    {
        if (enableDebugLogs) Debug.Log("[PinDebugger] OnEnable: 脚本已启动。", this);
        softbody = GetComponent<ObiSoftbody>();
        softbody.OnBlueprintLoaded += OnBlueprintLoaded;
        if (softbody.isLoaded) OnBlueprintLoaded(softbody, softbody.sourceBlueprint);
    }

    void OnDisable()
    {
        if (enableDebugLogs) Debug.Log("[PinDebugger] OnDisable: 脚本已禁用。清理约束和事件订阅。", this);
        softbody.OnBlueprintLoaded -= OnBlueprintLoaded;
        RemoveDynamicBatch();
        RestoreAllParticleColors();
    }

    private void OnBlueprintLoaded(ObiActor actor, ObiActorBlueprint blueprint)
    {
        if (enableDebugLogs) Debug.Log("[PinDebugger] OnBlueprintLoaded: 软体资源已加载，开始设置批处理并订阅求解器事件。", this);
        SetupDynamicBatch();
        SubscribeToSolver();
    }

    private void SubscribeToSolver()
    {
        if (solver != null) solver.OnCollision -= Solver_OnCollision;
        solver = softbody.solver;
        if (solver != null)
        {
            if (enableDebugLogs) Debug.Log("[PinDebugger] SubscribeToSolver: 成功获取求解器，正在订阅OnCollision事件。", this);
            solver.OnCollision += Solver_OnCollision;
        }
        else
        {
            Debug.LogError("[PinDebugger] SubscribeToSolver: 未能获取到求解器 (Solver)!", this);
        }
    }

    private void SetupDynamicBatch()
    {
        RemoveDynamicBatch();
        pinConstraintsData = softbody.GetConstraintsByType(Oni.ConstraintType.Pin) as ObiPinConstraintsData;
        if (pinConstraintsData == null) { Debug.LogError($"[{this.name}] ObiSoftbody上缺少PinConstraints组件。请添加一个Obi Pin Constraints。", this); enabled = false; return; }

        dynamicPinBatch = new ObiPinConstraintsBatch();
        pinInfos = new PinInfo[pinPoolSize];
        for (int i = 0; i < pinPoolSize; ++i)
        {
            dynamicPinBatch.AddConstraint(-1, null, Vector3.zero, Quaternion.identity, 1f, 1f);
            pinInfos[i] = new PinInfo();
        }
        dynamicPinBatch.activeConstraintCount = 0;
        pinConstraintsData.AddBatch(dynamicPinBatch);
        if (enableDebugLogs) Debug.Log($"[PinDebugger] SetupDynamicBatch: 动态约束批处理创建完毕，池大小为 {pinPoolSize}。", this);
    }

    private void RemoveDynamicBatch()
    {
        if (solver != null) solver.OnCollision -= Solver_OnCollision;
        solver = null;
        if (pinConstraintsData != null && dynamicPinBatch != null)
        {
            pinConstraintsData.RemoveBatch(dynamicPinBatch);
            if (softbody.isLoaded) softbody.SetConstraintsDirty(Oni.ConstraintType.Pin);
            if (enableDebugLogs) Debug.Log("[PinDebugger] RemoveDynamicBatch: 已从求解器移除动态约束批处理。", this);
        }
        dynamicPinBatch = null;
        pinConstraintsData = null;
        collisionPairToBatchIndex.Clear();
    }

    #endregion

    #region --- 核心逻辑: 碰撞、更新、激活、停用 ---

    private void Solver_OnCollision(ObiSolver solver, ObiNativeContactList contacts)
    {
        if (contacts.count == 0 || dynamicPinBatch == null) return;
        if (enableDebugLogs && contacts.count > 0) Debug.Log($"[PinDebugger] ---> Solver_OnCollision: 事件触发，收到 {contacts.count} 个碰撞点。", this);

        for (int i = 0; i < contacts.count; ++i)
        {
            Oni.Contact contact = contacts[i];
            int particleSolverIndex = GetParticleSolverIndexFromContact(contact);
            if (particleSolverIndex == -1) continue;

            int colliderIndex = GetColliderIndexFromContact(contact);
            var colliderHandle = ObiColliderWorld.GetInstance().colliderHandles[colliderIndex];
            var otherCollider = colliderHandle.owner;
            if (otherCollider == null || !otherCollider.gameObject.activeInHierarchy) continue;

            if (!string.IsNullOrEmpty(colliderTag) && !otherCollider.CompareTag(colliderTag))
            {
                if (enableDebugLogs) Debug.Log($"[PinDebugger] OnCollision: 碰撞体 '{otherCollider.name}' 的标签 '{otherCollider.tag}' 不匹配所需标签 '{colliderTag}'，已跳过。", this);
                continue;
            }

            long pairID = GetCollisionPairID(particleSolverIndex, colliderHandle.index);

            if (collisionPairToBatchIndex.TryGetValue(pairID, out int slotIndex))
            {
                pinInfos[slotIndex].IsVerifiedThisFrame = true;
            }
            else if (dynamicPinBatch.activeConstraintCount < pinPoolSize)
            {
                if (enableDebugLogs) Debug.Log($"[PinDebugger] OnCollision: [创建] 发现新碰撞对 (粒 {particleSolverIndex}, 物 {otherCollider.name})，准备创建Pin。", this);
                Vector3 localMidpoint = ((Vector3)contact.pointA + (Vector3)contact.pointB) * 0.5f;
                Vector3 worldAnchor = solver.transform.TransformPoint(localMidpoint);
                ActivatePin(particleSolverIndex, colliderHandle, worldAnchor);
            }
            else
            {
                Debug.LogWarning("[PinDebugger] OnCollision: 约束池已满，无法为新碰撞创建Pin！", this);
            }
        }
    }

    void LateUpdate()
    {
        if (!softbody.isLoaded || dynamicPinBatch == null) return;

        // 标记阶段
        for (int i = 0; i < dynamicPinBatch.activeConstraintCount; ++i)
        {
            pinInfos[i].IsVerifiedThisFrame = false;
        }

        // 清扫阶段
        for (int i = dynamicPinBatch.activeConstraintCount - 1; i >= 0; --i)
        {
            if (!pinInfos[i].IsVerifiedThisFrame)
            {
                if (enableDebugLogs) Debug.Log($"[PinDebugger] LateUpdate: [清扫] Pin (Slot {i}) 在本帧未被验证，即将移除。", this);
                DeactivatePin(i);
            }
        }

        // 统一更新阶段
        if (constraintsHaveChanged)
        {
            if (enableDebugLogs) Debug.Log("[PinDebugger] LateUpdate: 检测到约束变更，设置SetConstraintsDirty并更新颜色。", this);
            softbody.SetConstraintsDirty(Oni.ConstraintType.Pin);

            if (enableVisualization)
                UpdateColors();

            constraintsHaveChanged = false;
        }
    }

    private void ActivatePin(int particleSolverIndex, ObiColliderHandle colliderHandle, Vector3 worldAnchor)
    {
        int slotIndex = dynamicPinBatch.activeConstraintCount;

        dynamicPinBatch.particleIndices[slotIndex] = particleSolverIndex;
        dynamicPinBatch.pinBodies[slotIndex] = null;
        dynamicPinBatch.colliderIndices[slotIndex] = -1;
        dynamicPinBatch.offsets[slotIndex] = worldAnchor;
        dynamicPinBatch.stiffnesses[slotIndex * 2] = 1f - pinStiffness;
        dynamicPinBatch.stiffnesses[slotIndex * 2 + 1] = 1f;

        var info = pinInfos[slotIndex];
        info.ParticleSolverIndex = particleSolverIndex;
        info.ColliderHandleIndex = colliderHandle.index;
        info.IsVerifiedThisFrame = true;

        collisionPairToBatchIndex[GetCollisionPairID(particleSolverIndex, colliderHandle.index)] = slotIndex;
        dynamicPinBatch.activeConstraintCount++;

        constraintsHaveChanged = true;

        if (enableDebugLogs) Debug.Log($"[PinDebugger] ---> ActivatePin: 在槽位 {slotIndex} 成功激活一个新Pin。粒子: {particleSolverIndex}, 碰撞体: {colliderHandle.owner.name}, 世界锚点: {worldAnchor}", this);
    }

    private void DeactivatePin(int slotIndex)
    {
        long pairID = GetCollisionPairID(pinInfos[slotIndex].ParticleSolverIndex, pinInfos[slotIndex].ColliderHandleIndex);
        collisionPairToBatchIndex.Remove(pairID);

        dynamicPinBatch.activeConstraintCount--;
        int lastActiveIndex = dynamicPinBatch.activeConstraintCount;

        if (enableDebugLogs) Debug.Log($"[PinDebugger] ---> DeactivatePin: 正在停用槽位 {slotIndex} 的Pin。当前活动约束剩余 {dynamicPinBatch.activeConstraintCount}。", this);

        if (slotIndex < lastActiveIndex)
        {
            if (enableDebugLogs) Debug.Log($"[PinDebugger] DeactivatePin: 执行Swap and Pop, 将最后一个有效槽位 {lastActiveIndex} 的数据移动到 {slotIndex}。", this);

            dynamicPinBatch.particleIndices[slotIndex] = dynamicPinBatch.particleIndices[lastActiveIndex];
            dynamicPinBatch.pinBodies[slotIndex] = dynamicPinBatch.pinBodies[lastActiveIndex];
            dynamicPinBatch.colliderIndices[slotIndex] = dynamicPinBatch.colliderIndices[lastActiveIndex];
            dynamicPinBatch.offsets[slotIndex] = dynamicPinBatch.offsets[lastActiveIndex];
            dynamicPinBatch.stiffnesses[slotIndex * 2] = dynamicPinBatch.stiffnesses[lastActiveIndex * 2];
            dynamicPinBatch.stiffnesses[slotIndex * 2 + 1] = dynamicPinBatch.stiffnesses[lastActiveIndex * 2 + 1];

            pinInfos[slotIndex].ParticleSolverIndex = pinInfos[lastActiveIndex].ParticleSolverIndex;
            pinInfos[slotIndex].ColliderHandleIndex = pinInfos[lastActiveIndex].ColliderHandleIndex;
            pinInfos[slotIndex].IsVerifiedThisFrame = pinInfos[lastActiveIndex].IsVerifiedThisFrame;

            long movedPairID = GetCollisionPairID(pinInfos[slotIndex].ParticleSolverIndex, pinInfos[slotIndex].ColliderHandleIndex);
            collisionPairToBatchIndex[movedPairID] = slotIndex;
        }

        constraintsHaveChanged = true;
    }

    #endregion

    #region --- 可视化与辅助方法 ---

    private void UpdateColors()
    {
        if (enableDebugLogs) Debug.Log("[PinDebugger] ---> UpdateColors: 开始更新粒子颜色。", this);
        RestoreAllParticleColors();

        if (enableDebugLogs) Debug.Log($"[PinDebugger] UpdateColors: 准备为 {dynamicPinBatch.activeConstraintCount} 个激活的Pin粒子上色。", this);
        for (int i = 0; i < dynamicPinBatch.activeConstraintCount; ++i)
        {
            int solverIndex = dynamicPinBatch.particleIndices[i];
            if (!originalParticleColors.ContainsKey(solverIndex))
            {
                originalParticleColors[solverIndex] = solver.colors[solverIndex];
            }
            solver.colors[solverIndex] = pinnedParticleColor;
        }
        solver.colors.Upload();
        if (enableDebugLogs) Debug.Log("[PinDebugger] UpdateColors: 颜色数据上传至GPU完毕。", this);
    }

    private void RestoreAllParticleColors()
    {
        if (originalParticleColors.Count > 0 && solver != null)
        {
            if (enableDebugLogs) Debug.Log($"[PinDebugger] RestoreAllParticleColors: 恢复 {originalParticleColors.Count} 个粒子的原始颜色。", this);
            foreach (var p in originalParticleColors)
            {
                if (p.Key >= 0 && p.Key < solver.colors.count) solver.colors[p.Key] = p.Value;
            }
            solver.colors.Upload();
        }
        originalParticleColors.Clear();
    }

    private long GetCollisionPairID(int particleIndex, int colliderIndex)
    {
        return (long)particleIndex << 32 | (long)(uint)colliderIndex;
    }

    private int GetParticleSolverIndexFromContact(Oni.Contact contact)
    {
        if (IsParticleFromOurSoftbody(contact.bodyA)) return contact.bodyA;
        if (IsParticleFromOurSoftbody(contact.bodyB)) return contact.bodyB;
        return -1;
    }

    private int GetColliderIndexFromContact(Oni.Contact contact)
    {
        return IsParticleFromOurSoftbody(contact.bodyA) ? contact.bodyB : contact.bodyA;
    }

    private bool IsParticleFromOurSoftbody(int particleSolverIndex)
    {
        if (solver == null || particleSolverIndex < 0 || particleSolverIndex >= solver.particleToActor.Length) return false;
        var p = solver.particleToActor[particleSolverIndex];
        return p != null && p.actor == softbody;
    }

    #endregion
}