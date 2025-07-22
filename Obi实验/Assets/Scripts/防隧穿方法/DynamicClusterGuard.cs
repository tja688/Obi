using UnityEngine;
using Obi;
using System.Collections.Generic;
using System;

/// <summary>
/// 动态集群护盾 V1.7 - 修正崩溃，暂停销毁逻辑以便调试。
/// 1. 修正了 IsParticleFromOurSoftbody 中因使用 .count 而非 .Length 导致的 ArgumentOutOfRangeException。
/// 2. 暂时注释掉了护盾的自动销毁逻辑，使其创建后能持续存在，便于观察和调试。
/// </summary>
[RequireComponent(typeof(ObiSoftbody))]
public class DynamicClusterGuard : MonoBehaviour
{
    [Header("核心配置")]
    [Tooltip("护盾代理的预制体 (必须包含 Rigidbody, BoxCollider, ObiCollider 和 GuardProxy 脚本)")]
    public GameObject guardPrefab;
    [Tooltip("只对拥有此Tag的物体加强碰撞。留空则对所有碰撞生效。")]
    public string colliderTag;
    [Tooltip("预分配的 Pin 约束池大小。")]
    public int pinPoolSize = 256;

    [Header("固定模式与强度")]
    [Tooltip("Pin约束的硬度 (0-1)。")]
    [Range(0f, 1f)]
    public float pinStiffness = 1f;

    [Header("失效逻辑")]
    [Tooltip("护盾在脱离接触后，等待多少帧再被销毁。建议值: 3-5")]
    public int deactivationDelay = 3;

    [Header("可视化与调试")]
    public bool enableVisualization = true;
    public Color guardedParticleColor = Color.magenta;

    // --- 内部数据结构 ---
    private ObiSoftbody softbody;
    private ObiSolver solver;
    private ObiShapeMatchingConstraintsData shapeMatchingConstraintsData;
    private ObiPinConstraintsData pinConstraintsData;
    private ObiPinConstraintsBatch dynamicPinBatch;

    private List<GuardProxy> guardPool;

    private class ActiveGuardInfo
    {
        public GuardProxy guard;
        public List<int> pinnedParticleActorIndices = new List<int>();
    }
    private readonly Dictionary<Tuple<int, int>, ActiveGuardInfo> activeGuards = new Dictionary<Tuple<int, int>, ActiveGuardInfo>();
    private readonly HashSet<int> collidingParticlesThisFrame = new HashSet<int>();
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
            solver.OnCollision -= Solver_OnCollision;

