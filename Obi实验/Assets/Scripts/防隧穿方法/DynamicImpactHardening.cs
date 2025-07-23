using UnityEngine;
using Obi;
using System.Collections.Generic;

/// <summary>
/// 动态冲击硬化脚本 V1.0
/// 融合了动态Pin约束、形变检测、质量缩放和智能回弹释放机制，专门用于解决高速冲击下的软体穿透问题。
/// 
/// 工作流程:
/// 1. OnCollision: 当与指定Tag的物体碰撞时，立即为接触的粒子创建Pin约束，将其“钉”在碰撞体上。
/// 2. LateUpdate (每帧执行):
///    a. 计算整个软体的总形变程度。
///    b. 根据形变程度，通过AnimationCurve来动态增加软体的质量。
///    c. 【核心】比较当前帧与上一帧的形变量，一旦检测到形变开始减小（即进入“回弹”阶段），立即释放所有Pin约束。
///    d. 如果不再与目标物体碰撞，则将质量恢复到原始值。
/// </summary>
[RequireComponent(typeof(ObiSoftbody))]
public class DynamicImpactHardening : MonoBehaviour
{
    [Header("核心功能配置")]
    [Tooltip("只对拥有此Tag的物体加强碰撞。留空则对所有碰撞生效。")]
    public string colliderTag;
    [Tooltip("预分配的 Pin 约束池大小。")]
    public int pinPoolSize = 128;
    [Tooltip("Pin约束的硬度 (0-1)。将直接影响粒子'粘'在碰撞体上的强度。")]
    [Range(0f, 1f)]
    public float pinStiffness = 1f;

    [Header("形变与质量响应")]
    [Tooltip("用于将归一化的形变程度映射到质量的乘数。X轴(0-1)是形变程度，Y轴是质量乘数。")]
    public AnimationCurve massScaleCurve = AnimationCurve.Linear(0, 1, 1, 10);
    [Tooltip("开始改变质量的形变阈值，避免微小形变也触发质量变化。")]
    public float deformationThresholdForMassChange = 0.01f;
    [Tooltip("用于计算形变的缩放系数，调节形变值的敏感度。")]
    public float deformationScaling = 10f;

    [Header("回弹释放逻辑")]
    [Tooltip("检测到形变减小（开始回弹）时，释放Pin约束的敏感度。值越小，对回弹的检测越灵敏。建议0.05-0.2")]
    [Range(0.01f, 0.5f)]
    public float reboundDetectionSensitivity = 0.1f;

    [Header("可视化与调试")]
    [Tooltip("启用后，软体粒子颜色会根据形变程度变化。")]
    public bool enableDeformationColoring = true;
    [Tooltip("形变程度到颜色的渐变映射。")]
    public Gradient deformationColorGradient;

    // --- 内部状态变量 ---
    private ObiSoftbody softbody;
    private ObiSolver solver;
    private ObiPinConstraintsData pinConstraintsData;
    private ObiPinConstraintsBatch dynamicPinBatch;
    private ObiShapeMatchingConstraintsData shapeMatchingConstraintsData;

    // Pin约束管理 (借鉴 DynamicPinStiffener 的高效做法)
    private readonly Dictionary<int, int> particleToBatchIndex = new Dictionary<int, int>();

    // 状态追踪
    private float originalMassScale;
    private bool isMassModified = false;
    private float totalDeformationLastFrame = 0f;
    private readonly HashSet<ObiColliderBase> collidingCollidersThisFrame = new HashSet<ObiColliderBase>();

    // 可视化
    private readonly Dictionary<int, Color> originalParticleColors = new Dictionary<int, Color>();

    #region Unity生命周期与Obi事件
    void OnEnable()
    {
        softbody = GetComponent<ObiSoftbody>();
        softbody.OnBlueprintLoaded += OnBlueprintLoaded;
        if (softbody.isLoaded)
            OnBlueprintLoaded(softbody, softbody.sourceBlueprint);
    }

    void OnDisable()
    {
        softbody.OnBlueprintLoaded -= OnBlueprintLoaded;
        if (solver != null)
        {
            solver.OnCollision -= Solver_OnCollision;
        }
        RemoveDynamicBatch();
        RestoreOriginalMass();
        RestoreAllParticleColors();
    }

    private void OnBlueprintLoaded(ObiActor actor, ObiActorBlueprint blueprint)
    {
        // 获取形变约束以计算形变
        shapeMatchingConstraintsData = softbody.GetConstraintsByType(Oni.ConstraintType.ShapeMatching) as ObiShapeMatchingConstraintsData;
        if (shapeMatchingConstraintsData == null)
        {
            Debug.LogError("本脚本依赖 Obi Shape Matching Constraints 来计算形变。请为软体添加该组件。", this);
            enabled = false;
            return;
        }
        
        // 记录原始质量
        originalMassScale = softbody.massScale;

        SetupDynamicBatch();
        SubscribeToSolver();
    }

