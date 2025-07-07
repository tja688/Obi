using UnityEngine;
using Obi;
using System.Collections.Generic;

/// <summary>
/// (V4.0 - 动态接触点最终版)
/// 真正实现了将约束与碰撞体解耦的设计目标。
/// Pin约束被创建在世界空间，其锚点会在OnCollision事件中被持续更新到最新的接触点。
/// 这提供了强大的支撑力，同时允许碰撞体在软体表面自由滑动和滚动，不会产生“粘滞”效应。
/// </summary>
[RequireComponent(typeof(ObiSoftbody))]
public class ContactPointPinStiffener : MonoBehaviour // 可以用回你原来的名字
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
    public bool enableDebugLogs = false;

    // C# 层的状态追踪器
    private class PinInfo
    {
        public int ParticleSolverIndex;
        public int ColliderHandleIndex; // 用于生成唯一的碰撞对ID
        public bool IsVerifiedThisFrame;
    }
    private PinInfo[] pinInfos;

    private ObiSoftbody softbody;
    private ObiSolver solver;
    private ObiPinConstraintsData pinConstraintsData;
    private ObiPinConstraintsBatch dynamicPinBatch;
    
    // 使用一个长整型作为Key，高32位存粒子索引，低32位存碰撞体索引，唯一标识一个碰撞对
    private Dictionary<long, int> collisionPairToBatchIndex = new Dictionary<long, int>();
    private readonly Dictionary<int, Color> originalParticleColors = new Dictionary<int, Color>();

    // 脏标记，用于追踪约束列表或其参数是否发生变化
    private bool constraintsHaveChanged = false;

    #region --- Unity生命周期与Obi初始化 ---

    void OnEnable()
    {
        softbody = GetComponent<ObiSoftbody>();
        softbody.OnBlueprintLoaded += OnBlueprintLoaded;
        if (softbody.isLoaded) OnBlueprintLoaded(softbody, softbody.sourceBlueprint);
    }

    void OnDisable()
    {
        softbody.OnBlueprintLoaded -= OnBlueprintLoaded;
        RemoveDynamicBatch();
        RestoreAllParticleColors();
    }

    private void OnBlueprintLoaded(ObiActor actor, ObiActorBlueprint blueprint)
    {
        SetupDynamicBatch();
        SubscribeToSolver();
    }

    private void SubscribeToSolver()
    {
        if (solver != null) solver.OnCollision -= Solver_OnCollision;
        solver = softbody.solver;
        if (solver != null) solver.OnCollision += Solver_OnCollision;
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
    }

    private void RemoveDynamicBatch()
    {
        if (solver != null) solver.OnCollision -= Solver_OnCollision;
        solver = null;
        if (pinConstraintsData != null && dynamicPinBatch != null)
        {
            pinConstraintsData.RemoveBatch(dynamicPinBatch);
            if (softbody.isLoaded) softbody.SetConstraintsDirty(Oni.ConstraintType.Pin);
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

        for (int i = 0; i < contacts.count; ++i)
        {
            Oni.Contact contact = contacts[i];
            int particleSolverIndex = GetParticleSolverIndexFromContact(contact);
            if (particleSolverIndex == -1) continue;

            int colliderIndex = GetColliderIndexFromContact(contact);
            var colliderHandle = ObiColliderWorld.GetInstance().colliderHandles[colliderIndex];
            var otherCollider = colliderHandle.owner;
            if (otherCollider == null || !otherCollider.gameObject.activeInHierarchy) continue;
            if (!string.IsNullOrEmpty(colliderTag) && !otherCollider.CompareTag(colliderTag)) continue;

            long pairID = GetCollisionPairID(particleSolverIndex, colliderHandle.index);

            // 计算接触点的世界坐标
            Vector3 worldAnchor = solver.transform.TransformPoint(((Vector3)contact.pointA + (Vector3)contact.pointB) * 0.5f);

            if (collisionPairToBatchIndex.TryGetValue(pairID, out int slotIndex))
            {
                // *** 核心修改 1: 更新已存在的约束 ***
                // 该碰撞对已存在Pin，我们更新它的目标点并标记为已验证
                dynamicPinBatch.offsets[slotIndex] = worldAnchor;
                pinInfos[slotIndex].IsVerifiedThisFrame = true;
                constraintsHaveChanged = true; // 更新了参数，也需要设置脏标记
                 if (enableDebugLogs) Debug.Log($"[PinDebugger] OnCollision: [更新] Pin (Slot {slotIndex}), 粒子 {particleSolverIndex}, 新锚点 {worldAnchor}", this);
            }
            else if (dynamicPinBatch.activeConstraintCount < pinPoolSize)
            {
                // 新的碰撞对，创建Pin
                if (enableDebugLogs) Debug.Log($"[PinDebugger] OnCollision: [创建] 发现新碰撞对 (粒 {particleSolverIndex}, 物 {otherCollider.name})", this);
                ActivatePin(particleSolverIndex, colliderHandle, worldAnchor);
            }
            else
            {
                Debug.LogWarning("[PinDebugger] OnCollision: 约束池已满!", this);
            }
        }
    }

    void LateUpdate()
    {
        if (!softbody.isLoaded || dynamicPinBatch == null) return;

        // 1. 标记阶段: 假设所有约束都未被验证
        for (int i = 0; i < dynamicPinBatch.activeConstraintCount; ++i)
        {
            pinInfos[i].IsVerifiedThisFrame = false;
        }

        // 2. 验证与更新阶段: Unity/Obi 在此期间的某个点会调用 Solver_OnCollision

        // 3. 清扫阶段: 移除本帧没有收到OnCollision事件的约束
        for (int i = dynamicPinBatch.activeConstraintCount - 1; i >= 0; --i)
        {
            if (!pinInfos[i].IsVerifiedThisFrame)
            {
                if (enableDebugLogs) Debug.Log($"[PinDebugger] LateUpdate: [清扫] Pin (Slot {i}) 在本帧未被验证，即将移除。", this);
                DeactivatePin(i);
            }
        }

        // 4. 统一提交通知
        if (constraintsHaveChanged)
        {
            softbody.SetConstraintsDirty(Oni.ConstraintType.Pin);
            if (enableVisualization)
            {
                UpdateColors();
            }
            constraintsHaveChanged = false; // 重置脏标记
        }
    }
    
    // *** 核心修改 2: 激活逻辑回归到世界空间Pin ***
    private void ActivatePin(int particleSolverIndex, ObiColliderHandle colliderHandle, Vector3 worldAnchor)
    {
        int slotIndex = dynamicPinBatch.activeConstraintCount;

        dynamicPinBatch.particleIndices[slotIndex] = particleSolverIndex;
        dynamicPinBatch.pinBodies[slotIndex] = null; // 解耦的关键
        dynamicPinBatch.colliderIndices[slotIndex] = -1; // 解耦的关键
        dynamicPinBatch.offsets[slotIndex] = worldAnchor; // Pin在世界空间
        dynamicPinBatch.stiffnesses[slotIndex * 2] = 1f - pinStiffness;
        dynamicPinBatch.stiffnesses[slotIndex * 2 + 1] = 1f;

        var info = pinInfos[slotIndex];
        info.ParticleSolverIndex = particleSolverIndex;
        info.ColliderHandleIndex = colliderHandle.index;
        info.IsVerifiedThisFrame = true; // 新创建的当然是验证过的

        collisionPairToBatchIndex[GetCollisionPairID(particleSolverIndex, colliderHandle.index)] = slotIndex;
        dynamicPinBatch.activeConstraintCount++;

        constraintsHaveChanged = true;
    }

    // DeactivatePin 逻辑本身是好的，继续使用 "Swap and Pop"
    private void DeactivatePin(int slotIndex)
    {
        long pairID = GetCollisionPairID(pinInfos[slotIndex].ParticleSolverIndex, pinInfos[slotIndex].ColliderHandleIndex);
        collisionPairToBatchIndex.Remove(pairID);

        dynamicPinBatch.activeConstraintCount--;
        int lastActiveIndex = dynamicPinBatch.activeConstraintCount;

        if (slotIndex < lastActiveIndex)
        {
            // 将最后一个有效约束的数据移到当前槽位
            dynamicPinBatch.particleIndices[slotIndex] = dynamicPinBatch.particleIndices[lastActiveIndex];
            dynamicPinBatch.pinBodies[slotIndex] = dynamicPinBatch.pinBodies[lastActiveIndex];
            dynamicPinBatch.colliderIndices[slotIndex] = dynamicPinBatch.colliderIndices[lastActiveIndex];
            dynamicPinBatch.offsets[slotIndex] = dynamicPinBatch.offsets[lastActiveIndex];
            dynamicPinBatch.stiffnesses[slotIndex * 2] = dynamicPinBatch.stiffnesses[lastActiveIndex * 2];
            dynamicPinBatch.stiffnesses[slotIndex * 2 + 1] = dynamicPinBatch.stiffnesses[lastActiveIndex * 2 + 1];

            // 同样移动我们的C#追踪信息
            pinInfos[slotIndex].ParticleSolverIndex = pinInfos[lastActiveIndex].ParticleSolverIndex;
            pinInfos[slotIndex].ColliderHandleIndex = pinInfos[lastActiveIndex].ColliderHandleIndex;
            pinInfos[slotIndex].IsVerifiedThisFrame = pinInfos[lastActiveIndex].IsVerifiedThisFrame;

            // 更新被移动的约束在字典中的索引
            long movedPairID = GetCollisionPairID(pinInfos[slotIndex].ParticleSolverIndex, pinInfos[slotIndex].ColliderHandleIndex);
            collisionPairToBatchIndex[movedPairID] = slotIndex;
        }

        constraintsHaveChanged = true;
    }

    #endregion

    #region --- 可视化与辅助方法 (保持不变) ---
    // ... (此区域所有代码与你原脚本一致，无需修改)
    private void UpdateColors()
    {
        if (solver == null) return;
        RestoreAllParticleColors();
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
    }
    private void RestoreAllParticleColors()
    {
        if (originalParticleColors.Count > 0 && solver != null)
        {
            foreach (var p in originalParticleColors)
            {
                if (p.Key >= 0 && p.Key < solver.colors.count) solver.colors[p.Key] = p.Value;
            }
            solver.colors.Upload();
        }
        originalParticleColors.Clear();
    }
    private long GetCollisionPairID(int particleIndex, int colliderIndex) { return (long)particleIndex << 32 | (long)(uint)colliderIndex; }
    private int GetParticleSolverIndexFromContact(Oni.Contact contact) { if (IsParticleFromOurSoftbody(contact.bodyA)) return contact.bodyA; if (IsParticleFromOurSoftbody(contact.bodyB)) return contact.bodyB; return -1; }
    private int GetColliderIndexFromContact(Oni.Contact contact) { return IsParticleFromOurSoftbody(contact.bodyA) ? contact.bodyB : contact.bodyA; }
    private bool IsParticleFromOurSoftbody(int particleSolverIndex) { if (solver == null || particleSolverIndex < 0 || particleSolverIndex >= solver.particleToActor.Length) return false; var p = solver.particleToActor[particleSolverIndex]; return p != null && p.actor == softbody; }
    #endregion
}