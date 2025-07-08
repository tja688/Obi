using UnityEngine;
using Obi;

/// <summary>
/// 小车触发器（最终版）：
/// 新增职责：在检测到与玩家碰撞后，负责【抓取】玩家。
/// </summary>
public class CartTrigger : MonoBehaviour
{
    [Header("核心引用")]
    [SerializeField] private PillarController pillarController;
    [SerializeField] private ObiSolver obiSolver;
    [SerializeField] private ObiActor playerActor;
    [SerializeField] private ObiCollider cartObiCollider;
    [SerializeField] private ObiParticleAttachment playerAttachment;

    // ... Start方法无需修改 ...
    void Start() { if (pillarController == null || obiSolver == null || playerActor == null || cartObiCollider == null || playerAttachment == null) { Debug.LogError($"[{name}] 核心组件引用未设置，请检查Inspector！脚本已禁用。", this); enabled = false; } }

    void OnEnable() { if (obiSolver != null) obiSolver.OnCollision += OnObiPlayerCollisionWithCart; }
    void OnDisable() { if (obiSolver != null) obiSolver.OnCollision -= OnObiPlayerCollisionWithCart; }

    private void OnObiPlayerCollisionWithCart(ObiSolver solver, ObiNativeContactList contacts)
    {
        // [修改] 增加对两种冷却状态的检查
        if (playerAttachment.enabled || pillarController.IsAttachmentInGracePeriod) return;

        for (int i = 0; i < contacts.count; i++)
        {
            var contact = contacts[i];
            if ((IsPlayerParticle(contact.bodyA) && IsThisCartCollider(contact.bodyB)) ||
                (IsPlayerParticle(contact.bodyB) && IsThisCartCollider(contact.bodyA)))
            {
                Debug.Log("玩家碰撞到小车，开始抓取并请求升降...");
                
                playerAttachment.enabled = false;
                playerAttachment.BindToTarget(this.transform);
                playerAttachment.enabled = true;

                pillarController.RequestPillarSwap();
                return;
            }
        }
    }

    private bool IsPlayerParticle(int p) { if(p<0||p>=obiSolver.particleToActor.Length)return false;var i=obiSolver.particleToActor[p];return i!=null&&i.actor==playerActor; }
    private bool IsThisCartCollider(int c) { var w=ObiColliderWorld.GetInstance();if(c<0||c>=w.colliderHandles.Count)return false;var o=w.colliderHandles[c].owner;return o==cartObiCollider; }
}