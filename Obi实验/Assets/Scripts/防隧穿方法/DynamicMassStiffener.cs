using UnityEngine;
using Obi;

/// <summary>
/// 动态质量强化器 (V5 - 数据同步修正版)
/// 修正了因缓存求解器约束数据而导致的索引越界问题，
/// 确保在每一物理帧都使用最新的数据进行计算。
/// </summary>
[RequireComponent(typeof(ObiSoftbody))]
public class DynamicMassStiffener_Final_V5 : MonoBehaviour
{
    [Header("核心配置")]
    [Tooltip("粒子在无形变时的基础质量。")]
    public float minMass = 1f;
    [Tooltip("粒子在承受最大形变时能达到的最高质量。")]
    public float maxMass = 100f;
    [Tooltip("形变到质量的映射灵敏度。值越大，轻微的形变就能导致越大的质量变化。")]
    public float deformationSensitivity = 10f;
    [Tooltip("质量变化的平滑度。值越小，质量变化越快；值越大，变化越平滑。")]
    [Range(0.01f, 1.0f)]
    public float smoothing = 0.1f;

    private ObiSoftbody softbody;
    private ObiSolver solver;
    // 【修正】移除了对 actor 级别约束的引用，因为我们只需要在循环中访问 solver 级别的数据
    // private ObiConstraints<ObiShapeMatchingConstraintsBatch> shapeMatchingConstraints; 
    
    private bool isInitialized = false;

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
            solver.OnSimulationStart -= OnSimulationStartUpdate;
        }
        isInitialized = false;
    }

    private void OnBlueprintLoaded(ObiActor actor, ObiActorBlueprint blueprint)
    {
        // 确保 softbody 确实有 ShapeMatchingConstraints
        if (softbody.GetConstraintsByType(Oni.ConstraintType.ShapeMatching) == null)
        {
            Debug.LogError("本脚本依赖 Obi Shape Matching Constraints。请为软体添加该组件。", this);
            enabled = false;
            return;
        }

        solver = softbody.solver;
        if (solver != null)
        {
            solver.OnSimulationStart -= OnSimulationStartUpdate;
            solver.OnSimulationStart += OnSimulationStartUpdate;
        }
        isInitialized = true;
    }
    #endregion

    #region 核心逻辑
    
    private void OnSimulationStartUpdate(ObiSolver s, float timeToSimulate, float substepTime)
    {
        if (!isInitialized) return;

        // 【核心修正】在每次更新时，都从 Solver 重新获取最新的约束数据。
        var solverShapeMatchingConstraints = solver.GetConstraintsByType(Oni.ConstraintType.ShapeMatching) as ObiConstraints<ObiShapeMatchingConstraintsBatch>;
        var actorShapeMatchingConstraints = softbody.GetConstraintsByType(Oni.ConstraintType.ShapeMatching) as ObiConstraints<ObiShapeMatchingConstraintsBatch>;

        // 如果在这一帧获取不到，则直接返回，避免错误。
        if (solverShapeMatchingConstraints == null || actorShapeMatchingConstraints == null) return;

        // 【核心修正】使用 Actor 的批处理数量进行迭代，这是安全的。
        for (int i = 0; i < actorShapeMatchingConstraints.batches.Count; ++i)
        {
            var actorBatch = actorShapeMatchingConstraints.batches[i];
            
            // 检查索引是否安全，这是一个额外的保护层。
            if (i >= solverShapeMatchingConstraints.batches.Count) continue;
            var solverBatch = solverShapeMatchingConstraints.batches[i]; 

            for (int j = 0; j < actorBatch.activeConstraintCount; ++j)
            {
                // 检查索引是否安全
                if(i >= softbody.solverBatchOffsets[(int)Oni.ConstraintType.ShapeMatching].Count) continue;
                int constraintSolverIndex = softbody.solverBatchOffsets[(int)Oni.ConstraintType.ShapeMatching][i] + j;
                if (constraintSolverIndex >= solverBatch.linearTransforms.count) continue;

                // 1. 使用上一帧的形变数据计算目标质量
                float norm = solverBatch.linearTransforms[constraintSolverIndex].FrobeniusNorm();
                float deformation = Mathf.Max(0, (norm / 1.7320508f) - 1);
                
                float targetMass = Mathf.Lerp(minMass, maxMass, Mathf.Clamp01(deformation * deformationSensitivity));
                float targetInvMass = (targetMass > 0) ? 1.0f / targetMass : 0f;

                // 2. 迭代并修改粒子质量
                int firstParticle = actorBatch.firstIndex[j];
                int numParticlesInCluster = actorBatch.numIndices[j];
                for (int k = 0; k < numParticlesInCluster; ++k)
                {
                    int particleActorIndex = actorBatch.particleIndices[firstParticle + k];
                    
                    if (particleActorIndex >= softbody.solverIndices.count) continue;
                    int particleSolverIndex = softbody.solverIndices[particleActorIndex];
                    if (particleSolverIndex >= solver.invMasses.count) continue;
                    
                    float currentInvMass = solver.invMasses[particleSolverIndex];
                    float newInvMass = Mathf.Lerp(currentInvMass, targetInvMass, smoothing);
                    
                    solver.invMasses[particleSolverIndex] = newInvMass;
                }
            }
        }
    }
    #endregion
}