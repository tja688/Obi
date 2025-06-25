using UnityEngine;
using Obi;
using System.Collections.Generic;

/// <summary>
/// (V3.3 - 修正版)
/// 修复了Pin约束固定在世界空间以及生命周期管理不当的问题。
/// 采用了与健壮版类似的逻辑：将粒子钉在碰撞体的局部空间，并引入了可选的脱离距离来释放约束。
/// </summary>
[RequireComponent(typeof(ObiSoftbody))]
public class ContactPointPinStiffener_Corrected : MonoBehaviour // 重命名以区分
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

    [Header("柔化挣脱 (推荐启用)")]
    [Tooltip("启用后，当粒子远离其锚定点时，会自动释放约束。")]
    public bool enableDisengagement = true; // 推荐启用，否则Pin会永久存在
    [Tooltip("当粒子与其锚定点的距离超过此值时，释放Pin约束。")]
    public float disengagementDistance = 0.1f;

    [Header("可视化与调试")]
    [Tooltip("启用后，被固定的粒子会改变颜色。")]
    public bool enableVisualization = true;
    public Color pinnedParticleColor = Color.green;
    [Tooltip("启用后，将在控制台输出详细的调试日志。")]
    public bool enableDebugLogs = false;

    // C# 层的状态追踪器
    private class PinInfo { public ObiColliderBase Collider = null; public Vector3 LocalOffset; }
    private PinInfo[] pinInfos;

    private ObiSoftbody softbody;
    private ObiSolver solver;
    private ObiPinConstraintsData pinConstraintsData;
    private ObiPinConstraintsBatch dynamicPinBatch;

    // 使用粒子求解器索引来快速查找其在批处理中的位置，确保一个粒子只被钉一次
    private Dictionary<int, int> particleToBatchIndex = new Dictionary<int, int>();
    private readonly Dictionary<int, Color> originalParticleColors = new Dictionary<int, Color>();

    void OnEnable() { softbody = GetComponent<ObiSoftbody>(); softbody.OnBlueprintLoaded += OnBlueprintLoaded; if (softbody.isLoaded) OnBlueprintLoaded(softbody, softbody.sourceBlueprint); }
    void OnDisable() { softbody.OnBlueprintLoaded -= OnBlueprintLoaded; RemoveDynamicBatch(); RestoreAllParticleColors(); }
    private void OnBlueprintLoaded(ObiActor actor, ObiActorBlueprint blueprint) { SetupDynamicBatch(); SubscribeToSolver(); }
    private void SubscribeToSolver() { if (solver != null) solver.OnCollision -= Solver_OnCollision; solver = softbody.solver; if (solver != null) solver.OnCollision += Solver_OnCollision; }

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
        particleToBatchIndex.Clear();
    }

    void LateUpdate()
    {
        if (!softbody.isLoaded || dynamicPinBatch == null || !enableDisengagement) return;

        bool needsUpdate = false;
        // 倒序遍历以安全地在循环中移除元素
        for (int i = dynamicPinBatch.activeConstraintCount - 1; i >= 0; --i)
        {
            var info = pinInfos[i];
            var targetCollider = info.Collider;

            // 如果碰撞体被禁用或销毁，则释放Pin
            if (targetCollider == null || !targetCollider.gameObject.activeInHierarchy)
            {
                DeactivatePin(i);
                needsUpdate = true;
                continue;
            }
            
            // 检查脱离距离
            var particleSolverIndex = dynamicPinBatch.particleIndices[i];
            Vector3 currentParticlePos = solver.positions[particleSolverIndex];
            Vector3 pinWorldPos = targetCollider.transform.TransformPoint(info.LocalOffset);

            if (Vector3.SqrMagnitude(currentParticlePos - pinWorldPos) > disengagementDistance * disengagementDistance)
            {
                DeactivatePin(i);
                needsUpdate = true;
            }
        }

        if (needsUpdate)
        {
            softbody.SetConstraintsDirty(Oni.ConstraintType.Pin);
            if(enableVisualization) UpdateColors();
        }
    }

    private void Solver_OnCollision(ObiSolver solver, ObiNativeContactList contacts)
    {
        if (contacts.count == 0 || dynamicPinBatch == null) return;

        bool needsUpdate = false;
        for (int i = 0; i < contacts.count; ++i)
        {
            // 如果池已满，则停止
            if (dynamicPinBatch.activeConstraintCount >= pinPoolSize)
            {
                if (enableDebugLogs) Debug.LogWarning("Pin约束池已满，无法创建新的约束。");
                break;
            }

            Oni.Contact contact = contacts[i];
            int particleSolverIndex = GetParticleSolverIndexFromContact(contact);

            // 如果粒子无效或已经被钉住，则跳过
            if (particleSolverIndex == -1 || particleToBatchIndex.ContainsKey(particleSolverIndex)) continue;
            
            var otherCollider = ObiColliderWorld.GetInstance().colliderHandles[GetColliderIndexFromContact(contact)].owner;
            if (otherCollider == null || !otherCollider.gameObject.activeInHierarchy) continue;
            if (!string.IsNullOrEmpty(colliderTag) && !otherCollider.CompareTag(colliderTag)) continue;
            
            // *** 核心修正 1: 计算局部偏移量 ***
            Matrix4x4 bindMatrix = otherCollider.transform.worldToLocalMatrix * solver.transform.localToWorldMatrix;
            Vector3 pinOffset = bindMatrix.MultiplyPoint3x4(solver.positions[particleSolverIndex]);
            if (float.IsNaN(pinOffset.x)) continue;
            
            ActivatePin(particleSolverIndex, otherCollider, pinOffset);
            needsUpdate = true;
        }

        if (needsUpdate)
        {
            softbody.SetConstraintsDirty(Oni.ConstraintType.Pin);
            if(enableVisualization) UpdateColors();
        }
    }

    private void ActivatePin(int particleSolverIndex, ObiColliderBase collider, Vector3 localOffset)
    {
        int slotIndex = dynamicPinBatch.activeConstraintCount;

        // *** 核心修正 2: 正确设置约束参数 ***
        dynamicPinBatch.particleIndices[slotIndex] = particleSolverIndex;
        dynamicPinBatch.pinBodies[slotIndex] = collider.Handle; // 钉在碰撞体上
        dynamicPinBatch.colliderIndices[slotIndex] = collider.Handle.index;
        dynamicPinBatch.offsets[slotIndex] = localOffset;      // 使用局部偏移
        dynamicPinBatch.stiffnesses[slotIndex * 2] = 1f - pinStiffness;
        dynamicPinBatch.stiffnesses[slotIndex * 2 + 1] = 1f;

        var info = pinInfos[slotIndex];
        info.Collider = collider;
        info.LocalOffset = localOffset;
        
        particleToBatchIndex[particleSolverIndex] = slotIndex;
        dynamicPinBatch.activeConstraintCount++;
        
        if (enableDebugLogs) Debug.Log($"[PinDebugger] ---> ActivatePin: 在槽位 {slotIndex} 成功激活一个新Pin。粒子: {particleSolverIndex}, 碰撞体: {collider.name}", this);
    }

    private void DeactivatePin(int slotIndex)
    {
        // 采用 "Swap and Pop" 算法高效移除
        particleToBatchIndex.Remove(dynamicPinBatch.particleIndices[slotIndex]);
        dynamicPinBatch.activeConstraintCount--;
        int lastActiveIndex = dynamicPinBatch.activeConstraintCount;

        if (slotIndex < lastActiveIndex)
        {
            // 用最后一个元素的数据覆盖当前要删除的槽位
            int particleToMove = dynamicPinBatch.particleIndices[lastActiveIndex];
            dynamicPinBatch.particleIndices[slotIndex] = particleToMove;
            dynamicPinBatch.pinBodies[slotIndex] = dynamicPinBatch.pinBodies[lastActiveIndex];
            dynamicPinBatch.colliderIndices[slotIndex] = dynamicPinBatch.colliderIndices[lastActiveIndex];
            dynamicPinBatch.offsets[slotIndex] = dynamicPinBatch.offsets[lastActiveIndex];
            dynamicPinBatch.stiffnesses[slotIndex * 2] = dynamicPinBatch.stiffnesses[lastActiveIndex * 2];
            dynamicPinBatch.stiffnesses[slotIndex * 2 + 1] = dynamicPinBatch.stiffnesses[lastActiveIndex * 2 + 1];

            pinInfos[slotIndex].Collider = pinInfos[lastActiveIndex].Collider;
            pinInfos[slotIndex].LocalOffset = pinInfos[lastActiveIndex].LocalOffset;

            // 更新被移动的粒子在字典中的索引
            particleToBatchIndex[particleToMove] = slotIndex;
        }
        if (enableDebugLogs) Debug.Log($"[PinDebugger] ---> DeactivatePin: 正在停用槽位 {slotIndex} 的Pin。", this);
    }
    
    // --- 可视化与辅助方法 (无需修改) ---
    private void UpdateColors()
    {
        if (solver == null) return;
        RestoreAllParticleColors();
        for(int i = 0; i < dynamicPinBatch.activeConstraintCount; ++i)
        {
            int solverIndex = dynamicPinBatch.particleIndices[i];
            if(!originalParticleColors.ContainsKey(solverIndex))
            {
                originalParticleColors[solverIndex] = solver.colors[solverIndex];
            }
            solver.colors[solverIndex] = pinnedParticleColor;
        }
        solver.colors.Upload();
    }
    
    private void RestoreAllParticleColors()
    {
        if(originalParticleColors.Count > 0 && solver != null)
        {
            foreach(var p in originalParticleColors)
            {
                if(p.Key >= 0 && p.Key < solver.colors.count) solver.colors[p.Key] = p.Value;
            }
            solver.colors.Upload();
        }
        originalParticleColors.Clear();
    }
    
    private int GetParticleSolverIndexFromContact(Oni.Contact contact) { if(IsParticleFromOurSoftbody(contact.bodyA)) return contact.bodyA; if(IsParticleFromOurSoftbody(contact.bodyB)) return contact.bodyB; return -1; }
    private int GetColliderIndexFromContact(Oni.Contact contact) { return IsParticleFromOurSoftbody(contact.bodyA) ? contact.bodyB : contact.bodyA; }
    private bool IsParticleFromOurSoftbody(int particleSolverIndex) { if(solver == null || particleSolverIndex < 0 || particleSolverIndex >= solver.particleToActor.Length) return false; var p = solver.particleToActor[particleSolverIndex]; return p != null && p.actor == softbody; }
}