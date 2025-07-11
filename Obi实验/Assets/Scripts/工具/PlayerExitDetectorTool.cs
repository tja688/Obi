using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 检测玩家是否超出指定距离的工具。
/// 当玩家与一个中心点之间的距离超过设定值时，触发一个UnityEvent。
/// </summary>
public class PlayerExitDetectorTool : MonoBehaviour
{
    [Header("检测中心点")]
    [Tooltip("用于计算距离的中心点。如果留空，将默认使用此脚本所在对象自身的Transform。")]
    [SerializeField] private Transform checkCenter;

    [Header("检测距离")]
    [Tooltip("当玩家与中心点的距离超过这个值时，将触发事件。")]
    [SerializeField] private float triggerDistance = 15f;

    [Header("事件响应")]
    [Tooltip("当玩家脱离指定距离范围时，将触发此处的事件。")]
    [SerializeField] private UnityEvent onPlayerExitedRange;

    // --- 内部变量 ---
    private Transform playerTransform;
    // 用于追踪玩家是否在范围内的核心状态变量
    private bool isPlayerWithinRange = true;

    private void Awake()
    {
        // 如果用户没有在Inspector中指定，则自动获取自身Transform作为中心点
        if (checkCenter == null)
        {
            checkCenter = transform;
        }
    }

    private void Start()
    {
        // 尝试获取玩家单例的Transform
        if (PlayerControl_Ball.instance != null)
        {
            playerTransform = PlayerControl_Ball.instance.transform;

            // 初始化时检查一次玩家是否就在范围外
            float initialDistance = Vector3.Distance(checkCenter.position, playerTransform.position);
            isPlayerWithinRange = (initialDistance <= triggerDistance);
        }
        else
        {
            // 如果找不到玩家，则禁用此脚本以防止报错
            Debug.LogError($"[PlayerExitDetectorTool] 在 '{name}' 上未能找到玩家实例 (PlayerControl_Ball.instance)，脚本将禁用。", this);
            enabled = false;
        }
    }

    private void Update()
    {
        // 如果没有有效的玩家或中心点，则不执行任何操作
        if (playerTransform == null || checkCenter == null) return;

        // 实时计算玩家与中心点之间的距离
        float currentDistance = Vector3.Distance(checkCenter.position, playerTransform.position);

        // 检查状态转换
        if (isPlayerWithinRange && currentDistance > triggerDistance)
        {
            // 状态从“在范围内”变为“在范围外”
            isPlayerWithinRange = false;
            Debug.Log($"[PlayerExitDetectorTool] 玩家已超出 {triggerDistance}米 范围。", this);
            onPlayerExitedRange?.Invoke(); // 触发事件
        }
        else if (!isPlayerWithinRange && currentDistance <= triggerDistance)
        {
            // 状态从“在范围外”变回“在范围内”
            isPlayerWithinRange = true;
            Debug.Log($"[PlayerExitDetectorTool] 玩家已重新进入范围。", this);
            // 这里可以根据需要添加一个“玩家重新进入”的事件
        }
    }

    /// <summary>
    /// 在Scene视图中绘制一个可视化的距离提示球，方便在编辑器中调整。
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // 确保即使在非播放模式下，当我们更改了checkCenter引用时，Gizmo也能正确显示
        Transform center = (checkCenter != null) ? checkCenter : transform;

        // 设置Gizmo颜色
        Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.3f); // 黄色，半透明
        
        // 绘制一个实心球体来表示范围
        Gizmos.DrawSphere(center.position, triggerDistance);
        
        // 同时绘制一个线框球体，让边界更清晰
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center.position, triggerDistance);
    }
}