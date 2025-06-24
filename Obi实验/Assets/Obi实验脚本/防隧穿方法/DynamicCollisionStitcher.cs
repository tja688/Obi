using UnityEngine;
using Obi;
using System.Collections.Generic;
using System;

/// <summary>
/// (V3 - 最终修正版)
/// 通过动态在碰撞粒子之间创建 Stitch 约束来强化接触区域的刚性，以防止穿透。
/// 
/// 修正记录:
/// - 根据 ObiStitcher.cs 源码，采用手动创建/销毁约束批处理 (IStitchConstraintsBatchImpl) 的正确工作流。
/// - 脚本不再依赖于软物体上的 ObiStitchConstraintsData 组件，而是直接与 Solver 后端交互。
/// - 脚本内部自己维护 NativeList，并使用 SetStitchConstraints(...) 推送数据。
/// </summary>
[RequireComponent(typeof(ObiSoftbody))]
public class DynamicStitchStiffener : MonoBehaviour
{
    [Header("核心功能配置")]
    [Tooltip("只对拥有此Tag的物体加强碰撞。留空则对所有碰撞生效。")]
    public string colliderTag;
    [Tooltip("预分配的 Stitch 约束池大小。")]
    public int stitchPoolSize = 1024;

    [Header("缝合强度与范围")]
    [Tooltip("Stitch约束的硬度 (0-1)。将直接影响接触面片的'刚性'程度。")]
    [Range(0f, 1f)]
    public float stitchStiffness = 1f;
    [Tooltip("只有当两个碰撞粒子间的距离小于此值时，才为它们创建约束。")]
    public float stitchMaxDistance = 0.5f;

    [Header("可视化与调试")]
    [Tooltip("勾选后，所有被动态缝合的粒子都会高亮显示。")]
    public bool enableVisualization = true;
    public Color stitchedParticleColor = Color.yellow;
    [Tooltip("勾选后，将在控制台打印约束的激活和销毁日志。")]
    public bool logActivity = false;

    // --- 核心内部状态 ---
    private ObiSoftbody softbody;
    private ObiSolver solver;

    // V3 修正：手动管理批处理和数据列表
    private IStitchConstraintsBatchImpl m_BatchImpl;
    private ObiNativeIntList particleIndices;
    private ObiNativeFloatList stiffnesses;
    private ObiNativeFloatList lambdas;
    private int activeConstraintCount = 0;
    private bool isInSolver = false;

    // 用于跟踪活动的约束：将粒子对(Item1 < Item2)映射到其在批处理中的索引
    private readonly Dictionary<Tuple<int, int>, int> pairToBatchIndexMap = new Dictionary<Tuple<int, int>, int>();
    
    // 用于在物理回调和LateUpdate之间传递信息
    private readonly HashSet<int> collidingParticlesThisFrame = new HashSet<int>();
    private readonly List<int> collidingParticlesList = new List<int>();

    // 用于可视化
    private readonly Dictionary<int, Color> originalParticleColors = new Dictionary<int, Color>();

    #region Unity生命周期与Obi事件
    void Start()
    {
        softbody = GetComponent<ObiSoftbody>();
        solver = softbody.solver; 
        softbody.OnBlueprintLoaded += OnBlueprintLoaded;
        if (softbody.isLoaded)
        {
            OnBlueprintLoaded(softbody, softbody.sourceBlueprint);
        }
    }

    void OnDisable()
    {
        softbody.OnBlueprintLoaded -= OnBlueprintLoaded;
        // 如果之前已成功添加到求解器，则从中移除
        if (solver != null && isInSolver)
        {
            RemoveFromSolver();
        }
    }

    private void OnBlueprintLoaded(ObiActor actor, ObiActorBlueprint blueprint)
    {
        if (actor == softbody && softbody.isLoaded)
        {
            AddToSolver(solver);
        }
    }
    #endregion

