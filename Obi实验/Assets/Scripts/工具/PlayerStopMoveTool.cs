// Filename: PlayerStopMoveTool.cs
using UnityEngine;

/// <summary>
/// 一个工具脚本，用于在启用时停止玩家（PlayerControl_Ball）的移动。
/// 可以在启用时选择是否同时禁用玩家的输入控制。
/// </summary>
public class PlayerStopMoveTool : MonoBehaviour
{
    [Tooltip("如果勾选，启用此脚本时会完全禁用玩家的输入控制。如果不勾选，则只清除一次速度，玩家仍可移动。")]
    public bool disablePlayerControl = true;

    // 私有变量，用于记录此脚本是否禁用了玩家控制，以便在之后恢复。
    private bool _didDisableControl = false;

    /// <summary>
    /// 当此脚本组件或其GameObject被启用时调用。
    /// </summary>
    private void OnEnable()
    {
        // 尝试获取玩家控制的单例
        var player = PlayerControl_Ball.instance;
        if (player == null)
        {
            Debug.LogWarning("PlayerStopMoveTool: 场景中未找到 PlayerControl_Ball 的实例。");
            return;
        }

        // 无论如何，首先清除玩家当前的速度
        player.ClearMove();

        // 如果设置了要禁用玩家控制
        if (disablePlayerControl)
        {
            // 禁用玩家控制，并记录是我们这个脚本干的
            player.playerControl = false;
            _didDisableControl = true;
        }
    }

    /// <summary>
    /// 当此脚本组件或其GameObject被禁用时调用。
    /// </summary>
    private void OnDisable()
    {
        // 检查之前是否是这个脚本禁用了玩家控制
        if (_didDisableControl)
        {
            // 再次获取玩家实例
            var player = PlayerControl_Ball.instance;
            if (player != null)
            {
                // 恢复玩家的控制权
                player.playerControl = true;
            }

            // 重置标记
            _didDisableControl = false;
        }
    }
}