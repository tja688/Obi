using UnityEngine;
using Obi;
using System.Collections.Generic;

/// <summary>
/// 动态冲击硬化脚本 V1.1
/// 
/// V1.1 更新日志:
/// 1. 【新增】Pin约束冷却机制：在回弹释放所有Pin后，会进入冷却状态。在该状态下，即使有新的碰撞也不会创建Pin。
///    此状态会一直持续，直到软体质量完全恢复到初始值后，系统才会重置，准备迎接下一次冲击。这完美实现了“Pin只挡第一下”的逻辑。
/// 2. 【优化】质量配置：移除了AnimationCurve，改为更直观的“最大质量”和“达到最大质量的形变量”配置。
/// 3. 【新增】调试监控：在Inspector中实时显示当前软体的归一化形变值，便于观察和调试。
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
    [Tooltip("冲击时软体能达到的最大质量值。")]
    public float maxMassOnImpact = 50f;
    [Tooltip("达到最大质量所需的形变值。形变达到此值时，质量将等于'最大质量值'。")]
    public float deformationForMaxMass = 0.5f;
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
    [Tooltip("【只读】当前计算出的实时归一化形变值(0-1)。"), Range(0, 1)]
    public float currentNormalizedDeformation;

    // --- 内部状态变量 ---
    private ObiSoftbody softbody;
    private ObiSolver solver;
    private ObiPinConstraintsData pinConstraintsData;
    private ObiPinConstraintsBatch dynamicPinBatch;
    private ObiShapeMatchingConstraintsData shapeMatchingConstraintsData;

    private readonly Dictionary<int, int> particleToBatchIndex = new Dictionary<int, int>();

    // 状态追踪
    private float originalMassScale;
    private bool isMassModified = false;
    private bool isPinCooldownActive = false; // V1.1 新增：Pin约束冷却状态
    private float totalDeformationLastFrame = 0f;
    private readonly HashSet<ObiColliderBase> collidingCollidersThisFrame = new HashSet<ObiColliderBase>();

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
        shapeMatchingConstraintsData = softbody.GetConstraintsByType(Oni.ConstraintType.ShapeMatching) as ObiShapeMatchingConstraintsData;
        if (shapeMatchingConstraintsData == null)
        {
            Debug.LogError("本脚本依赖 Obi Shape Matching Constraints 来计算形变。请为软体添加该组件。", this);
            enabled = false;
            return;
        }
        
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
        
        float currentDeformation = CalculateTotalDeformation();
        // V1.1 更新Debug监视器
        currentNormalizedDeformation = Mathf.Clamp01(currentDeformation / deformationForMaxMass);

        // 1. 【核心】回弹检测与Pin约束释放
        if (!isPinCooldownActive && particleToBatchIndex.Count > 0 && currentDeformation < totalDeformationLastFrame * (1 - reboundDetectionSensitivity))
        {
            Debug.Log("<color=cyan>检测到回弹，释放所有Pin约束！</color>");
            constraintsChanged = ReleaseAllPins();
            // V1.1 新增：激活Pin冷却
            isPinCooldownActive = true; 
            Debug.Log("<color=orange>Pin约束冷却已激活，待质量恢复后重置。</color>");
        }
        
        // 2. 质量管理
        if (collidingCollidersThisFrame.Count > 0)
        {
            if (currentDeformation > deformationThresholdForMassChange)
            {
                // V1.1 修改：使用更直观的Lerp进行质量计算
                float t = currentNormalizedDeformation; // 使用上面计算好的归一化值
                softbody.SetMass(Mathf.Lerp(originalMassScale, maxMassOnImpact, t));
                isMassModified = true;
            }
        }
        else 
        {
            if (isMassModified)
            {
                RestoreOriginalMass();
            }
        }

        if (constraintsChanged)
        {
            softbody.SetConstraintsDirty(Oni.ConstraintType.Pin);
        }

        if (enableDeformationColoring)
        {
            UpdateColors(currentNormalizedDeformation);
        }

        totalDeformationLastFrame = currentDeformation;
        collidingCollidersThisFrame.Clear();
    }
    #endregion

    #region 碰撞与Pin约束管理
    private void Solver_OnCollision(ObiSolver solver, ObiNativeContactList contacts)
    {
        // V1.1 新增：如果Pin处于冷却状态，则直接跳过，不创建任何新约束
        if (isPinCooldownActive) return;

        if (contacts.count == 0 || dynamicPinBatch == null) return;

        bool needsUpdate = false;
        for (int i = 0; i < contacts.count; ++i)
        {
            if (dynamicPinBatch.activeConstraintCount >= pinPoolSize) break; 

            Oni.Contact contact = contacts[i];
            int particleSolverIndex = GetParticleSolverIndexFromContact(contact);
            if (particleSolverIndex == -1 || particleToBatchIndex.ContainsKey(particleSolverIndex)) continue;

            var otherCollider = ObiColliderWorld.GetInstance().colliderHandles[GetColliderIndexFromContact(contact)].owner;
            if (otherCollider == null || !otherCollider.gameObject.activeInHierarchy) continue;
            if (!string.IsNullOrEmpty(colliderTag) && !otherCollider.CompareTag(colliderTag)) continue;

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
        particleToBatchIndex.Clear();
        dynamicPinBatch.activeConstraintCount = 0;
        return true;
    }
    #endregion

    #region 形变计算
    private float CalculateTotalDeformation()
    {
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
                    float deformation = solverBatch.linearTransforms[offset + i].FrobeniusNorm() - 2;
                    if (deformation > 0)
                    {
                        totalDeformation += deformation;
                        activeClusters++;
                    }
                }
            }
        }
        
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
        
        // V1.1 新增：当质量恢复时，解除Pin的冷却状态，让系统可以响应下一次冲击
        if (isPinCooldownActive)
        {
            isPinCooldownActive = false;
            Debug.Log("<color=lime>质量已恢复，Pin约束系统已重置，准备下一次冲击。</color>");
        }
    }
    
    private void UpdateColors(float normalizedDeformation)
    {
        if (!enableDeformationColoring || solver == null) return;
        
        RestoreAllParticleColors();
        
        Color deformationColor = deformationColorGradient.Evaluate(normalizedDeformation);

        for (int i = 0; i < softbody.solverIndices.count; ++i)
        {
            int solverIndex = softbody.solverIndices[i];
            if (solver.invMasses[solverIndex] > 0)
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