    private void SubscribeToSolver()
    {
        if (solver != null) solver.OnCollision -= Solver_OnCollision;
        solver = softbody.solver;
        if (solver != null) solver.OnCollision += Solver_OnCollision;
    }
    #endregion

    #region 核心逻辑 - LateUpdate
    void LateUpdate()
    {
        if (solver == null || !softbody.isLoaded || shapeMatchingConstraintsData == null) return;

        bool constraintsChanged = false;

        // 1. 计算当前帧的总形变
        float currentDeformation = CalculateTotalDeformation();

        // 2. 【核心】回弹检测与Pin约束释放
        // 如果当前形变明显小于上一帧，说明软体开始回弹
        if (particleToBatchIndex.Count > 0 && currentDeformation < totalDeformationLastFrame * (1 - reboundDetectionSensitivity))
        {
            Debug.Log("<color=cyan>检测到回弹，释放所有Pin约束！</color>");
            constraintsChanged = ReleaseAllPins();
        }
        
        // 3. 质量管理
        // 如果本帧有碰撞发生
        if (collidingCollidersThisFrame.Count > 0)
        {
            // 并且形变超过阈值
            if (currentDeformation > deformationThresholdForMassChange)
            {
                // 使用AnimationCurve计算质量乘数并应用
                float massMultiplier = massScaleCurve.Evaluate(Mathf.Clamp01(currentDeformation));
                softbody.massScale = originalMassScale * massMultiplier;
                isMassModified = true;
            }
        }
        else // 如果本帧没有任何目标碰撞
        {
            // 如果质量之前被修改过，则恢复
            if (isMassModified)
            {
                RestoreOriginalMass();
            }
        }

        // 4. 更新与清理
        if (constraintsChanged)
        {
            softbody.SetConstraintsDirty(Oni.ConstraintType.Pin);
        }

        if (enableDeformationColoring)
        {
            UpdateColors(currentDeformation);
        }

        // 更新上一帧形变值，为下一帧做准备
        totalDeformationLastFrame = currentDeformation;

        // 清理当前帧的碰撞记录
        collidingCollidersThisFrame.Clear();
    }
    #endregion

    #region 碰撞与Pin约束管理
    private void Solver_OnCollision(ObiSolver solver, ObiNativeContactList contacts)
    {
        if (contacts.count == 0 || dynamicPinBatch == null) return;

        bool needsUpdate = false;
        for (int i = 0; i < contacts.count; ++i)
        {
            if (dynamicPinBatch.activeConstraintCount >= pinPoolSize) break; // 池已满

            Oni.Contact contact = contacts[i];
            int particleSolverIndex = GetParticleSolverIndexFromContact(contact);
            if (particleSolverIndex == -1 || particleToBatchIndex.ContainsKey(particleSolverIndex)) continue;

            var otherCollider = ObiColliderWorld.GetInstance().colliderHandles[GetColliderIndexFromContact(contact)].owner;
            if (otherCollider == null || !otherCollider.gameObject.activeInHierarchy) continue;
            if (!string.IsNullOrEmpty(colliderTag) && !otherCollider.CompareTag(colliderTag)) continue;

            // 记录发生碰撞的碰撞体
            collidingCollidersThisFrame.Add(otherCollider);
            
            Matrix4x4 bindMatrix = otherCollider.transform.worldToLocalMatrix * solver.transform.localToWorldMatrix;
            Vector3 pinOffset = bindMatrix.MultiplyPoint3x4(solver.positions[particleSolverIndex]);
            if (float.IsNaN(pinOffset.x)) continue;

            ActivatePin(particleSolverIndex, otherCollider, pinOffset);
            needsUpdate = true;
        }

        if (needsUpdate)
        {
            softbody.SetConstraintsDirty(Oni.ConstraintType.Pin);
        }
    }

    private void ActivatePin(int particleSolverIndex, ObiColliderBase collider, Vector3 offset)
    {
        int slotIndex = dynamicPinBatch.activeConstraintCount;

        dynamicPinBatch.particleIndices[slotIndex] = particleSolverIndex;
        dynamicPinBatch.pinBodies[slotIndex] = collider.Handle;
        dynamicPinBatch.colliderIndices[slotIndex] = collider.Handle.index;
        dynamicPinBatch.offsets[slotIndex] = offset;
        dynamicPinBatch.stiffnesses[slotIndex * 2] = 1f - pinStiffness;
        dynamicPinBatch.stiffnesses[slotIndex * 2 + 1] = 1f;
        
        particleToBatchIndex[particleSolverIndex] = slotIndex;
        dynamicPinBatch.activeConstraintCount++;
    }

    private bool ReleaseAllPins()
    {
        if (particleToBatchIndex.Count == 0) return false;

        // 清空所有约束和追踪字典
        particleToBatchIndex.Clear();
        dynamicPinBatch.activeConstraintCount = 0;
        return true;
    }
    #endregion