    #region 核心逻辑
    void LateUpdate()
    {
        if (!isInSolver || !softbody.isLoaded) return;

        bool constraintsChanged = false;

        // --- 步骤 1: 停用不再有效的约束 ---
        var activePairs = new List<Tuple<int, int>>(pairToBatchIndexMap.Keys);
        foreach (var pair in activePairs)
        {
            if (!collidingParticlesThisFrame.Contains(pair.Item1) || !collidingParticlesThisFrame.Contains(pair.Item2))
            {
                DeactivateStitch(pairToBatchIndexMap[pair]);
                constraintsChanged = true;
            }
        }

        // --- 步骤 2: 激活新的约束 ---
        collidingParticlesList.Clear();
        collidingParticlesList.AddRange(collidingParticlesThisFrame);

        for (int i = 0; i < collidingParticlesList.Count; ++i)
        {
            for (int j = i + 1; j < collidingParticlesList.Count; ++j)
            {
                if (activeConstraintCount >= stitchPoolSize)
                {
                    Debug.LogWarning("Stitch约束池已满，请考虑增大 stitchPoolSize。", this);
                    goto EndOfCreation; 
                }
                
                int pIndex1 = collidingParticlesList[i];
                int pIndex2 = collidingParticlesList[j];

                var pair = new Tuple<int, int>(Math.Min(pIndex1, pIndex2), Math.Max(pIndex1, pIndex2));

                if (!pairToBatchIndexMap.ContainsKey(pair))
                {
                    int solverIndex1 = softbody.solverIndices[pIndex1];
                    int solverIndex2 = softbody.solverIndices[pIndex2];
                    
                    if (Vector3.SqrMagnitude(solver.positions[solverIndex1] - solver.positions[solverIndex2]) < stitchMaxDistance * stitchMaxDistance)
                    {
                        ActivateStitch(pair);
                        constraintsChanged = true;
                    }
                }
            }
        }

    EndOfCreation:;

        if (constraintsChanged)
        {
            // V3 修正: 直接调用批处理的 Set 方法来更新约束数据
            m_BatchImpl.SetStitchConstraints(particleIndices, stiffnesses, lambdas, activeConstraintCount);
            if (enableVisualization) UpdateColors();
        }

        collidingParticlesThisFrame.Clear();
    }
    
    private void Solver_OnCollision(ObiSolver solver, ObiNativeContactList contacts)
    {
        if (!this.enabled || contacts.count == 0) return;

        for (int i = 0; i < contacts.count; ++i)
        {
            Oni.Contact contact = contacts[i];
            
            int particleSolverIndex = GetParticleSolverIndexFromContact(contact);
            if (particleSolverIndex == -1) continue;

            var otherCollider = ObiColliderWorld.GetInstance().colliderHandles[GetColliderIndexFromContact(contact)].owner;
            if (otherCollider == null || (!string.IsNullOrEmpty(colliderTag) && !otherCollider.CompareTag(colliderTag))) continue;

            collidingParticlesThisFrame.Add(solver.particleToActor[particleSolverIndex].indexInActor);
        }
    }
    #endregion

    #region 手动批处理管理 (V3 核心修正)
    
    private void AddToSolver(ObiSolver solver)
    {
        if (solver == null || isInSolver) return;

        this.solver = solver;
        this.solver.OnCollision += Solver_OnCollision;

        // 初始化数据列表，容量为设定的池大小
        particleIndices = new ObiNativeIntList(stitchPoolSize * 2);
        stiffnesses = new ObiNativeFloatList(stitchPoolSize);
        lambdas = new ObiNativeFloatList(stitchPoolSize);

        // 直接从求解器实现中创建批处理
        m_BatchImpl = solver.implementation.CreateConstraintsBatch(Oni.ConstraintType.Stitch) as IStitchConstraintsBatchImpl;

        if (m_BatchImpl != null)
        {
            m_BatchImpl.enabled = true;
            isInSolver = true;
        }
        else
        {
            Debug.LogError("创建Stitch约束批处理失败！请检查求解器后端是否支持。");
        }
    }

    private void RemoveFromSolver()
    {
        if (solver == null || !isInSolver) return;
        
        solver.OnCollision -= Solver_OnCollision;

        if (m_BatchImpl != null)
        {
            // 从求解器实现中销毁批处理
            solver.implementation.DestroyConstraintsBatch(m_BatchImpl);
            m_BatchImpl.Destroy();
            m_BatchImpl = null;
        }

        // 释放 NativeList 内存
        particleIndices.Dispose();
        stiffnesses.Dispose();
        lambdas.Dispose();
        
        isInSolver = false;
        this.solver = null;
    }
    
