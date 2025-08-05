using UnityEngine;
using System.Collections.Generic;
using Obi; // 必须引用Obi命名空间
using System.Text; // 引入StringBuilder所需的命名空间

// 要求此脚本所在的物体上必须有ObiSoftbody组件
[RequireComponent(typeof(ObiSoftbody))]
public class RecordDeformationOnMassIncrease : MonoBehaviour
{
    [Header("目标对象设置")]
    [Tooltip("需要控制其质量并检测其碰撞的外部刚体")]
    public Rigidbody targetRigidbody;

    [Header("质量变化设置")]
    [Tooltip("模拟开始时刚体的最小质量")]
    public float minMass = 1f;
    [Tooltip("模拟过程中刚体能达到的最大质量")]
    public float maxMass = 100f;
    [Tooltip("从最小质量增长到最大质量所需的总时间（秒）")]
    public float massIncreaseDuration = 60f;

    [Header("数据记录设置")]
    [Tooltip("每隔多少秒记录一次数据")]
    public float recordingInterval = 5f;

    // --- 私有变量 ---
    private ObiSoftbody softbody;
    private bool isSimulating = false;
    private float simulationStartTime;
    private float lastRecordingTime;

    private float accumulatedDeformation;
    private int deformationSampleCount;

    public struct DeformationRecord
    {
        public float timeSinceStart;
        public float rigidbodyMass;
        public float averageDeformation;

        public override string ToString()
        {
            return $"记录时间: {timeSinceStart:F2}s, 刚体质量: {rigidbodyMass:F2}, 平均形变量: {averageDeformation:F5}";
        }
    }

    private List<DeformationRecord> allRecords;

    void Awake()
    {
        softbody = GetComponent<ObiSoftbody>();
        allRecords = new List<DeformationRecord>();

        if (targetRigidbody == null)
        {
            Debug.LogError("错误：请在Inspector面板中指定一个目标刚体 (Target Rigidbody)！");
            enabled = false;
        }
    }

    void OnEnable()
    {
        if (softbody != null)
        {
            softbody.OnInterpolate += CalculateDeformation;
        }
    }

    void OnDisable()
    {
        if (softbody != null)
        {
            softbody.OnInterpolate -= CalculateDeformation;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.S) && !isSimulating)
        {
            StartSimulation();
        }

        if (isSimulating)
        {
            UpdateRigidbodyMass();

            if (Time.time - lastRecordingTime >= recordingInterval)
            {
                RecordCurrentData();
            }
        }
    }

    private void CalculateDeformation(ObiActor actor, float stepTime, float substepTime)
    {
        if (!isSimulating) return;

        var dc = softbody.GetConstraintsByType(Oni.ConstraintType.ShapeMatching) as ObiConstraints<ObiShapeMatchingConstraintsBatch>;
        var sc = softbody.solver.GetConstraintsByType(Oni.ConstraintType.ShapeMatching) as ObiConstraints<ObiShapeMatchingConstraintsBatch>;

        if (dc != null && sc != null)
        {
            for (int j = 0; j < softbody.solverBatchOffsets[(int)Oni.ConstraintType.ShapeMatching].Count; ++j)
            {
                var solverBatch = sc.batches[j] as ObiShapeMatchingConstraintsBatch;

                for (int i = 0; i < solverBatch.activeConstraintCount; i++)
                {
                    int offset = softbody.solverBatchOffsets[(int)Oni.ConstraintType.ShapeMatching][j];
                    float deformation = solverBatch.linearTransforms[offset + i].FrobeniusNorm() - 2;
                    accumulatedDeformation += deformation;
                    deformationSampleCount++;
                }
            }
        }
    }

    public void StartSimulation()
    {
        isSimulating = true;
        simulationStartTime = Time.time;
        lastRecordingTime = Time.time;
        allRecords.Clear();

        if(targetRigidbody != null)
        {
            targetRigidbody.mass = minMass;
        }
        
        Debug.Log("====== 模拟开始 ======");
        RecordCurrentData();
    }

    public void StopSimulationByCollision()
    {
        if (!isSimulating) return;

        isSimulating = false;
        Debug.Log("====== 模拟因碰撞终止 ======");
        
        RecordCurrentData();
        PrintAllRecords();
    }

    private void UpdateRigidbodyMass()
    {
        if (targetRigidbody == null) return;

        float elapsedTime = Time.time - simulationStartTime;
        float progress = Mathf.Clamp01(elapsedTime / massIncreaseDuration);
        targetRigidbody.mass = Mathf.Lerp(minMass, maxMass, progress);
    }

    private void RecordCurrentData()
    {
        if (targetRigidbody == null) return;

        float averageDeformation = 0;
        if (deformationSampleCount > 0)
        {
            averageDeformation = accumulatedDeformation / deformationSampleCount;
        }

        DeformationRecord newRecord = new DeformationRecord
        {
            timeSinceStart = Time.time - simulationStartTime,
            rigidbodyMass = targetRigidbody.mass,
            averageDeformation = averageDeformation
        };

        allRecords.Add(newRecord);

        // 我们不再在这里单独打印每一条记录
        // Debug.Log(newRecord.ToString()); 

        lastRecordingTime = Time.time;
        accumulatedDeformation = 0;
        deformationSampleCount = 0;
    }

    // ======[ 此方法已更新 ]======
    private void PrintAllRecords()
    {
        // 使用StringBuilder来高效地构建一个长字符串
        StringBuilder reportBuilder = new StringBuilder();

        reportBuilder.AppendLine("====== 完整记录报告 ======"); // AppendLine会自动添加换行符

        if (allRecords.Count == 0)
        {
            reportBuilder.AppendLine("没有任何记录。");
        }
        else
        {
            foreach (var record in allRecords)
            {
                // 将每条记录的字符串形式追加到StringBuilder中
                reportBuilder.AppendLine(record.ToString());
            }
        }

        reportBuilder.AppendLine("==========================");

        Debug.Log(reportBuilder.ToString());
    }
}