using UnityEngine;
using Obi;
using System;

public class CartTrigger : MonoBehaviour
{
    [Header("核心引用")]
    [SerializeField] private PillarController pillarController;
    [SerializeField] private ObiCollider cartObiCollider; 

    [Header("核心求解器与目标Actor")]
    [Tooltip("【重要】这里必须引用【玩家Actor】所在的那个Solver")]
    [SerializeField] private ObiSolver playerSolver; 
    [Tooltip("代表玩家的Obi Actor")]
    [SerializeField] private ObiActor playerActor;

    [Header("调试设置")]
    [Tooltip("勾选后，将在控制台打印详细的碰撞检测日志")]
    [SerializeField] private bool enableDebugLogging = true;

    private ObiParticleAttachment playerAttachment;
    
    void Start()
    {
        if (PlayerControl_Ball.instance != null)
        {
            playerAttachment = PlayerControl_Ball.instance.GetComponent<ObiParticleAttachment>();
        }

        if (cartObiCollider == null || playerSolver == null || playerActor == null)
        {
            Debug.LogError("CartTrigger初始化失败：请在Inspector中指定Cart Collider, Player Solver 和 Player Actor！", this);
            enabled = false;
            return;
        }
        
        playerSolver.OnCollision += HandleCollision;
    }
    
    void OnDestroy()
    {
        if (playerSolver != null)
        {
            playerSolver.OnCollision -= HandleCollision;
        }
    }

    private void HandleCollision(ObiSolver solver, ObiNativeContactList contacts)
    {
        if (solver != playerSolver) return;
        
        var world = ObiColliderWorld.GetInstance();

        for (int i = 0; i < contacts.count; i++)
        {
            var contact = contacts[i];
            
            if (TryParseCollisionPair(contact, world, out ObiActor hitActor, out ObiColliderBase hitCollider))
            {
                if (enableDebugLogging)
                {
                    Debug.Log($"[CartTrigger] 解码成功: Actor='{hitActor?.name ?? "NULL"}' <--> Collider='{hitCollider?.name ?? "NULL"}'");
                }
                
                if (hitActor == playerActor && hitCollider == cartObiCollider)
                {
                    if(enableDebugLogging)
                    {
                        Debug.Log($"<color=lime>[CartTrigger] 验证成功! 玩家 '{playerActor.name}' 撞到了小车 '{cartObiCollider.name}'。执行逻辑...</color>");
                    }
                    
                    ExecutePlayerGrabbing();
                    return; 
                }
            }
        }
    }

    private bool TryParseCollisionPair(Oni.Contact contact, ObiColliderWorld world, out ObiActor actor, out ObiColliderBase collider)
    {
        actor = null;
        collider = null;

        if (IsParticleFromOurActor(contact.bodyA))
        {
            actor = this.playerActor;
            if (contact.bodyB >= 0 && contact.bodyB < world.colliderHandles.Count)
            {
                collider = world.colliderHandles[contact.bodyB].owner;
            }
        }
        else if (IsParticleFromOurActor(contact.bodyB))
        {
            actor = this.playerActor;
            if (contact.bodyA >= 0 && contact.bodyA < world.colliderHandles.Count)
            {
                collider = world.colliderHandles[contact.bodyA].owner;
            }
        }

        return actor != null && collider != null;
    }

    private bool IsParticleFromOurActor(int particleSolverIndex)
    {
        if (playerSolver == null || !playerSolver.gameObject.activeInHierarchy || particleSolverIndex < 0 || particleSolverIndex >= playerSolver.particleToActor.Length)
            return false;
            
        var p = playerSolver.particleToActor[particleSolverIndex];
        return p != null && p.actor == this.playerActor;
    }

    private void ExecutePlayerGrabbing()
    {
        if (playerAttachment == null || playerAttachment.enabled || (pillarController != null && pillarController.isAttachmentInGracePeriod)) return;
        
        Debug.Log("玩家碰撞到小车 (已通过Solver/Actor/Collider验证)，开始抓取并请求升降...");
                
        playerAttachment.BindToTarget(this.transform);
        playerAttachment.enabled = true;

        if (pillarController != null)
        {
            pillarController.RequestPillarSwap();
        }
    }
}