// IControllable.cs
using UnityEngine;

/// <summary>
/// 定义了一个可被PlayerController控制的对象的通用接口。
/// 任何希望接收玩家输入的脚本都应实现此接口。
/// </summary>
public interface IControllable
{
    /// <summary>
    /// 获取该可控对象关联的GameObject。
    /// </summary>
    GameObject controlledGameObject { get; }

    /// <summary>
    /// 处理移动输入。
    /// </summary>
    /// <param name="moveVector">2D移动输入向量 (x, y)。</param>
    void Move(Vector2 moveVector);
    
    /// <summary>
    /// 处理移动清理。
    /// </summary>
    void ClearMove();

    /// <summary>
    /// 处理跳跃输入。
    /// </summary>
    void Jump();

    /// <summary>
    /// 处理交互输入。
    /// </summary>
    void Interact();
    
    
}