    #region 形变计算
    private float CalculateTotalDeformation()
    {
        // 借鉴 DeformationToColors.cs 的逻辑
        float totalDeformation = 0;
        int activeClusters = 0;

        var dc = shapeMatchingConstraintsData;
        var sc = solver.GetConstraintsByType(Oni.ConstraintType.ShapeMatching) as ObiConstraints<ObiShapeMatchingConstraintsBatch>;

        if (dc != null && sc != null)
        {
            for (int j = 0; j < softbody.solverBatchOffsets[(int)Oni.ConstraintType.ShapeMatching].Count; ++j)
            {
                var batch = dc.batches[j] as ObiShapeMatchingConstraintsBatch;
                var solverBatch = sc.batches[j] as ObiShapeMatchingConstraintsBatch;
                int offset = softbody.solverBatchOffsets[(int)Oni.ConstraintType.ShapeMatching][j];

                for (int i = 0; i < batch.activeConstraintCount; i++)
                {
                    // 使用 Frobenius 范数来估计形变，减2是为了归一化（无形变时约为2）
                    float deformation = solverBatch.linearTransforms[offset + i].FrobeniusNorm() - 2;
                    if (deformation > 0)
                    {
                        totalDeformation += deformation;
                        activeClusters++;
                    }
                }
            }
        }
        
        // 返回平均形变并应用一个缩放系数
        if (activeClusters > 0)
        {
            return (totalDeformation / activeClusters) * deformationScaling;
        }
        return 0;
    }
    #endregion

    #region 质量与可视化
    private void RestoreOriginalMass()
    {
        softbody.massScale = originalMassScale;
        isMassModified = false;
        Debug.Log("<color=green>碰撞结束，恢复原始质量。</color>");
    }
    
    private void UpdateColors(float currentDeformation)
    {
        if (!enableDeformationColoring || solver == null) return;
        
        // 先恢复所有颜色，防止旧的颜色残留
        RestoreAllParticleColors();
        
        // 根据形变上色
        Color deformationColor = deformationColorGradient.Evaluate(Mathf.Clamp01(currentDeformation));

        // 将颜色应用到所有被Pin住的粒子，或整个软体上（可根据需求修改）
        for (int i = 0; i < softbody.solverIndices.count; ++i)
        {
            int solverIndex = softbody.solverIndices[i];
            if (solver.invMasses[solverIndex] > 0) // 只为非固定粒子上色
            {
                if (!originalParticleColors.ContainsKey(solverIndex))
                {
                    originalParticleColors[solverIndex] = solver.colors[solverIndex];
                }
                solver.colors[solverIndex] = Color.Lerp(solver.colors[solverIndex], deformationColor, 0.5f);
            }
        }
        
        solver.colors.Upload();
    }
    
    private void RestoreAllParticleColors()
    {
        if (originalParticleColors.Count > 0 && solver != null)
        {
            foreach(var p in originalParticleColors)
            {
                if (p.Key >= 0 && p.Key < solver.colors.count) 
                    solver.colors[p.Key] = p.Value;
            }
            solver.colors.Upload();
        }
        originalParticleColors.Clear();
    }
    #endregion
    
    #region 辅助与底层管理
    private void SetupDynamicBatch()
    {
        RemoveDynamicBatch();
        pinConstraintsData = softbody.GetConstraintsByType(Oni.ConstraintType.Pin) as ObiPinConstraintsData;
        if (pinConstraintsData == null) { Debug.LogError("请为软体添加 Obi Pin Constraints 组件。", this); enabled = false; return; }

        dynamicPinBatch = new ObiPinConstraintsBatch();
        for (int i = 0; i < pinPoolSize; ++i)
        {
            dynamicPinBatch.AddConstraint(-1, null, Vector3.zero, Quaternion.identity, 1f, 1f);
        }
        dynamicPinBatch.activeConstraintCount = 0;
        pinConstraintsData.AddBatch(dynamicPinBatch);
    }
    private void RemoveDynamicBatch()
    {
        if (pinConstraintsData != null && dynamicPinBatch != null)
        {
            pinConstraintsData.RemoveBatch(dynamicPinBatch);
            if(softbody.isLoaded) softbody.SetConstraintsDirty(Oni.ConstraintType.Pin);
        }
    }
    private int GetParticleSolverIndexFromContact(Oni.Contact contact) { if (IsParticleFromOurSoftbody(contact.bodyA)) return contact.bodyA; if (IsParticleFromOurSoftbody(contact.bodyB)) return contact.bodyB; return -1; }
    private int GetColliderIndexFromContact(Oni.Contact contact) { return IsParticleFromOurSoftbody(contact.bodyA) ? contact.bodyB : contact.bodyA; }
    private bool IsParticleFromOurSoftbody(int particleSolverIndex) { if (solver == null || particleSolverIndex < 0 || particleSolverIndex >= solver.particleToActor.Length) return false; var p = solver.particleToActor[particleSolverIndex]; return p != null && p.actor == softbody; }
    #endregion
}