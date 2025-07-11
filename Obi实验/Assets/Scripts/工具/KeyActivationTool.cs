using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 按键激活工具。
/// 在启用时，监听指定的按键。按下按键后，触发一个可配置的 UnityEvent。
/// 同时，在启用和禁用时，通过 EventCenter 发布事件，以便UI系统可以显示或隐藏对应的按键提示。
/// </summary>
public class KeyActivationTool : MonoBehaviour
{
    [Header("按键设置")]
    [Tooltip("指定要监听的按键")]
    public KeyCode activationKey = KeyCode.E;

    [Header("功能与描述")]
    [Tooltip("对此按键功能的描述，将用于UI提示")]
    [TextArea(2, 5)] // 在Inspector中提供一个更大的文本输入框
    public string description = "进行交互";

    [Header("触发事件")]
    [Tooltip("当按下指定按键时，此处配置的事件将被调用")]
    public UnityEvent onKeyPressed;

    /// <summary>
    /// 当脚本组件或其GameObject被启用时调用。
    /// </summary>
    private void OnEnable()
    {
        // 发布“按键激活”事件，并将此脚本实例作为参数传递。
        // UI管理器可以监听此事件来显示提示。
        EventCenter.TriggerEvent<KeyActivationTool>("ButtonActive", this);
    }

    /// <summary>
    /// 每一帧调用。
    /// </summary>
    private void Update()
    {
        // 使用老输入系统检查按键是否被按下
        if (Input.GetKeyDown(activationKey))
        {
            // 如果按键被按下，调用在Inspector中配置的UnityEvent
            onKeyPressed?.Invoke();
        }
    }

    /// <summary>
    /// 当脚本组件或其GameObject被禁用时调用。
    /// </summary>
    private void OnDisable()
    {
        // 发布“按键禁用”事件，并将此脚本实例作为参数传递。
        // UI管理器可以监听此事件来隐藏对应的提示。
        EventCenter.TriggerEvent<KeyActivationTool>("ButtonDisable", this);
    }
}