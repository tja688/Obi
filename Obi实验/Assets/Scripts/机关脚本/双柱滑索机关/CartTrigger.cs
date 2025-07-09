using UnityEngine;
using Obi;
using System; // 需要引入System命名空间来使用Action

/// <summary>
/// 小车触发器 (最终重构版)
/// 职责：仅负责自身的业务逻辑（抓取玩家）。碰撞检测完全委托给ObiCollisionManager。
/// </summary>
public class CartTrigger : MonoBehaviour
{
    [Header("核心引用")]
    [SerializeField] private PillarController pillarController;
    [SerializeField] private ObiCollider cartObiCollider; // 只需要引用自身的碰撞体

    private ObiParticleAttachment _playerAttachment;

    // 为了能在Unregister时正确取消，需要保存回调方法的实例
    private Action<Oni.Contact> _onPlayerCollisionAction;

    void Start()
    {
        // 获取玩家附件组件
        if (PlayerControl_Ball.instance != null)
        {
            _playerAttachment = PlayerControl_Ball.instance.GetComponent<ObiParticleAttachment>();
        }

        // 确保所有引用都已就绪
        if (cartObiCollider == null || ObiCollisionManager.Instance == null)
        {
            Debug.LogError("CartTrigger初始化失败：请检查ObiCollider和ObiCollisionManager实例！", this);
            return;
        }

        // 创建回调方法的委托实例
        _onPlayerCollisionAction = HandlePlayerCollision;
        
        // 向管理器注册： "嘿，管理器，请在'cartObiCollider'和玩家碰撞时，调用我的'HandlePlayerCollision'方法"
        // 注意：我们没有提供第三个参数(targetActor)，所以管理器会自动设为玩家。
        ObiCollisionManager.Instance.RegisterCollisionCallback(cartObiCollider, _onPlayerCollisionAction);
    }
    
    void OnDestroy()
    {
        // 在对象销毁时，务必向管理器取消注册，防止内存泄漏和空引用
        if (ObiCollisionManager.Instance != null && cartObiCollider != null)
        {
            ObiCollisionManager.Instance.UnregisterCollisionCallback(cartObiCollider, _onPlayerCollisionAction);
        }
    }

    /// <summary>
    /// 这是碰撞发生时，由管理器回调的专属业务逻辑处理函数。
    /// </summary>
    /// <param name="contact">由管理器传回的碰撞点信息，目前未使用，但保留以备将来扩展。</param>
    private void HandlePlayerCollision(Oni.Contact contact)
    {
        // 检查冷却状态
        if (_playerAttachment == null || _playerAttachment.enabled || (pillarController != null && pillarController.IsAttachmentInGracePeriod)) return;
        
        Debug.Log("玩家碰撞到小车 (由Manager通知)，开始抓取并请求升降...");
                
        // 执行核心的抓取逻辑
        _playerAttachment.BindToTarget(this.transform);
        _playerAttachment.enabled = true;

        if (pillarController != null)
        {
            pillarController.RequestPillarSwap();
        }
    }
}