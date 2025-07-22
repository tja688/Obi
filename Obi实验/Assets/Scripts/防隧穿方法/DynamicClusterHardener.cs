// using UnityEngine;
// using Obi;
// using System.Collections.Generic;
// using System;
//
// /// <summary>
// /// 动态集群硬化器 (V1 - 稳定版)
// /// 通过在碰撞的粒子簇内部动态添加距离约束，来安全、稳定地强化其刚性。
// /// 这是比直接修改质量更推荐、更符合Obi设计哲学的做法。
// /// </summary>
// [RequireComponent(typeof(ObiSoftbody))]
// public class DynamicClusterHardener : MonoBehaviour
// {
//     [Header("核心配置")]
//     [Tooltip("只对拥有此Tag的物体加强碰撞。留空则对所有碰撞生效。")]
//     public string colliderTag;
//     [Tooltip("动态创建的距离约束的硬度 (0-1)。")]
//     [Range(0, 1)]
//     public float stiffness = 1f;
//
//     // --- 内部数据结构 ---
//     private ObiSoftbody softbody;
//     private ObiSolver solver;
//     private ObiShapeMatchingConstraintsData shapeMatchingConstraintsData;
//     private ObiDistanceConstraintsData distanceConstraintsData;
//     private ObiDistanceConstraintsBatch dynamicBatch;
//
//     private readonly HashSet<int> collidingParticlesThisFrame = new HashSet<int>();
//     // 用于记录哪些集群已经被强化过了
//     private readonly HashSet<Tuple<int, int>> hardenedClusters = new HashSet<Tuple<int, int>>();
//
//     #region Unity生命周期与Obi事件
//     void OnEnable()
//     {
//         softbody = GetComponent<ObiSoftbody>();
//         softbody.OnBlueprintLoaded += OnBlueprintLoaded;
//         if (softbody.isLoaded)
//             OnBlueprintLoaded(softbody, softbody.sourceBlueprint);
//     }
//
//     void OnDisable()
//     {
//         softbody.OnBlueprintLoaded -= OnBlueprintLoaded;
//         if (solver != null)
//         {
//             solver.OnCollision -= Solver_OnCollision;
//         }
//         // 在禁用时，清理动态创建的约束批处理
//         if (distanceConstraintsData != null && dynamicBatch != null)
//         {
//             distanceConstraintsData.RemoveBatch(dynamicBatch);
//         }
//     }
//
//     private void OnBlueprintLoaded(ObiActor actor, ObiActorBlueprint blueprint)
//     {
//         shapeMatchingConstraintsData = softbody.GetConstraintsByType(Oni.ConstraintType.ShapeMatching) as ObiShapeMatchingConstraintsData;
//         if (shapeMatchingConstraintsData == null)
//         {
//             Debug.LogError("本脚本依赖 Obi Shape Matching Constraints 来定义粒子簇。", this);
//             enabled = false;
//             return;
//         }
//         
//         // 获取或创建 Distance 约束数据
//         distanceConstraintsData = softbody.GetConstraintsByType(Oni.ConstraintType.Distance) as ObiDistanceConstraintsData;
//         if (distanceConstraintsData == null)
//         {
//             distanceConstraintsData = softbody.GetOrCreateConstraints(Oni.ConstraintType.Distance) as ObiDistanceConstraintsData;
//         }
//
//         // 创建一个用于动态添加约束的批处理
//         dynamicBatch = distanceConstraintsData.CreateBatch();
//
//         if (solver != null) solver.OnCollision -= Solver_OnCollision;
//         solver = softbody.solver;
//         if (solver != null) solver.OnCollision += Solver_OnCollision;
//     }
//     #endregion
//
//     #region 核心逻辑
//     private void Solver_OnCollision(ObiSolver solver, ObiNativeContactList contacts)
//     {
//         if (contacts.count == 0 || !this.enabled) return;
//
//         for (int i = 0; i < contacts.count; ++i)
//         {
//             var contact = contacts[i];
//             int particleSolverIndex = GetParticleSolverIndexFromContact(contact);
//             if (particleSolverIndex == -1) continue;
//
//             int colliderIndex = GetColliderIndexFromContact(contact);
//             var collider = ObiColliderWorld.GetInstance().colliderHandles[colliderIndex].owner;
//             if (collider == null || (!string.IsNullOrEmpty(colliderTag) && !collider.CompareTag(colliderTag))) continue;
//
//             collidingParticlesThisFrame.Add(solver.particleToActor[particleSolverIndex].indexInActor);
//         }
//     }
//
//     void LateUpdate()
//     {
//         if (solver == null || !softbody.isLoaded || shapeMatchingConstraintsData == null || collidingParticlesThisFrame.Count == 0)
//         {
//             return;
//         }
//
//         bool constraintsAdded = false;
//
//         // 遍历所有ShapeMatching批处理
//         for (int batchIndex = 0; batchIndex < shapeMatchingConstraintsData.batchCount; ++batchIndex)
//         {
//             var batch = shapeMatchingConstraintsData.batches[batchIndex];
//             // 遍历批处理中的每一个簇
//             for (int shapeIndex = 0; shapeIndex < batch.activeConstraintCount; ++shapeIndex)
//             {
//                 var clusterId = new Tuple<int, int>(batchIndex, shapeIndex);
//                 // 如果该簇已被强化，则跳过
//                 if (hardenedClusters.Contains(clusterId)) continue;
//                 
//                 // 遍历簇内的每一个粒子，检查是否有碰撞
//                 for (int k = 0; k < batch.numIndices[shapeIndex]; ++k)
//                 {
//                     int particleActorIndex = batch.particleIndices[batch.firstIndex[shapeIndex] + k];
//                     if (collidingParticlesThisFrame.Contains(particleActorIndex))
//                     {
//                         // 发现碰撞，强化整个簇
//                         HardenCluster(batch, shapeIndex);
//                         hardenedClusters.Add(clusterId);
//                         constraintsAdded = true;
//                         goto next_cluster; // 处理完这个簇就跳到下一个，避免重复添加
//                     }
//                 }
//             }
//             next_cluster:;
//         }
//
//         if (constraintsAdded)
//         {
//             // 通知求解器有新的约束加入
//             softbody.SetConstraintsDirty(Oni.ConstraintType.Distance);
//         }
//
//         collidingParticlesThisFrame.Clear();
//     }
//
//     /// <summary>
//     /// 强化一个粒子簇的核心方法
//     /// </summary>
//     private void HardenCluster(ObiShapeMatchingConstraintsBatch batch, int shapeIndex)
//     {
//         int particleCount = batch.numIndices[shapeIndex];
//         int firstParticleOffset = batch.firstIndex[shapeIndex];
//
//         // 在簇内所有粒子对之间创建距离约束 (n*(n-1)/2 条)
//         for (int i = 0; i < particleCount; ++i)
//         {
//             for (int j = i + 1; j < particleCount; ++j)
//             {
//                 int p1_actorIndex = batch.particleIndices[firstParticleOffset + i];
//                 int p2_actorIndex = batch.particleIndices[firstParticleOffset + j];
//                 
//                 int p1_solverIndex = softbody.solverIndices[p1_actorIndex];
//                 int p2_solverIndex = softbody.solverIndices[p2_actorIndex];
//
//                 // 计算当前距离作为约束的静止长度，以“锁死”当前状态
//                 float restLength = Vector3.Distance(
//                     (Vector3)solver.positions[p1_solverIndex],
//                     (Vector3)solver.positions[p2_solverIndex]
//                 );
//                 
//                 // 向动态批处理中添加约束
//                 dynamicBatch.AddConstraint(new Vector2Int(p1_actorIndex, p2_actorIndex), restLength, stiffness);
//             }
//         }
//         Debug.Log($"<color=green>集群已硬化:</color> 为一个包含 {particleCount} 个粒子的集群添加了 {particleCount * (particleCount - 1) / 2} 个距离约束。");
//     }
//
//     #endregion
//
//     #region 辅助函数
//     private int GetParticleSolverIndexFromContact(Oni.Contact contact)
//     {
//         if (IsParticleFromOurSoftbody(contact.bodyA)) return contact.bodyA;
//         if (IsParticleFromOurSoftbody(contact.bodyB)) return contact.bodyB;
//         return -1;
//     }
//
//     private int GetColliderIndexFromContact(Oni.Contact contact)
//     {
//         return IsParticleFromOurSoftbody(contact.bodyA) ? contact.bodyB : contact.bodyA;
//     }
//
//     private bool IsParticleFromOurSoftbody(int particleSolverIndex)
//     {
//         if (solver == null || particleSolverIndex < 0 || particleSolverIndex >= solver.particleToActor.Length) return false;
//         var p = solver.particleToActor[particleSolverIndex];
//         return p != null && p.actor == softbody;
//     }
//     #endregion
// }