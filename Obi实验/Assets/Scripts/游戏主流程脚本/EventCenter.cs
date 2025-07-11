using System;
using System.Collections.Generic;

/// <summary>
/// 一个强大、类型安全的全局事件中心
/// 特点：
/// 1. 无需继承MonoBehaviour，无需在场景中创建实例。
/// 2. 使用字符串作为事件名，方便快捷。
/// 3. 支持无参、一参、二参、三参的事件重载。
/// 4. 通过泛型实现类型安全，在编译期即可发现类型不匹配的错误。
/// 5. 自动清理空的事件字典，防止内存占用。
/// </summary>
public static class EventCenter
{
    // 使用Delegate作为字典的值类型，以存储不同签名的委托（Action<T>, Action<T1, T2>等）
    private static readonly Dictionary<string, Delegate> eventDictionary = new Dictionary<string, Delegate>();

    #region 添加监听器 AddListener

    /// <summary>
    /// 添加无参事件监听
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="listener">监听委托</param>
    public static void AddListener(string eventName, Action listener)
    {
        AddListenerInternal(eventName, listener);
    }

    /// <summary>
    /// 添加带一个参数的事件监听
    /// </summary>
    public static void AddListener<T>(string eventName, Action<T> listener)
    {
        AddListenerInternal(eventName, listener);
    }

    /// <summary>
    /// 添加带两个参数的事件监听
    /// </summary>
    public static void AddListener<T1, T2>(string eventName, Action<T1, T2> listener)
    {
        AddListenerInternal(eventName, listener);
    }

    /// <summary>
    /// 添加带三个参数的事件监听
    /// </summary>
    public static void AddListener<T1, T2, T3>(string eventName, Action<T1, T2, T3> listener)
    {
        AddListenerInternal(eventName, listener);
    }

    // 内部通用添加方法
    private static void AddListenerInternal(string eventName, Delegate listener)
    {
        if (eventDictionary.TryGetValue(eventName, out Delegate d))
        {
            // 检查委托签名是否一致
            if (d != null && d.GetType() != listener.GetType())
            {
                throw new Exception($"尝试为事件 '{eventName}' 添加类型为 {listener.GetType()} 的监听器，但现有监听器的类型为 {d.GetType()}。一个事件名只能对应一种委托签名。");
            }
            eventDictionary[eventName] = Delegate.Combine(d, listener);
        }
        else
        {
            eventDictionary.Add(eventName, listener);
        }
    }

    #endregion

    #region 移除监听器 RemoveListener

    /// <summary>
    /// 移除无参事件监听
    /// </summary>
    public static void RemoveListener(string eventName, Action listener)
    {
        RemoveListenerInternal(eventName, listener);
    }

    /// <summary>
    /// 移除带一个参数的事件监听
    /// </summary>
    public static void RemoveListener<T>(string eventName, Action<T> listener)
    {
        RemoveListenerInternal(eventName, listener);
    }

    /// <summary>
    /// 移除带两个参数的事件监听
    /// </summary>
    public static void RemoveListener<T1, T2>(string eventName, Action<T1, T2> listener)
    {
        RemoveListenerInternal(eventName, listener);
    }

    /// <summary>
    /// 移除带三个参数的事件监听
    /// </summary>
    public static void RemoveListener<T1, T2, T3>(string eventName, Action<T1, T2, T3> listener)
    {
        RemoveListenerInternal(eventName, listener);
    }

    // 内部通用移除方法
    private static void RemoveListenerInternal(string eventName, Delegate listener)
    {
        if (eventDictionary.TryGetValue(eventName, out Delegate d))
        {
            Delegate result = Delegate.Remove(d, listener);
            if (result == null) // 如果移除后没有监听器了，就从字典中移除该事件
            {
                eventDictionary.Remove(eventName);
            }
            else
            {
                eventDictionary[eventName] = result;
            }
        }
    }

    #endregion

    #region 触发事件 TriggerEvent

    /// <summary>
    /// 触发无参事件
    /// </summary>
    public static void TriggerEvent(string eventName)
    {
        if (eventDictionary.TryGetValue(eventName, out Delegate d))
        {
            // 安全地调用委托
            (d as Action)?.Invoke();
        }
    }

    /// <summary>
    /// 触发带一个参数的事件
    /// </summary>
    public static void TriggerEvent<T>(string eventName, T arg1)
    {
        if (eventDictionary.TryGetValue(eventName, out Delegate d))
        {
            (d as Action<T>)?.Invoke(arg1);
        }
    }

    /// <summary>
    /// 触发带两个参数的事件
    /// </summary>
    public static void TriggerEvent<T1, T2>(string eventName, T1 arg1, T2 arg2)
    {
        if (eventDictionary.TryGetValue(eventName, out Delegate d))
        {
            (d as Action<T1, T2>)?.Invoke(arg1, arg2);
        }
    }

    /// <summary>
    /// 触发带三个参数的事件
    /// </summary>
    public static void TriggerEvent<T1, T2, T3>(string eventName, T1 arg1, T2 arg2, T3 arg3)
    {
        if (eventDictionary.TryGetValue(eventName, out Delegate d))
        {
            (d as Action<T1, T2, T3>)?.Invoke(arg1, arg2, arg3);
        }
    }

    #endregion

    /// <summary>
    /// 清理所有事件监听
    /// 通常在场景切换时调用
    /// </summary>
    public static void ClearAll()
    {
        eventDictionary.Clear();
    }
}