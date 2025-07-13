// using UnityEngine;
// using UnityEngine.Events; // 1. 引入UnityEvents命名空间
// using Obi;
// using System;
// using System.Collections;
// using UnityEngine.Serialization;
//
// /// <summary>
// /// 一个通用的Obi碰撞触发器脚本。
// /// 当一个特定的ObiActor与该对象上的ObiCollider发生碰撞时，会触发一个可在Inspector中配置的UnityEvent。
// /// 同时，它能通过事件中心订阅事件，动态地更换监听目标。
// /// </summary>
// [RequireComponent(typeof(ObiCollider))]
// public class ObiCollisionTriggerTool : MonoBehaviour
// {
//     
//     
//     [Header("触发器")]
//     [Tooltip("建议指定触发器")]
//     [SerializeField] private ObiCollider thisCollider;
//     
//     // --- 事件名称常量 (建议统一管理) ---
//     public const string PlayerChange = "PlayerChange";
//
//     [Header("目标设置 (可选, 可动态更新)")]
//     [Tooltip("要监听其碰撞事件的求解器。如果留空，将自动尝试获取玩家的Solver。")]
//     [SerializeField] private ObiSolver targetSolver;
//
//     [Tooltip("要检测其碰撞的目标Actor。如果留空，将自动尝试获取玩家的Actor。")]
//     [SerializeField] private ObiActor targetActor;
//
//     [Header("事件响应")]
//     [Tooltip("当确认发生有效碰撞时，将触发此处的事件。")]
//     [SerializeField] private UnityEvent onCollisionConfirmed; // 2. 添加UnityEvent配置栏
//
//
//     void Awake()
//     {
//         if (!thisCollider)
//         {
//             thisCollider = GetComponent<ObiCollider>();
//         }
//         
//         if (thisCollider == null)
//         {
//             Debug.LogError($"[ObiCollisionTrigger] 在对象 '{name}' 上找不到 ObiCollider 组件！脚本将不会运行。", this);
//             enabled = false;
//         }
//     }
//
//     private void OnEnable()
//     {
//         // 3. 在OnEnable中订阅事件中心的PlayerChange事件
//         // EventCenter是你之前创建的事件中心类
//         EventCenter.AddListener<ObiSolver, ObiActor>(PlayerChange, HandlePlayerChange);
//
//         // 启动协程，将初始化的查找逻辑延迟一帧执行，避免启动顺序问题
//         StartCoroutine(DelayedInitialization());
//     }
//
//     private void OnDisable()
//     {
//         // 4. 在OnDisable中取消订阅事件中心和Solver的事件，确保健壮性
//         EventCenter.RemoveListener<ObiSolver, ObiActor>(PlayerChange, HandlePlayerChange);
//
//         if (targetSolver != null)
//         {
//             targetSolver.OnCollision -= HandleSolverCollision;
//         }
//     }
//
//     /// <summary>
//     /// 延迟一帧执行初始化，以确保其他单例（如PlayerControl_Ball）已准备就绪。
//     /// </summary>
//     private IEnumerator DelayedInitialization()
//     {
//         yield return null; // 等待下一帧
//
//         if (targetSolver == null || targetActor == null)
//         {
//             Debug.Log($"[ObiCollisionTrigger] 在 '{name}' 上未指定初始目标，将自动查找玩家...", this);
//             // if (PlayerControl_Ball.instance != null)
//             {
//                 // 使用新的HandlePlayerChange方法来统一设置初始目标
//                 // HandlePlayerChange(PlayerControl_Ball.instance.playerSolver, PlayerControl_Ball.instance.actor);
//             }
//             // else
//             {
//                 Debug.LogWarning($"[ObiCollisionTrigger] 在 '{name}' 上未能自动找到玩家实例，将等待 '{PlayerChange}' 事件来配置目标。", this);
//             }
//         }
//         else
//         {
//             // 如果用户在Inspector中预设了目标，则直接使用
//             HandlePlayerChange(targetSolver, targetActor);
//         }
//     }
//
//     /// <summary>
//     /// 响应 "PlayerChange" 事件，更新监听的目标。
//     /// </summary>
//     private void HandlePlayerChange(ObiSolver newSolver, ObiActor newActor)
//     {
//         Debug.Log($"[ObiCollisionTrigger] 在 '{name}' 上接收到PlayerChange事件，更新目标...", this);
//
//         // 1. 先从旧的Solver上取消注册
//         if (targetSolver != null)
//         {
//             targetSolver.OnCollision -= HandleSolverCollision;
//         }
//
//         // 2. 更新内部存储的Solver和Actor
//         targetSolver = newSolver;
//         targetActor = newActor;
//
//         // 3. 在新的Solver上注册碰撞回调
//         if (targetSolver != null && targetActor != null)
//         {
//             targetSolver.OnCollision += HandleSolverCollision;
//             Debug.Log($"[ObiCollisionTrigger] 在 '{name}' 上成功切换监听目标。新目标: Actor '{targetActor.name}' on Solver '{targetSolver.name}'。", this);
//         }
//         else
//         {
//              Debug.LogWarning($"[ObiCollisionTrigger] 在 '{name}' 上接收到的新目标为null，已停止监听。", this);
//         }
//     }
//
//     /// <summary>
//     /// Obi Solver触发的原始碰撞处理函数。
//     /// </summary>
//     private void HandleSolverCollision(ObiSolver solver, ObiNativeContactList contacts)
//     {
//         // 使用工具类来解析碰撞对
//         for (var i = 0; i < contacts.count; ++i)
//         {
//             if (!ObiCollisionUtils.TryParseActorColliderPair(contacts[i], solver, out var hitActor,
//                     out var hitCollider)) continue;
//             if (hitActor != targetActor || hitCollider != thisCollider) continue;
//             onCollisionConfirmed?.Invoke();
//             return;
//         }
//     }
// }