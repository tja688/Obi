using UnityEngine;
using Obi;

/// <summary>
/// 管理 ObiParticleAttachment 的单例类，用于简化对粒子附着的抓取和释放操作。
/// 需要与 ObiParticleAttachment 和 ObiActor 组件在同一个 GameObject 上。
/// </summary>
[RequireComponent(typeof(ObiParticleAttachment))]
public class ObiAttachmentManager : MonoBehaviour
{
    #region Singleton Pattern
    
    private static ObiAttachmentManager _instance;

    /// <summary>
    /// 获取 ObiAttachmentManager 的全局唯一实例。
    /// </summary>
    public static ObiAttachmentManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<ObiAttachmentManager>();
                if (_instance == null)
                {
                    Debug.LogError("场景中未找到 ObiAttachmentManager 实例。请确保已将其添加到带有 ObiParticleAttachment 的对象上。");
                }
            }
            return _instance;
        }
    }

    #endregion

    private ObiParticleAttachment particleAttachment;

    private void Awake()
    {
        // 实现单例模式
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        // 获取关联的 ObiParticleAttachment 组件
        particleAttachment = GetComponent<ObiParticleAttachment>();
        if (particleAttachment == null)
        {
            Debug.LogError("ObiAttachmentManager 无法找到 ObiParticleAttachment 组件！", this);
            return;
        }

        // 初始状态下禁用 Attachment
        particleAttachment.enabled = false;
    }

    /// <summary>
    /// 抓取一个目标 Transform。
    /// </summary>
    /// <param name="target">要抓取的目标对象。</param>
    /// <param name="useBindToTarget">抓取方式。true: 使用 BindToTarget 将粒子直接对齐到目标原点；false: 使用 Bind 保持粒子与目标的相对偏移。</param>
    /// <param name="isDynamic">抓取类型。true: 动态抓取 (使用约束，有弹性)；false: 静态抓取 (固定粒子，无弹性)。</param>
    public void GrabPlayer(Transform target, bool useBindToTarget = true, bool isDynamic = true)
    {
        if (target == null)
        {
            Debug.LogWarning("抓取目标(target)不能为空。");
            return;
        }

        if (particleAttachment == null)
        {
            Debug.LogError("ParticleAttachment 组件引用丢失！");
            return;
        }
        
        // 1. 设置抓取类型（动态/静态）
        particleAttachment.attachmentType = isDynamic 
            ? ObiParticleAttachment.AttachmentType.Dynamic 
            : ObiParticleAttachment.AttachmentType.Static;

        // 2. 根据选择的抓取方式设置目标并绑定
        if (useBindToTarget)
        {
            // 这种方式会立即将粒子对齐到目标中心
            particleAttachment.BindToTarget(target);
        }
        else
        {
            // 这种方式会保持粒子与目标之间的初始偏移量
            particleAttachment.target = target;
        }

        // 3. 启用 Attachment 组件以应用抓取效果
        particleAttachment.enabled = true;
    }

    /// <summary>
    /// 释放当前抓取的目标。
    /// </summary>
    public void ReleasePlayer()
    {
        if (particleAttachment == null)
        {
            Debug.LogError("ParticleAttachment 组件引用丢失！");
            return;
        }

        // 1. 禁用 Attachment 组件以解除约束
        particleAttachment.enabled = false;

        // 2. 将 target 设置为 null，彻底清除绑定关系
        // ObiParticleAttachment 的 target set 访问器会调用 Bind()，从而清理内部状态
        particleAttachment.target = null;
    }
}