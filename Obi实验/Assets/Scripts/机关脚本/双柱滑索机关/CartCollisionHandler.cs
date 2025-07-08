using System;
using UnityEngine;

/// <summary>
/// 车辆碰撞处理器（已更新）：
/// 1. 检测与【触发球】的碰撞。
/// 2. 通知PillarController将车辆设置为【触发球的父对象】的子对象。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class CartCollisionHandler : MonoBehaviour
{
    [Header("核心引用")]
    [Tooltip("场景中挂载了PillarController脚本的对象")]
    [SerializeField] private PillarController pillarController;

    [Header("碰撞目标设置")]
    [Tooltip("柱子【触发球】的Tag (请确保您的触发球有此Tag)")]
    [SerializeField] private string pillarTriggerTag = "PillarTrigger"; // 建议为触发球使用一个专门的Tag

    [Obsolete("Obsolete")]
    void Start()
    {
        if (pillarController == null)
        {
            pillarController = FindObjectOfType<PillarController>();
            if (pillarController == null)
            {
                Debug.LogError($"[{name}] 未在Inspector中指定 PillarController，且自动查找失败！", this);
                enabled = false;
            }
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (pillarController == null) return;
        if (pillarController.IsCartInGracePeriod) return;

        // **[已修改]** 检查碰撞对象的Tag是否为我们设定的【触发球Tag】
        if (collision.gameObject.CompareTag(pillarTriggerTag))
        {
            // **[已修改]** 调用控制器的锁定方法，并传入【触发球的父对象】的Transform
            // 这是实现新架构的关键一步
            Transform parentToLockTo = collision.transform.parent;
            if (parentToLockTo != null)
            {
                pillarController.LockCartToPillarParent(parentToLockTo);
            }
            else
            {
                Debug.LogWarning($"碰撞到的触发球 {collision.gameObject.name} 没有父对象!", this);
            }
        }
    }
}