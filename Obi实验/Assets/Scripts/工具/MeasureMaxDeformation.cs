using UnityEngine;
using Obi; // 确保引用Obi命名空间

// 强制要求此脚本附加的游戏对象上必须有ObiSoftbody组件
[RequireComponent(typeof(ObiSoftbody))]
public class MeasureMaxDeformation : MonoBehaviour
{
    // 私有变量
    private ObiSoftbody softbody;
    private bool isExperimentRunning = false;
    private float maxDeformationRecorded = 0f;

    void Awake()
    {
        // 获取ObiSoftbody组件的引用
        softbody = GetComponent<ObiSoftbody>();
        Debug.Log("准备就绪：按 'S' 开始记录最大形变，按 'E' 结束并报告结果。");
    }

    // 推荐在OnEnable/OnDisable中订阅和取消订阅事件
    void OnEnable()
    {
        if (softbody != null)
        {
            // 订阅Obi求解器完成插值计算后的事件
            softbody.OnInterpolate += FindMaxDeformation;
        }
    }

    void OnDisable()
    {
        if (softbody != null)
        {
            // 取消订阅，防止内存泄漏
            softbody.OnInterpolate -= FindMaxDeformation;
        }
    }

    void Update()
    {
        // 按 'S' 键并且当前没有实验在进行
        if (Input.GetKeyDown(KeyCode.S) && !isExperimentRunning)
        {
            StartExperiment();
        }

        // 按 'E' 键并且当前有实验正在进行
        if (Input.GetKeyDown(KeyCode.E) && isExperimentRunning)
        {
            EndExperiment();
        }
    }

    /// <summary>
    /// 开始实验
    /// </summary>
    private void StartExperiment()
    {
        isExperimentRunning = true;
        // 重置上一轮实验记录的最大值
        maxDeformationRecorded = 0f;
        Debug.Log("====== 实验开始：正在记录形变值... ======");
    }

    /// <summary>
    /// 结束实验并报告结果
    /// </summary>
    private void EndExperiment()
    {
        isExperimentRunning = false;
        Debug.Log($"====== 实验结束。======\n从开始到结束，记录到的最大形变值为: {maxDeformationRecorded:F5}");
    }

    /// <summary>
    /// 在每个物理插值步骤中计算形变，并更新最大值
    /// </summary>
    private void FindMaxDeformation(ObiActor actor, float stepTime, float substepTime)
    {
        // 如果实验没有在进行，则直接返回，不进行任何计算
        if (!isExperimentRunning)
        {
            return;
        }

        // --- 以下是从之前脚本中借鉴的核心计算逻辑 ---

        // 获取形变约束
        var dc = softbody.GetConstraintsByType(Oni.ConstraintType.ShapeMatching) as ObiConstraints<ObiShapeMatchingConstraintsBatch>;
        var sc = softbody.solver.GetConstraintsByType(Oni.ConstraintType.ShapeMatching) as ObiConstraints<ObiShapeMatchingConstraintsBatch>;

        if (dc != null && sc != null)
        {
            float totalDeformationThisStep = 0;
            int sampleCountThisStep = 0;

            // 遍历所有约束批处理
            for (int j = 0; j < softbody.solverBatchOffsets[(int)Oni.ConstraintType.ShapeMatching].Count; ++j)
            {
                var solverBatch = sc.batches[j] as ObiShapeMatchingConstraintsBatch;
                for (int i = 0; i < solverBatch.activeConstraintCount; i++)
                {
                    int offset = softbody.solverBatchOffsets[(int)Oni.ConstraintType.ShapeMatching][j];
                    
                    // 使用弗罗贝尼乌斯范数 (Frobenius Norm) 来估算形变程度
                    float currentDeformation = solverBatch.linearTransforms[offset + i].FrobeniusNorm() - 2;
                    
                    totalDeformationThisStep += currentDeformation;
                    sampleCountThisStep++;
                }
            }

            // 计算当前物理步骤的平均形变
            if (sampleCountThisStep > 0)
            {
                float averageDeformationThisStep = totalDeformationThisStep / sampleCountThisStep;

                // 如果当前步骤的平均形变大于已记录的最大值，则更新最大值
                if (averageDeformationThisStep > maxDeformationRecorded)
                {
                    maxDeformationRecorded = averageDeformationThisStep;
                }
            }
        }
    }
}