        foreach (var guardInfo in activeGuards.Values)
        {
            if (guardInfo.guard != null) guardInfo.guard.Deactivate();
        }
        activeGuards.Clear();
        RemoveDynamicBatch();
        DestroyGuardPool();
        RestoreAllParticleColors();
    }

    private void OnBlueprintLoaded(ObiActor actor, ObiActorBlueprint blueprint)
    {
        shapeMatchingConstraintsData = softbody.GetConstraintsByType(Oni.ConstraintType.ShapeMatching) as ObiShapeMatchingConstraintsData;
        if (shapeMatchingConstraintsData == null)
        {
            Debug.LogError("本脚本依赖 Obi Shape Matching Constraints 来定义粒子簇。请为软体添加该组件。", this);
            enabled = false;
            return;
        }

        if (solver != null) solver.OnCollision -= Solver_OnCollision;
        solver = softbody.solver;
        if (solver != null) solver.OnCollision += Solver_OnCollision;
        
        SetupDynamicBatch();
        CreateGuardPool();
    }
    #endregion

    #region 核心逻辑
    private void Solver_OnCollision(ObiSolver solver, ObiNativeContactList contacts)
    {
        if (contacts.count == 0) return;

        for (int i = 0; i < contacts.count; ++i)
        {
            var contact = contacts[i];
            int particleSolverIndex = GetParticleSolverIndexFromContact(contact);
            if (particleSolverIndex == -1) continue;

            int colliderIndex = GetColliderIndexFromContact(contact);
            var colliderHandle = ObiColliderWorld.GetInstance().colliderHandles[colliderIndex];
            if (colliderHandle == null) continue;
            var collider = colliderHandle.owner;
            if (collider == null || (!string.IsNullOrEmpty(colliderTag) && !collider.CompareTag(colliderTag))) continue;

            collidingParticlesThisFrame.Add(solver.particleToActor[particleSolverIndex].indexInActor);
        }
    }

    void LateUpdate()
    {
        if (solver == null || !softbody.isLoaded || shapeMatchingConstraintsData == null) return;

        bool constraintsChanged = false;

        for (int batchIndex = 0; batchIndex < shapeMatchingConstraintsData.batchCount; ++batchIndex)
        {
            var batch = shapeMatchingConstraintsData.batches[batchIndex];
            for (int i = 0; i < batch.activeConstraintCount; ++i)
            {
                var clusterId = new Tuple<int, int>(batchIndex, i);
                if (activeGuards.ContainsKey(clusterId)) continue;

                if (IsClusterColliding(batch, i, out List<int> particlesInCluster))
                {
                    ActivateGuardForCluster(clusterId, particlesInCluster);
                    constraintsChanged = true;
                }
            }
        }

        // [V1.7 调试] 暂时注释掉整个护盾失效/销毁逻辑，让护盾被创建后能一直存在。
        /*
        List<Tuple<int, int>> clustersToRemove = null;
        foreach (var pair in activeGuards)
        {
            var clusterId = pair.Key;
            var guardInfo = pair.Value;
            var batch = shapeMatchingConstraintsData.batches[clusterId.Item1];

            if (IsClusterColliding(batch, clusterId.Item2, out _))
            {
                guardInfo.guard.NotifyContact();
            }
            else
            {
                guardInfo.guard.framesSinceLastContact++;
            }

            if (guardInfo.guard.framesSinceLastContact > deactivationDelay)
            {
                if (clustersToRemove == null) clustersToRemove = new List<Tuple<int, int>>();
                clustersToRemove.Add(clusterId);
            }
        }

        if (clustersToRemove != null)
        {
            foreach (var clusterId in clustersToRemove)
            {
                DeactivateGuardForCluster(clusterId);
                constraintsChanged = true;
            }
        }
        */
        
        if (constraintsChanged)
        {
            softbody.SetConstraintsDirty(Oni.ConstraintType.Pin);
            if(enableVisualization) UpdateAllColors();
        }

        collidingParticlesThisFrame.Clear();
    }
    #endregion

    #region 护盾与约束管理
    private void ActivateGuardForCluster(Tuple<int, int> clusterId, List<int> particlesInCluster)
    {
        GuardProxy guard = GetGuardFromPool();
        if (guard == null)
        {
            Debug.LogWarning("护盾对象池已满！");
            return;
        }

        Vector3 center = Vector3.zero;
        foreach (int pIndex in particlesInCluster)
        {
            center += (Vector3)solver.positions[softbody.solverIndices[pIndex]];
        }
        center /= particlesInCluster.Count;
        guard.transform.position = center;
        guard.Initialize();

        var guardInfo = new ActiveGuardInfo { guard = guard };

        var guardCollider = guard.GetComponent<ObiColliderBase>();
        foreach (int pIndex in particlesInCluster)
        {
            if (dynamicPinBatch.activeConstraintCount >= pinPoolSize)
            {
                Debug.LogWarning("Pin约束池已满！");
                break;
            }
            ActivatePin(pIndex, guardCollider);
            guardInfo.pinnedParticleActorIndices.Add(pIndex);
        }
        
        activeGuards.Add(clusterId, guardInfo);
    }

    private void DeactivateGuardForCluster(Tuple<int, int> clusterId)
    {
        if (activeGuards.TryGetValue(clusterId, out ActiveGuardInfo guardInfo))
        {
            foreach (int pIndex in guardInfo.pinnedParticleActorIndices)
            {
                DeactivatePin(pIndex);
            }
            guardInfo.guard.Deactivate();
            activeGuards.Remove(clusterId);
        }
    }
    
    private bool IsClusterColliding(ObiShapeMatchingConstraintsBatch batch, int shapeIndexInBatch, out List<int> particlesInCluster)
    {
        particlesInCluster = new List<int>();
        bool isColliding = false;
        if (shapeIndexInBatch >= batch.activeConstraintCount) return false;

        for (int k = 0; k < batch.numIndices[shapeIndexInBatch]; ++k)
        {
            int particleActorIndex = batch.particleIndices[batch.firstIndex[shapeIndexInBatch] + k];
            particlesInCluster.Add(particleActorIndex);
            if (collidingParticlesThisFrame.Contains(particleActorIndex))
            {
                isColliding = true;
            }
        }
        return isColliding;
    }
    #endregion

    #region 底层Pin约束管理
    private void SetupDynamicBatch()
    {
        RemoveDynamicBatch();
        pinConstraintsData = softbody.GetConstraintsByType(Oni.ConstraintType.Pin) as ObiPinConstraintsData;
        if (pinConstraintsData == null) { enabled = false; Debug.LogError("请为软体添加 Obi Pin Constraints 组件。"); return; }
        
        dynamicPinBatch = new ObiPinConstraintsBatch();
        // 预先分配内存，这借鉴了 ContactPointPinStiffener.cs 的成熟做法
        for (int i = 0; i < pinPoolSize; ++i)
        {
            dynamicPinBatch.AddConstraint(-1, null, Vector3.zero, Quaternion.identity, 0, 0);
        }
        dynamicPinBatch.activeConstraintCount = 0;
        pinConstraintsData.AddBatch(dynamicPinBatch);
    }
    private void RemoveDynamicBatch()
    {
        if (pinConstraintsData != null && dynamicPinBatch != null)
        {
            pinConstraintsData.RemoveBatch(dynamicPinBatch);
        }
    }
    private void ActivatePin(int particleActorIndex, ObiColliderBase guardCollider)
    {
        int particleSolverIndex = softbody.solverIndices[particleActorIndex];
        int slotIndex = dynamicPinBatch.activeConstraintCount;
        Vector3 offset = guardCollider.transform.InverseTransformPoint(solver.positions[particleSolverIndex]);

        dynamicPinBatch.particleIndices[slotIndex] = particleSolverIndex;
        dynamicPinBatch.pinBodies[slotIndex] = guardCollider.Handle;
        dynamicPinBatch.colliderIndices[slotIndex] = guardCollider.Handle.index;
        dynamicPinBatch.offsets[slotIndex] = offset;
        dynamicPinBatch.stiffnesses[slotIndex * 2] = 1f - pinStiffness;
        dynamicPinBatch.stiffnesses[slotIndex * 2 + 1] = 1f;

        dynamicPinBatch.activeConstraintCount++;
    }
    private void DeactivatePin(int particleActorIndex)
    {
        int particleSolverIndex = softbody.solverIndices[particleActorIndex];
        for (int i = 0; i < dynamicPinBatch.activeConstraintCount; ++i)
        {
            if (dynamicPinBatch.particleIndices[i] == particleSolverIndex)
            {
                dynamicPinBatch.activeConstraintCount--;
                int last = dynamicPinBatch.activeConstraintCount;
                if (i < last)
                {
                    dynamicPinBatch.particleIndices[i] = dynamicPinBatch.particleIndices[last];
                    dynamicPinBatch.pinBodies[i] = dynamicPinBatch.pinBodies[last];
                    dynamicPinBatch.colliderIndices[i] = dynamicPinBatch.colliderIndices[last];
                    dynamicPinBatch.offsets[i] = dynamicPinBatch.offsets[last];
                    dynamicPinBatch.stiffnesses[i * 2] = dynamicPinBatch.stiffnesses[last * 2];
                    dynamicPinBatch.stiffnesses[i * 2 + 1] = dynamicPinBatch.stiffnesses[last * 2 + 1];
                }
                return;
            }
        }
    }
    #endregion
    
    #region 对象池与可视化
    private void CreateGuardPool()
    {
        DestroyGuardPool();
        guardPool = new List<GuardProxy>();
        for (int i = 0; i < 20; ++i) 
        {
            var go = Instantiate(guardPrefab, this.transform);
            var proxy = go.GetComponent<GuardProxy>();
            proxy.Deactivate();
            guardPool.Add(proxy);
        }
    }
    private void DestroyGuardPool()
    {
        if (guardPool != null)
        {
            foreach (var proxy in guardPool)
                if(proxy != null) Destroy(proxy.gameObject);
            guardPool.Clear();
        }
    }
    private GuardProxy GetGuardFromPool()
    {
        foreach (var proxy in guardPool)
            if (!proxy.gameObject.activeInHierarchy)
                return proxy;
        return null;
    }
    private void UpdateAllColors()
    {
        RestoreAllParticleColors();
        foreach (var guardInfo in activeGuards.Values)
        {
            foreach (int pIndex in guardInfo.pinnedParticleActorIndices)
            {
                int solverIndex = softbody.solverIndices[pIndex];
                if (!originalParticleColors.ContainsKey(solverIndex))
                    originalParticleColors[solverIndex] = solver.colors[solverIndex];
                solver.colors[solverIndex] = guardedParticleColor;
            }
        }
        if (originalParticleColors.Count > 0)
            solver.colors.Upload();
    }
    private void RestoreAllParticleColors()
    {
        if (originalParticleColors.Count > 0 && solver != null)
        {
            foreach (var p in originalParticleColors)
            {
                if (p.Key >= 0 && p.Key < solver.colors.count)
                    solver.colors[p.Key] = p.Value;
            }
            solver.colors.Upload();
        }
        originalParticleColors.Clear();
    }
    #endregion

    #region 辅助函数
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
        // [V1.7 修正] 严格使用 .Length 进行边界检查，修复 ArgumentOutOfRangeException
        if (solver == null || particleSolverIndex < 0 || particleSolverIndex >= solver.particleToActor.Length) return false;
        var p = solver.particleToActor[particleSolverIndex];
        return p != null && p.actor == softbody;
    }
    #endregion
}