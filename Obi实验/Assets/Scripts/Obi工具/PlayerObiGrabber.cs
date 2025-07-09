using UnityEngine;
using Obi;

/// <summary>
/// 提供通用的玩家抓取和释放功能。
/// 既可以响应 ObiCollisionTriggerTool 的碰撞事件自动抓取，
/// 也可以通过公共方法被其他脚本手动调用。
/// </summary>
public class PlayerObiGrabber : MonoBehaviour
{
    [Header("碰撞触发器 (可选)")]
    [Tooltip("关联的碰撞触发器。如果留空，将自动查找挂载在本对象上的同名组件。")]
    [SerializeField] private ObiCollisionTriggerTool collisionTrigger;

    [Header("抓取设置")]
    [Tooltip("玩家被抓取后吸附的目标点。如果留空，将默认使用本对象的Transform。")]
    [SerializeField] private Transform grabTarget;

    // --- 内部变量 ---
    private ObiParticleAttachment playerAttachment;
    private bool isPlayerGrabbed = false;

    #region Unity生命周期
    void Start()
    {
        // 1. 自动配置抓取目标点
        if (grabTarget == null)
        {
            grabTarget = this.transform;
        }

        // 2. 自动配置碰撞触发器
        if (collisionTrigger == null)
        {
            collisionTrigger = GetComponent<ObiCollisionTriggerTool>();
        }

        // 3. 如果找到了碰撞触发器，则订阅其静态事件
        if (collisionTrigger != null)
        {
            // 注意：这里我们订阅的是静态事件，所以即使有多个Grabber，它们也都会收到通知
            ObiCollisionTriggerTool.OnObiCollisionTriggered += HandleCollisionTriggered;
            Debug.Log($"[PlayerGrabber] 在 '{name}' 上已成功订阅碰撞事件。", this);
        }
        else
        {
            Debug.Log($"[PlayerGrabber] 在 '{name}' 上未找到碰撞触发器，将只响应手动调用。", this);
        }
    }

    void OnDestroy()
    {
        // 在对象销毁时，务必取消订阅，防止内存泄漏
        if (collisionTrigger != null)
        {
            ObiCollisionTriggerTool.OnObiCollisionTriggered -= HandleCollisionTriggered;
        }
    }
    #endregion

    #region 事件处理
    /// <summary>
    /// 处理来自全局碰撞触发器的事件。
    /// </summary>
    private void HandleCollisionTriggered(ObiCollider triggeredCollider, ObiActor collidingActor)
    {
        // 检查这个事件是否是由我们自己关联的那个触发器引发的
        if (triggeredCollider == collisionTrigger.GetComponent<ObiCollider>())
        {
            Debug.Log($"[PlayerGrabber] '{name}' 接收到自己的碰撞事件，准备抓取玩家...", this);
            GrabPlayer();
        }
    }
    #endregion

    #region 公共API (抓取与释放核心逻辑)

    /// <summary>
    /// 执行抓取玩家的逻辑。
    /// </summary>
    public void GrabPlayer()
    {
        // 如果已经被抓取，则不重复执行
        if (isPlayerGrabbed) return;

        // 通过玩家单例安全地获取ObiParticleAttachment组件
        if (PlayerControl_Ball.instance == null)
        {
            Debug.LogError("[PlayerGrabber] 无法抓取：找不到 PlayerControl_Ball 的单例！");
            return;
        }
        
        playerAttachment = PlayerControl_Ball.instance.GetComponent<ObiParticleAttachment>();

        if (playerAttachment == null)
        {
            Debug.LogError("[PlayerGrabber] 无法抓取：玩家对象上没有找到 ObiParticleAttachment 组件！");
            return;
        }

        Debug.Log($"<color=cyan>[PlayerGrabber] 执行抓取！目标: '{grabTarget.name}'</color>", this);
        
        playerAttachment.BindToTarget(this.grabTarget);
        playerAttachment.enabled = true;
        
        isPlayerGrabbed = true;
    }

    /// <summary>
    /// 执行释放玩家的逻辑。
    /// </summary>
    public void ReleasePlayer()
    {
        // 如果未被抓取，则不执行
        if (!isPlayerGrabbed) return;
        
        if (playerAttachment == null)
        {
             // 即使playerAttachment为空，也要重置状态，以防逻辑卡死
            isPlayerGrabbed = false;
            return;
        }

        Debug.Log($"<color=yellow>[PlayerGrabber] 执行释放！</color>", this);
        
        // 释放逻辑：禁用组件并清空目标
        playerAttachment.enabled = false;
        playerAttachment.target = null;

        isPlayerGrabbed = false;
    }

    #endregion
}
