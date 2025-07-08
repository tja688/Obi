using UnityEngine;
using Obi;

/// <summary>
/// 小车触发器（最终版）：
/// 新增职责：在检测到与玩家碰撞后，负责【抓取】玩家。
/// </summary>
public class CartTrigger : MonoBehaviour
{
    [Header("核心引用")]
    [Tooltip("场景中唯一的PillarController控制器")]
    [SerializeField] private PillarController pillarController;
    [Tooltip("场景中的ObiSolver")]
    [SerializeField] private ObiSolver obiSolver;
    [Tooltip("玩家的ObiActor组件")]
    [SerializeField] private ObiActor playerActor;
    [Tooltip("小车自身的ObiCollider组件")]
    [SerializeField] private ObiCollider cartObiCollider;
    // [新增] 对玩家身上Attachment脚本的引用
    [Tooltip("玩家对象身上挂载的ObiParticleAttachment脚本")]
    [SerializeField] private ObiParticleAttachment playerAttachment;

    void Start()
    {
        // ... (引用检查保持不变) ...
        if (pillarController == null || obiSolver == null || playerActor == null || cartObiCollider == null || playerAttachment == null)
        {
            Debug.LogError($"[{name}] 核心组件引用未设置，请检查Inspector！脚本已禁用。", this);
            enabled = false;
        }
    }

    void OnEnable() { if (obiSolver != null) obiSolver.OnCollision += OnObiPlayerCollisionWithCart; }
    void OnDisable() { if (obiSolver != null) obiSolver.OnCollision -= OnObiPlayerCollisionWithCart; }

    private void OnObiPlayerCollisionWithCart(ObiSolver solver, ObiNativeContactList contacts)
    {
        // 如果玩家已经被抓住了，就没必要再触发了
        if (playerAttachment.enabled) return;

        for (int i = 0; i < contacts.count; i++)
        {
            var contact = contacts[i];
            if ((IsPlayerParticle(contact.bodyA) && IsThisCartCollider(contact.bodyB)) ||
                (IsPlayerParticle(contact.bodyB) && IsThisCartCollider(contact.bodyA)))
            {
                Debug.Log("玩家碰撞到小车，开始抓取并请求升降...");
                
                // --- [新增] 抓取玩家的核心逻辑 ---
                // 步骤1 & 2: 确保脚本失活时，调用BindToTarget方法
                // (根据您的描述，脚本默认应为失活状态，所以第一步的失活是双重保险)
                playerAttachment.enabled = false;
                playerAttachment.BindToTarget(this.transform); // target是小车自身

                // 步骤3: 激活脚本，实施抓取
                playerAttachment.enabled = true;

                // 抓取后，立刻发出升降请求
                pillarController.RequestPillarSwap();
                return;
            }
        }
    }

    private bool IsPlayerParticle(int p) { if(p<0||p>=obiSolver.particleToActor.Length)return false;var i=obiSolver.particleToActor[p];return i!=null&&i.actor==playerActor; }
    private bool IsThisCartCollider(int c) { var w=ObiColliderWorld.GetInstance();if(c<0||c>=w.colliderHandles.Count)return false;var o=w.colliderHandles[c].owner;return o==cartObiCollider; }
}