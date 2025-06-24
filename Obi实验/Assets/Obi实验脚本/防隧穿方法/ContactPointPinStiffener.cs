using UnityEngine;
using Obi;
using System.Collections.Generic;

/// <summary>
/// (V2 - 接触点锚定版)
/// 根据用户提出的新思路进行改进。
/// 此脚本不再将软体粒子Pin到碰撞体上，而是Pin到碰撞发生时的“世界空间位置”。
/// 这创建了一个临时的、静止的锚点来抵抗穿透，解决了碰撞体移动时对软体产生不自然拉扯的问题。
/// 当粒子因软体自身运动而远离其静止锚点时，约束会自然释放。
/// </summary>
[RequireComponent(typeof(ObiSoftbody))]
public class ContactPointPinStiffener : MonoBehaviour
{
    [Header("核心功能配置")]
    [Tooltip("只对拥有此Tag的物体加强碰撞。留空则对所有碰撞生效。")]
    public string colliderTag;
    [Tooltip("预分配的 Pin 约束池大小。")]
    public int pinPoolSize = 64;

    [Header("固定模式与强度")]
    [Tooltip("Pin约束的硬度 (0-1)。将直接影响粒子'粘'在锚点上的强度。")]
    [Range(0f, 1f)]
    public float pinStiffness = 1f;

    [Header("柔化挣脱 (可选)")]
    [Tooltip("启用后，当粒子远离其锚定点时，会自动释放约束。")]
    public bool enableDisengagement = true; // 默认为 true，因为此模式下该功能至关重要
    [Tooltip("当粒子与其锚定点的距离超过此值时，释放Pin约束。")]
    public float disengagementDistance = 0.1f;

    [Header("可视化与调试")]
    public bool enableVisualization = true;
    public Color pinnedParticleColor = Color.red; // 使用不同颜色以作区分

    // *** 关键修改 1: C# 层的状态追踪器不再需要存储Collider，只需要世界坐标锚点 ***
    private class PinInfo { public Vector3 WorldAnchor; }
    private PinInfo[] pinInfos;
    
    private ObiSoftbody softbody;
    private ObiSolver solver;
    private ObiPinConstraintsData pinConstraintsData;
    private ObiPinConstraintsBatch dynamicPinBatch;
    
