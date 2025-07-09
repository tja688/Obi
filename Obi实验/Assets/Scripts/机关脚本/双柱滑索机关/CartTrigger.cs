using UnityEngine;
using Obi;
using System;

/// <summary>
/// 小车触发器 (改造后)
/// 职责：仅负责自身的业务逻辑（抓取玩家）。碰撞检测直接依赖Solver事件。
/// </summary>
public class CartTrigger : MonoBehaviour
{
    [Header("核心引用")]
    [SerializeField] private PillarController pillarController;
    [SerializeField] private ObiCollider cartObiCollider; 

    [Header("核心求解器与目标Actor")]
    [Tooltip("场景中驱动物理的核心Obi Solver")]
    [SerializeField] private ObiSolver targetSolver; // [新增]
    [Tooltip("代表玩家的Obi Actor")]
    [SerializeField] private ObiActor playerActor;   // [新增]

    private ObiParticleAttachment _playerAttachment;
    
    // [删除] 不再需要外部管理器的回调实例

    void Start()
    {
        if (PlayerControl_Ball.instance != null)
        {
            _playerAttachment = PlayerControl_Ball.instance.GetComponent<ObiParticleAttachment>();
        }

        // [改造] 确保所有新引用都已就绪
        if (cartObiCollider == null || targetSolver == null || playerActor == null)
        {
            Debug.LogError("CartTrigger初始化失败：请在Inspector中指定Cart Collider, Target Solver 和 Player Actor！", this);
            enabled = false;
            return;
        }
        
        // [改造] 直接向Solver注册
        targetSolver.OnCollision += HandleCollision;
    }
    
    void OnDestroy()
    {
        // [改造] 直接向Solver取消注册
        if (targetSolver != null)
        {
            targetSolver.OnCollision -= HandleCollision;
        }
    }

    // [改造] 新的统一碰撞处理函数
    private void HandleCollision(ObiSolver solver, ObiNativeContactList contacts)
    {
        if (solver != targetSolver) return;
        
        var world = ObiColliderWorld.GetInstance();

        foreach (Oni.Contact contact in contacts)
        {
            var pair = ObiUtils.GetColliderActorPair(solver, world, contact);
            if (pair == null) continue;

            // 验证: 碰撞方必须是玩家Actor，且碰撞体必须是这个小车的Collider
            if (pair.Value.actor == playerActor && pair.Value.collider == cartObiCollider)
            {
                // 所有条件满足！执行核心业务逻辑
                ExecutePlayerGrabbing();
                break; 
            }
        }
    }

    private void ExecutePlayerGrabbing()
    {
        if (_playerAttachment == null || _playerAttachment.enabled || (pillarController != null && pillarController.IsAttachmentInGracePeriod)) return;
        
        Debug.Log("玩家碰撞到小车 (已通过Solver/Actor/Collider验证)，开始抓取并请求升降...");
                
        _playerAttachment.BindToTarget(this.transform);
        _playerAttachment.enabled = true;

        if (pillarController != null)
        {
            pillarController.RequestPillarSwap();
        }
    }
}