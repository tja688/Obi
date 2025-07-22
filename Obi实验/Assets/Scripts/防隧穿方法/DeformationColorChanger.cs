using UnityEngine;
using Obi;
using System.Collections.Generic;

/// <summary>
/// 形变颜色更改器 (V1 - 稳定基线版)
/// 目标：建立一个绝对稳定的数据读取和写入基线。
/// 功能：根据粒子簇的形变程度，平滑地修改其内部粒子的颜色。
/// 这将验证我们访问形变数据和写入粒子属性的通路是否正确，且不会因物理反馈而崩溃。
/// </summary>
[RequireComponent(typeof(ObiSoftbody))]
public class DeformationColorChanger : MonoBehaviour
{
    [Header("颜色配置")]
    [Tooltip("粒子在无形变时应呈现的颜色。")]
    public Color noDeformationColor = Color.blue;

    [Tooltip("粒子在承受最大形变时应呈现的颜色。")]
    public Color maxDeformationColor = Color.red;

    [Header("灵敏度与平滑")]
    [Tooltip("形变到颜色映射的灵敏度。值越大，轻微的形变就能导致越明显的颜色变化。")]
    public float deformationSensitivity = 5f;

    [Tooltip("颜色变化的平滑度。值越小，颜色变化越快；值越大，变化越平滑。")]
    [Range(0.01f, 1f)]
    public float colorSmoothing = 0.1f;

    private ObiSoftbody softbody;
    private ObiSolver solver;
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

        // 每次更新时，都从 Solver 和 Actor 重新获取最新的约束数据，确保同步。
        var solverShapeMatchingConstraints = solver.GetConstraintsByType(Oni.ConstraintType.ShapeMatching) as ObiConstraints<ObiShapeMatchingConstraintsBatch>;
        var actorShapeMatchingConstraints = softbody.GetConstraintsByType(Oni.ConstraintType.ShapeMatching) as ObiConstraints<ObiShapeMatchingConstraintsBatch>;

        if (solverShapeMatchingConstraints == null || actorShapeMatchingConstraints == null) return;
        
        // 【关键修正】获取当前 Actor 在 Solver 的所有批处理中的索引列表。
        // 这解决了之前版本中错误的索引对应问题。
        List<int> batchIndicesInSolver = softbody.solverBatchOffsets[(int)Oni.ConstraintType.ShapeMatching];
        if (batchIndicesInSolver == null) return;


        // 1. 遍历当前 Actor 自己的约束批处理 (Batches)
        for (int i = 0; i < actorShapeMatchingConstraints.batches.Count; ++i)
        {
            var actorBatch = actorShapeMatchingConstraints.batches[i];
            
            // 安全检查：确保我们有对应的 Solver 批处理索引
            if (i >= batchIndicesInSolver.Count) continue;
            
            // 【关键修正】使用索引列表来找到 Actor Batch 在 Solver Batches 中对应的那个 Batch。
            int solverBatchIndex = batchIndicesInSolver[i];
            
            // 安全检查：确保 Solver 批处理索引有效
            if (solverBatchIndex >= solverShapeMatchingConstraints.batches.Count) continue;
            var solverBatch = solverShapeMatchingConstraints.batches[solverBatchIndex];

            // 2. 遍历批处理中的每一个约束 (粒子簇)
            for (int j = 0; j < actorBatch.activeConstraintCount; ++j)
            {
                // 安全检查: 确保约束索引 j 在 solverBatch 的形变数据中有效。
                // 我们假设每个 solverBatch 的 linearTransforms 是从 0 开始计数的。
                if (j >= solverBatch.linearTransforms.count) continue;

                // 2.1. 计算该粒子簇的形变量
                float norm = solverBatch.linearTransforms[j].FrobeniusNorm();
                float deformation = Mathf.Max(0, (norm / 1.7320508f) - 1); // 标准化，0代表无形变
                
                // 2.2. 根据形变量计算出目标颜色
                float t = Mathf.Clamp01(deformation * deformationSensitivity);
                Color targetColor = Color.Lerp(noDeformationColor, maxDeformationColor, t);

                // 2.3. 将目标颜色应用到该簇内的所有粒子
                int firstParticle = actorBatch.firstIndex[j];
                int numParticlesInCluster = actorBatch.numIndices[j];
                for (int k = 0; k < numParticlesInCluster; ++k)
                {
                    int particleActorIndex = actorBatch.particleIndices[firstParticle + k];
                    
                    // 获取 Solver 中的粒子索引，并进行安全检查
                    if (particleActorIndex >= softbody.solverIndices.count) continue;
                    int particleSolverIndex = softbody.solverIndices[particleActorIndex];
                    if (particleSolverIndex >= solver.colors.count) continue;
                    
                    // 读取当前颜色，进行平滑插值，再写回
                    Color currentColor = solver.colors[particleSolverIndex];
                    solver.colors[particleSolverIndex] = Color.Lerp(currentColor, targetColor, colorSmoothing);
                }
            }
        }
    }
    #endregion
}