    // 底层数据结构保持不变，用于高效管理
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
            // 预分配内存，此时它们都是无效的。
            // *** 关键修改 2: Pin的目标collider始终为null，offset此时无意义 ***
            dynamicPinBatch.AddConstraint(-1, null, Vector3.zero, Quaternion.identity, 1f, 1f);
            pinInfos[i] = new PinInfo();
        }
        
        // 活动约束数量从0开始
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
            int particleSolverIndex = dynamicPinBatch.particleIndices[i];
            
            // *** 关键修改 3: 挣脱逻辑不再关心碰撞体，只关心粒子与世界锚点的距离 ***
            Vector3 currentParticlePos = solver.positions[particleSolverIndex];
            Vector3 pinWorldAnchor = pinInfos[i].WorldAnchor;

            if (Vector3.SqrMagnitude(currentParticlePos - pinWorldAnchor) > disengagementDistance * disengagementDistance)
            {
                DeactivatePin(i);
                needsUpdate = true;
            }
        }

        if (needsUpdate)
        {
            softbody.SetConstraintsDirty(Oni.ConstraintType.Pin);
            UpdateColors();
        }
    }

    private void Solver_OnCollision(ObiSolver solver, ObiNativeContactList contacts)
    {
        if (contacts.count == 0 || dynamicPinBatch == null) return;

        bool needsUpdate = false;
        for (int i = 0; i < contacts.count; ++i)
        {
            if (dynamicPinBatch.activeConstraintCount >= pinPoolSize)
            {
                 Debug.LogWarning("Pin约束池已满，无法创建新的约束。");
                 break;
            }

            Oni.Contact contact = contacts[i];
            int particleSolverIndex = GetParticleSolverIndexFromContact(contact);
            if (particleSolverIndex == -1 || particleToBatchIndex.ContainsKey(particleSolverIndex)) continue;
            
            var otherCollider = ObiColliderWorld.GetInstance().colliderHandles[GetColliderIndexFromContact(contact)].owner;
            if (otherCollider == null || !otherCollider.gameObject.activeInHierarchy) continue;
            if (!string.IsNullOrEmpty(colliderTag) && !otherCollider.CompareTag(colliderTag)) continue;
            
            // *** 关键修改 4: 核心逻辑 - 获取碰撞点的世界坐标作为锚点 ***
            // contact.point 是碰撞在世界空间中的确切位置。这就是我们需要的静态锚点。
            Vector3 pinAnchorWorldPosition = contact.pointB;
            
            // 传入锚点，激活约束
            ActivatePin(particleSolverIndex, pinAnchorWorldPosition);
            needsUpdate = true;
        }

        if (needsUpdate)
        {
            softbody.SetConstraintsDirty(Oni.ConstraintType.Pin);
            UpdateColors(); 
        }
    }

    // *** 关键修改 5: 激活逻辑不再需要Collider和Offset，而是需要世界坐标锚点 ***
    private void ActivatePin(int particleSolverIndex, Vector3 worldAnchor)
    {
        int slotIndex = dynamicPinBatch.activeConstraintCount;

        dynamicPinBatch.particleIndices[slotIndex] = particleSolverIndex;
        
        // Pin到世界坐标的关键：pinBody 设置为 null (或无效句柄), offset 设置为世界坐标
        dynamicPinBatch.pinBodies[slotIndex] = null; 
        dynamicPinBatch.colliderIndices[slotIndex] = -1; // -1 表示没有有效的collider
        dynamicPinBatch.offsets[slotIndex] = worldAnchor;
        
        // 设置硬度
        dynamicPinBatch.stiffnesses[slotIndex * 2] = 1f - pinStiffness; 
        dynamicPinBatch.stiffnesses[slotIndex * 2 + 1] = 1f;

        // 在C#层追踪锚点信息
        pinInfos[slotIndex].WorldAnchor = worldAnchor;
        
        // 更新追踪字典和活动数量
        particleToBatchIndex[particleSolverIndex] = slotIndex;
        dynamicPinBatch.activeConstraintCount++;
    }

    // "Swap and Pop" 停用逻辑
    private void DeactivatePin(int slotIndex)
    {
        particleToBatchIndex.Remove(dynamicPinBatch.particleIndices[slotIndex]);
        dynamicPinBatch.activeConstraintCount--;
        
        int lastActiveIndex = dynamicPinBatch.activeConstraintCount;
        if (slotIndex < lastActiveIndex)
        {
            // 复制批处理数据
            dynamicPinBatch.particleIndices[slotIndex] = dynamicPinBatch.particleIndices[lastActiveIndex];
            dynamicPinBatch.pinBodies[slotIndex] = dynamicPinBatch.pinBodies[lastActiveIndex]; // 始终是 null
            dynamicPinBatch.colliderIndices[slotIndex] = dynamicPinBatch.colliderIndices[lastActiveIndex]; // 始终是 -1
            dynamicPinBatch.offsets[slotIndex] = dynamicPinBatch.offsets[lastActiveIndex];
            dynamicPinBatch.stiffnesses[slotIndex * 2] = dynamicPinBatch.stiffnesses[lastActiveIndex * 2];
            dynamicPinBatch.stiffnesses[slotIndex * 2 + 1] = dynamicPinBatch.stiffnesses[lastActiveIndex * 2 + 1];

            // *** 关键修改 6: 复制C#追踪信息 ***
            pinInfos[slotIndex].WorldAnchor = pinInfos[lastActiveIndex].WorldAnchor;

            // 更新被移动的粒子在字典中的索引
            particleToBatchIndex[dynamicPinBatch.particleIndices[slotIndex]] = slotIndex;
        }
    }
    
    // --- 颜色和辅助方法 (无重大修改) ---
    private void UpdateColors()
    {
        if (!enableVisualization || solver == null) return;
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