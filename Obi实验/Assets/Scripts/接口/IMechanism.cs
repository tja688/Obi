// IMechanism.cs
using UnityEngine;

/// <summary>
/// 定义了一个可与MechanismController交互的机关对象的通用接口。
/// 任何希望接收机关输入的脚本都应实现此接口。
/// </summary>
public interface IMechanism
{
    /// <summary>
    /// 获取该机关关联的GameObject。
    /// </summary>
    GameObject mechanismGameObject { get; }

    /// <summary>
    /// 处理鼠标移动输入。传递的是屏幕坐标位置。
    /// </summary>
    /// <param name="position">鼠标在屏幕上的当前位置 (Vector2)。</param>
    void OnMouseMove(Vector2 position);

    /// <summary>
    /// 处理鼠标左键输入。
    /// </summary>
    /// <param name="isPressed">如果左键被按下则为true，抬起则为false。</param>
    void OnLeftButton(bool isPressed);

    /// <summary>
    /// 处理鼠标右键输入。
    /// </summary>
    /// <param name="isPressed">如果右键被按下则为true，抬起则为false。</param>
    void OnRightButton(bool isPressed);
    
    /// <summary>
    /// 处理鼠标滚轮输入。
    /// </summary>
    /// <param name="scroll">滚轮的滚动向量。</param>
    void OnMouseWheel(Vector2 scroll);

    /// <summary>
    /// 处理退出机关操作的输入。
    /// </summary>
    void OnQuit();
}