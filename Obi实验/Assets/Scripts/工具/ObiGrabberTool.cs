using System;
using UnityEngine;
using Obi;

/// <summary>
/// 提供通用的玩家抓取和释放功能。激活时自动查找玩家并抓取，失活时自动释放。
/// </summary>
public class PlayerObiGrabber : MonoBehaviour
{
    [Header("抓取设置")]
    [Tooltip("玩家被抓取后吸附的目标点。如果留空，将默认使用本对象的Transform。")]
    [SerializeField] private Transform grabTarget;
    
    private void OnEnable()
    {
        if (grabTarget == null)
            grabTarget = this.transform;
        
        ObiAttachmentManager.Instance.GrabPlayer(this.grabTarget);

    }
    
    private void OnDisable()
    {
        ObiAttachmentManager.Instance.ReleasePlayer();
    }
}
