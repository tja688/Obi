using UnityEngine;

/// <summary>
/// 挂载在“护盾”预制体上，用于追踪其状态。
/// </summary>
public class GuardProxy : MonoBehaviour
{
    // 用于实现“惰性消失”的计时器
    public int framesSinceLastContact = 0;

    /// <summary>
    /// 当从对象池取出并激活时调用
    /// </summary>
    public void Initialize()
    {
        gameObject.SetActive(true);
        framesSinceLastContact = 0;
    }

    /// <summary>
    /// 当被外部碰撞体接触时，由主控制器调用来重置计时器
    /// </summary>
    public void NotifyContact()
    {
        framesSinceLastContact = 0;
    }

    /// <summary>
    /// 回收到对象池之前调用
    /// </summary>
    public void Deactivate()
    {
        gameObject.SetActive(false);
    }
}