using UnityEngine;
using Obi;
using System.Collections.Generic;

/// <summary>
/// (V1.1) 基于形变和状态机的动态固定脚本（加入Cooldown最小时间防抖机制）。
/// 当软体侵入静态障碍物时，通过动态Pin约束进行固化以抵抗挤压。
/// 当软体形变恢复正常（意图脱离）时，智能释放所有约束并进入冷却期，防止反复粘连。
/// </summary>
public class DeformationBasedPinning : MonoBehaviour
{
    private enum PinningState { Idle, Intruding, Cooldown }
    private PinningState currentState = PinningState.Idle;

    [Header("核心功能配置")]
    public string targetColliderTag = "Obstacle";
    public int pinPoolSize = 128;

    [Header("固定与释放逻辑")]
    [Range(0f, 1f)]
    public float pinStiffness = 1f;
    public float baselineDeformationThreshold = 0.01f;
    [Tooltip("Cooldown 最短持续时间（秒），防止状态快速跳变。")]
    public float minCooldownDuration = 0.2f;

    [Header("可视化与调试")]
    public bool enableVisualization = true;
    public Color pinnedParticleColor = Color.red;
    public bool enableDebugGUI = true;

    private ObiSoftbody softbody;
    private ObiSolver solver;
    private ObiPinConstraintsData pinConstraintsData;
    private ObiPinConstraintsBatch dynamicPinBatch;
    private Dictionary<int, int> particleToBatchIndex = new Dictionary<int, int>();
    private readonly Dictionary<int, Color> originalParticleColors = new Dictionary<int, Color>();

    private float currentOverallDeformation = 0f;
    private int lastContactFrame = -1;
    private float cooldownStartTime = 0f;

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
        if (pinConstraintsData == null) { Debug.LogError($"[{this.name}] 脚本需要 Obi Pin Constraints 组件。", this); enabled = false; return; }

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
        if (!softbody.isLoaded || dynamicPinBatch == null) return;

        CalculateOverallDeformation();

        switch (currentState)
        {
            case PinningState.Intruding:
                if (currentOverallDeformation < baselineDeformationThreshold)
                {
                    ReleaseAllPins();
                    cooldownStartTime = Time.time;
                    currentState = PinningState.Cooldown;
                }
                break;

            case PinningState.Cooldown:
                if (Time.frameCount > lastContactFrame && Time.time - cooldownStartTime >= minCooldownDuration)
                {
                    currentState = PinningState.Idle;
                }
                break;
        }
    }

    private void Solver_OnCollision(ObiSolver solver, ObiNativeContactList contacts)
    {
        if (contacts.count == 0 || dynamicPinBatch == null) return;

        bool needsPinUpdate = false;
        for (int i = 0; i < contacts.count; ++i)
        {
            Oni.Contact contact = contacts[i];

            var otherCollider = ObiColliderWorld.GetInstance().colliderHandles[GetColliderIndexFromContact(contact)].owner;
            if (otherCollider == null || !otherCollider.CompareTag(targetColliderTag)) continue;

            lastContactFrame = Time.frameCount;

            if (currentState == PinningState.Cooldown) continue;
            if (currentState == PinningState.Idle) currentState = PinningState.Intruding;

            if (dynamicPinBatch.activeConstraintCount >= pinPoolSize)
            {
                Debug.LogWarning("Pin约束池已满，无法创建新的约束。");
                break;
            }

            int particleSolverIndex = GetParticleSolverIndexFromContact(contact);
            if (particleSolverIndex != -1 && !particleToBatchIndex.ContainsKey(particleSolverIndex))
            {
                Matrix4x4 bindMatrix = otherCollider.transform.worldToLocalMatrix * solver.transform.localToWorldMatrix;
                Vector3 pinOffset = bindMatrix.MultiplyPoint3x4(solver.positions[particleSolverIndex]);
                if (float.IsNaN(pinOffset.x)) continue;

                ActivatePin(particleSolverIndex, otherCollider, pinOffset);
                needsPinUpdate = true;
            }
        }

        if (needsPinUpdate)
        {
            softbody.SetConstraintsDirty(Oni.ConstraintType.Pin);
            UpdateColors();
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

    private void ReleaseAllPins()
    {
        if (dynamicPinBatch == null || dynamicPinBatch.activeConstraintCount == 0) return;
        dynamicPinBatch.activeConstraintCount = 0;
        particleToBatchIndex.Clear();
        softbody.SetConstraintsDirty(Oni.ConstraintType.Pin);
        RestoreAllParticleColors();
    }

    private void CalculateOverallDeformation()
    {
        var dc = softbody.GetConstraintsByType(Oni.ConstraintType.ShapeMatching) as ObiConstraints<ObiShapeMatchingConstraintsBatch>;
        if (dc == null || dc.batches.Count == 0) { currentOverallDeformation = 0; return; }

        var sc = softbody.solver.GetConstraintsByType(Oni.ConstraintType.ShapeMatching) as ObiConstraints<ObiShapeMatchingConstraintsBatch>;
        if (sc == null) { currentOverallDeformation = 0; return; }

        float totalDeformation = 0;
        int activeClusters = 0;

        for (int j = 0; j < dc.batches.Count; ++j)
        {
            var batch = dc.batches[j];
            var solverBatch = sc.batches[j];
            activeClusters += batch.activeConstraintCount;

            for (int i = 0; i < batch.activeConstraintCount; i++)
            {
                int offset = softbody.solverBatchOffsets[(int)Oni.ConstraintType.ShapeMatching][j];
                totalDeformation += Mathf.Abs(solverBatch.linearTransforms[offset + i].FrobeniusNorm() - 2);
            }
        }

        currentOverallDeformation = (activeClusters > 0) ? (totalDeformation / activeClusters) : 0;
    }

    private void OnGUI()
    {
        if (!enableDebugGUI) return;
        GUILayout.Window(this.GetInstanceID(), new Rect(20, 100, 300, 120), (id) =>
        {
            GUI.color = Color.white;
            GUILayout.Label("<b>状态机调试信息</b>");
            GUILayout.Label($"当前状态: <color=yellow>{currentState}</color>");
            GUILayout.Label($"当前形变值: {currentOverallDeformation:F4}");
            GUILayout.Label($"已固定粒子数: {dynamicPinBatch?.activeConstraintCount ?? 0}");
            GUI.DragWindow();
        }, "形变固化脚本");
    }

    private void UpdateColors()
    {
        if (!enableVisualization || solver == null) return;
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
}