    private void ActivateStitch(Tuple<int, int> pair)
    {
        int index = activeConstraintCount;

        // 在列表末尾添加新数据
        particleIndices[index * 2] = softbody.solverIndices[pair.Item1];
        particleIndices[index * 2 + 1] = softbody.solverIndices[pair.Item2];
        stiffnesses[index] = 1 - stitchStiffness; // Obi 中硬度通常是 1-stiffness
        lambdas[index] = 0;
        
        pairToBatchIndexMap[pair] = index;
        activeConstraintCount++;
        
        if (logActivity) Debug.Log($"<color=lime>Stitch Activated:</color> {pair}");
    }

    private void DeactivateStitch(int indexToRemove)
    {
        if (indexToRemove >= activeConstraintCount) return;

        var pairToRemove = new Tuple<int, int>(
            solver.particleToActor[particleIndices[indexToRemove * 2]].indexInActor, 
            solver.particleToActor[particleIndices[indexToRemove * 2 + 1]].indexInActor
        );
        var orderedPair = new Tuple<int,int>(Math.Min(pairToRemove.Item1, pairToRemove.Item2), Math.Max(pairToRemove.Item1, pairToRemove.Item2));

        if (logActivity) Debug.Log($"<color=red>Stitch Deactivated:</color> {orderedPair}");
        
        pairToBatchIndexMap.Remove(orderedPair);

        activeConstraintCount--;
        int lastIndex = activeConstraintCount;

        // 如果移除的不是最后一个元素，用最后一个元素覆盖它 (Swap and Pop)
        if (indexToRemove < lastIndex)
        {
            // 复制最后一个元素的数据到当前位置
            particleIndices[indexToRemove * 2] = particleIndices[lastIndex * 2];
            particleIndices[indexToRemove * 2 + 1] = particleIndices[lastIndex * 2 + 1];
            stiffnesses[indexToRemove] = stiffnesses[lastIndex];
            lambdas[indexToRemove] = lambdas[lastIndex];

            // 更新被移动的那个元素在字典中的索引
            var movedPair = new Tuple<int, int>(
                solver.particleToActor[particleIndices[indexToRemove * 2]].indexInActor,
                solver.particleToActor[particleIndices[indexToRemove * 2 + 1]].indexInActor
            );
            var orderedMovedPair = new Tuple<int,int>(Math.Min(movedPair.Item1, movedPair.Item2), Math.Max(movedPair.Item1, movedPair.Item2));
            pairToBatchIndexMap[orderedMovedPair] = indexToRemove;
        }
    }
    #endregion

    #region 辅助与可视化
    private void UpdateColors()
    {
        if (!enableVisualization || solver == null) return;
        
        RestoreAllParticleColors();
        
        for(int i = 0; i < activeConstraintCount; ++i)
        {
            int solverIndex1 = particleIndices[i * 2];
            int solverIndex2 = particleIndices[i * 2 + 1];
            
            SetParticleColor(solverIndex1, stitchedParticleColor, true);
            SetParticleColor(solverIndex2, stitchedParticleColor, true);
        }

        if (originalParticleColors.Count > 0)
            solver.colors.Upload();
    }

    private void SetParticleColor(int solverIndex, Color color, bool isSolverIndex = false)
    {
        if (solverIndex < 0) return;
        
        if (!isSolverIndex) // if actor index is passed
        {
            if (solverIndex >= softbody.solverIndices.count) return;
            solverIndex = softbody.solverIndices[solverIndex];
        }

        if (!originalParticleColors.ContainsKey(solverIndex))
            originalParticleColors[solverIndex] = solver.colors[solverIndex];
        solver.colors[solverIndex] = color;
    }

    private void RestoreAllParticleColors()
    {
        if (originalParticleColors.Count == 0 || solver == null) return;
        
        foreach (var pair in originalParticleColors)
        {
            if (pair.Key >= 0 && pair.Key < solver.colors.count)
                solver.colors[pair.Key] = pair.Value;
        }
        
        if (originalParticleColors.Count > 0)
            solver.colors.Upload();
            
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
    #endregion
}