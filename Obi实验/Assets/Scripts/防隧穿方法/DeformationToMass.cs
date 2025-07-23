using UnityEngine;
using Obi;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 这个脚本根据软体粒子的形变动态地修改其质量。
/// ...
/// </summary>
[RequireComponent(typeof(ObiSoftbody))]
public class DeformationToMass : MonoBehaviour
{
    // --- 新增一个结构体来保存调试信息，方便排序 ---
    private struct ParticleChangeInfo
    {
        public int SolverIndex;
        public float NewMass;
        public float MassScale; // 我们将根据这个值来排序
    }

    private ObiSoftbody softbody;

    [Header("Behavior")]
    [Tooltip("形变效果的放大系数。值越大，同样形变导致的质量缩放系数越高。")]
    public float deformationScaling = 10f;

    [Tooltip("允许的最大质量缩放系数。例如，值为5意味着粒子质量最多可以变成其原始质量的5倍。")]
    public float maxMassScale = 5f;

    private float[] deformationSum;
    private int[] counts;
    private bool isReady = false;

    [Header("Debug")]
    [Tooltip("开启后将在屏幕上显示一个调试窗口，展示质量变化最大的前40个粒子。")]
    public bool showDebugGUI = false;

    private const int DebugParticleDisplayCount = 40;
    private Dictionary<int, float> particlesForDebug = new Dictionary<int, float>();
    private Rect debugWindowRect = new Rect(20, 20, 320, 400); // 稍微加宽窗口
    private Vector2 scrollPosition = Vector2.zero;

    void OnEnable()
    {
        softbody = GetComponent<ObiSoftbody>();
        softbody.OnSubstepsStart += UpdateMasses;
        Initialize();
    }

    void OnDisable()
    {
        if (softbody != null)
        {
            softbody.OnSubstepsStart -= UpdateMasses;
            RestoreInitialMasses();
        }
    }
    
    private void Initialize()
    {
        if (softbody != null && softbody.isLoaded)
        {
            deformationSum = new float[softbody.particleCount];
            counts = new int[softbody.particleCount];
            isReady = true;
        }
    }

    private void RestoreInitialMasses()
    {
        if (!isReady || softbody == null || !softbody.isLoaded || softbody.softbodyBlueprint == null) return;
        for (int i = 0; i < softbody.solverIndices.count; ++i)
        {
            int solverIndex = softbody.solverIndices[i];
            if (i < softbody.softbodyBlueprint.invMasses.Length && softbody.softbodyBlueprint.invMasses[i] > 0)
            {
                softbody.solver.invMasses[solverIndex] = softbody.softbodyBlueprint.invMasses[i];
            }
        }
        softbody.UpdateParticleProperties();
    }

    private void UpdateMasses(ObiActor actor, float simulatedTime, float substepTime)
    {
        if (!isReady) Initialize();
        if (!isReady || actor != softbody || softbody.softbodyBlueprint == null) return;
        
        // 如果开启调试，创建一个临时列表来收集本帧受影响的粒子
        var affectedParticlesThisFrame = showDebugGUI ? new List<ParticleChangeInfo>() : null;

        var dc = softbody.GetConstraintsByType(Oni.ConstraintType.ShapeMatching) as ObiConstraints<ObiShapeMatchingConstraintsBatch>;
        var sc = softbody.solver.GetConstraintsByType(Oni.ConstraintType.ShapeMatching) as ObiConstraints<ObiShapeMatchingConstraintsBatch>;

        if (dc != null && sc != null)
        {
            for (int j = 0; j < softbody.solverBatchOffsets[(int)Oni.ConstraintType.ShapeMatching].Count; ++j)
            {
                var batch = dc.batches[j] as ObiShapeMatchingConstraintsBatch;
                var solverBatch = sc.batches[j] as ObiShapeMatchingConstraintsBatch;
                for (int i = 0; i < batch.activeConstraintCount; i++)
                {
                    int offset = softbody.solverBatchOffsets[(int)Oni.ConstraintType.ShapeMatching][j];
                    float deformation = solverBatch.linearTransforms[offset + i].FrobeniusNorm() - 2;
                    for (int k = 0; k < batch.numIndices[i]; ++k)
                    {
                        int p_local = batch.particleIndices[batch.firstIndex[i] + k];
                        deformationSum[p_local] += deformation;
                        counts[p_local]++;
                    }
                }
            }
            
            for (int i = 0; i < softbody.solverIndices.count; ++i)
            {
                int solverIndex = softbody.solverIndices[i];
                if (i >= softbody.softbodyBlueprint.invMasses.Length) continue;
                
                float blueprintInvMass = softbody.softbodyBlueprint.invMasses[i];
                if (blueprintInvMass <= 0) continue; 

                float finalInvMass = blueprintInvMass;
                float massScale = 1.0f;

                if (counts[i] > 0)
                {
                    float avgDeformation = deformationSum[i] / counts[i];
                    float scaleIncrease = Mathf.Abs(avgDeformation * deformationScaling);
                    massScale = 1.0f + scaleIncrease;
                    massScale = Mathf.Min(massScale, maxMassScale);
                    finalInvMass = blueprintInvMass / massScale;
                    deformationSum[i] = 0;
                    counts[i] = 0;
                }

                softbody.solver.invMasses[solverIndex] = finalInvMass;

                // 如果开启调试，并且质量确实发生了变化，则记录信息
                if (showDebugGUI && massScale > 1.0f)
                {
                    float currentMass = (finalInvMass > 0) ? (1.0f / finalInvMass) : float.PositiveInfinity;
                    affectedParticlesThisFrame.Add(new ParticleChangeInfo
                    {
                        SolverIndex = solverIndex,
                        NewMass = currentMass,
                        MassScale = massScale
                    });
                }
            }
            
            softbody.UpdateParticleProperties();

            if (showDebugGUI)
            {
                UpdateDebugList(affectedParticlesThisFrame);
            }
        }
    }
    
    // *** 核心逻辑修改 ***
    // 从随机洗牌改为降序排序
    private void UpdateDebugList(List<ParticleChangeInfo> allAffected)
    {
        // 按 MassScale 降序排序 (b 在前 a 在后)
        allAffected.Sort((a, b) => b.MassScale.CompareTo(a.MassScale));

        particlesForDebug.Clear();
        int count = Mathf.Min(allAffected.Count, DebugParticleDisplayCount);
        for (int i = 0; i < count; i++)
        {
            var info = allAffected[i];
            particlesForDebug[info.SolverIndex] = info.NewMass;
        }
    }

    void OnGUI()
    {
        if (!showDebugGUI) return;
        debugWindowRect = GUILayout.Window(0, debugWindowRect, DrawDebugWindow, "Deformation-Mass Debug (Top 40)");
    }

    void DrawDebugWindow(int windowID)
    {
        // 修改标题以反映新逻辑
        GUILayout.Label($"显示质量变化最大的 {particlesForDebug.Count} / {DebugParticleDisplayCount} 个粒子:");

        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        if (particlesForDebug.Count == 0)
        {
            GUILayout.Label("当前没有粒子受形变影响...");
        }
        else
        {
            foreach (var particleData in particlesForDebug)
            {
                GUILayout.Label($"粒子 [{particleData.Key}]: 质量 = {particleData.Value.ToString("F4")}");
            }
        }

        GUILayout.EndScrollView();
        GUI.DragWindow();
    }
}