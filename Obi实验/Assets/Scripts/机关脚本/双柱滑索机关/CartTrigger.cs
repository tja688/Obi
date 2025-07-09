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
    private ObiActor _playerActor;
    [SerializeField] private ObiCollider cartObiCollider;
    private ObiParticleAttachment _playerAttachment;

    void Start()
    {
        if (obiSolver == null)
        {
            obiSolver = FindFirstObjectByType<ObiSolver>();
        }
        
        _playerActor = PlayerControl_Ball.instance.GetComponent<ObiActor>();
        
        _playerAttachment = PlayerControl_Ball.instance.GetComponent<ObiParticleAttachment>();
    }

    void OnEnable() { if (obiSolver != null) obiSolver.OnCollision += OnObiPlayerCollisionWithCart; }
    void OnDisable() { if (obiSolver != null) obiSolver.OnCollision -= OnObiPlayerCollisionWithCart; }

    private void OnObiPlayerCollisionWithCart(ObiSolver solver, ObiNativeContactList contacts)
    {
        // [修改] 增加对两种冷却状态的检查
        if (_playerAttachment.enabled || pillarController.IsAttachmentInGracePeriod) return;

        for (int i = 0; i < contacts.count; i++)
        {
            var contact = contacts[i];
            if ((IsPlayerParticle(contact.bodyA) && IsThisCartCollider(contact.bodyB)) ||
                (IsPlayerParticle(contact.bodyB) && IsThisCartCollider(contact.bodyA)))
            {
                Debug.Log("玩家碰撞到小车，开始抓取并请求升降...");
                
                _playerAttachment.enabled = false;
                _playerAttachment.BindToTarget(this.transform);
                _playerAttachment.enabled = true;

                pillarController.RequestPillarSwap();
                return;
            }
        }
    }

    private bool IsPlayerParticle(int p) { if(p<0||p>=obiSolver.particleToActor.Length)return false;var i=obiSolver.particleToActor[p];return i!=null&&i.actor==_playerActor; }
    private bool IsThisCartCollider(int c) { var w=ObiColliderWorld.GetInstance();if(c<0||c>=w.colliderHandles.Count)return false;var o=w.colliderHandles[c].owner;return o==cartObiCollider; }
}