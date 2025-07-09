using UnityEngine;
using Obi;

/// <summary>
/// 小车触发器（最终版）：
/// 新增职责：在检测到与玩家碰撞后，负责【抓取】玩家。
/// 修改：解耦Solver，现在监听指定Player Solver的碰撞事件。
/// </summary>
public class CartTrigger : MonoBehaviour
{
    [Header("核心引用")]
    [SerializeField] private PillarController pillarController;
    [Tooltip("需要监听其碰撞事件的玩家Solver")]
    [SerializeField] private ObiSolver playerSolver; // **修改点1: 明确引用玩家的Solver**
    [SerializeField] private ObiCollider cartObiCollider;
    
    private ObiActor playerActor;
    private ObiParticleAttachment playerAttachment;

    void Start()
    {
        // 确保关键引用存在
        if (playerSolver == null)
        {
            Debug.LogError("CartTrigger: 玩家Solver(playerSolver)未指定！", this);
            return;
        }
        
        // 获取一次玩家实例即可，避免重复查找
        if (PlayerControl_Ball.instance != null)
        {
            playerActor = PlayerControl_Ball.instance.GetComponent<ObiActor>();
            playerAttachment = PlayerControl_Ball.instance.GetComponent<ObiParticleAttachment>();
        }
        else
        {
            Debug.LogError("CartTrigger: 无法找到玩家实例(PlayerControl_Ball.instance)！", this);
        }
    }

    // **修改点2: 监听和取消监听playerSolver**
    void OnEnable() { if (playerSolver != null) playerSolver.OnCollision += OnObiPlayerCollisionWithCart; }
    void OnDisable() { if (playerSolver != null) playerSolver.OnCollision -= OnObiPlayerCollisionWithCart; }

    // **修改点3: 使用事件传入的solver作为上下文**
    private void OnObiPlayerCollisionWithCart(ObiSolver solver, ObiNativeContactList contacts)
    {
        // 检查冷却状态
        if (playerAttachment == null || playerAttachment.enabled || (pillarController != null && pillarController.IsAttachmentInGracePeriod)) return;

        for (int i = 0; i < contacts.count; i++)
        {
            var contact = contacts[i];
            // **修改点4: 将正确的solver上下文传入辅助函数**
            if ((IsPlayerParticle(solver, contact.bodyA) && IsThisCartCollider(contact.bodyB)) ||
                (IsPlayerParticle(solver, contact.bodyB) && IsThisCartCollider(contact.bodyA)))
            {
                Debug.Log("玩家碰撞到小车，开始抓取并请求升降...");
                
                // 使用ObiParticleAttachment的标准方式来附加目标
                playerAttachment.BindToTarget(this.transform);
                playerAttachment.enabled = true;

                if (pillarController)
                {
                    pillarController.RequestPillarSwap();
                }
                
                return; // 处理完一次碰撞即可退出，防止重复触发
            }
        }
    }

    // **修改点5: 辅助函数接收solver上下文**
    private bool IsPlayerParticle(ObiSolver solverContext, int particleIndex) 
    { 
        if (playerActor == null || particleIndex < 0 || particleIndex >= solverContext.particleToActor.Length) return false;
        var actorHandle = solverContext.particleToActor[particleIndex];
        return actorHandle != null && actorHandle.actor == playerActor;
    }

    private bool IsThisCartCollider(int colliderIndex) 
    { 
        var world = ObiColliderWorld.GetInstance();
        if (colliderIndex < 0 || colliderIndex >= world.colliderHandles.Count) return false;
        var owner = world.colliderHandles[colliderIndex].owner;
        return owner == cartObiCollider;
    }
}