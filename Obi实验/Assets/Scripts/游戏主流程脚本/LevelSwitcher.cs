using UnityEngine;
using UnityEngine.SceneManagement; // 用于加载场景
using Obi; // 引用Obi命名空间

/// <summary>
/// 一个简单的关卡切换器。
/// 当任何Obi物体与指定的触发碰撞体接触时，加载新的关卡。
/// </summary>
public class LevelSwitcher : MonoBehaviour
{
    [Header("关卡切换设置")]
    [Tooltip("作为触发器的Obi碰撞体。当玩家碰到它时，切换关卡。")]
    public ObiCollider triggerCollider;

    [Tooltip("要加载的目标关卡的名称（必须与Build Settings中的场景名一致）。")]
    public string targetSceneName;

    private ObiSolver solver; // 用于缓存场景中的Obi求解器

    // 在脚本启动时调用
    private void Awake()
    {
        // 自动查找并获取场景中唯一的ObiSolver实例
        solver = FindFirstObjectByType<ObiSolver>();
        if (solver == null)
        {
            Debug.LogError("场景中找不到ObiSolver！此脚本无法工作。", this);
        }
    }

    // 当该组件被激活时调用
    private void OnEnable()
    {
        // 确保solver存在后，再订阅（监听）碰撞事件
        if (solver != null)
        {
            solver.OnCollision += Solver_OnCollision;
        }
    }

    // 当该组件被禁用时调用
    private void OnDisable()
    {
        // 取消订阅事件，这是一个好习惯，可以防止内存泄漏
        if (solver != null)
        {
            solver.OnCollision -= Solver_OnCollision;
        }
    }

    /// <summary>
    /// 这是处理碰撞事件的核心方法。
    /// </summary>
    private void Solver_OnCollision(ObiSolver s, ObiNativeContactList contacts)
    {
        // 如果没有在Inspector面板中设置触发器或场景名，则不执行任何操作
        if (triggerCollider == null || string.IsNullOrEmpty(targetSceneName)) return;

        var world = ObiColliderWorld.GetInstance();

        // 遍历所有发生的碰撞
        foreach (var contact in contacts)
        {
            // 确保是一个有效的穿透碰撞
            if (!(contact.distance > 0.01f)) continue;

            // 获取与玩家碰撞的那个碰撞体 (contact.bodyB)
            var hitCollider = world.colliderHandles[contact.bodyB].owner;

            // 检查这个碰撞体是否是我们指定的那个触发器
            if (hitCollider == triggerCollider)
            {
                Debug.Log($"检测到与目标碰撞体 '{triggerCollider.name}' 的碰撞，正在加载关卡: {targetSceneName}");
                
                // 加载目标关卡
                SceneManager.LoadScene(targetSceneName);
                
                // 找到后即可退出循环，无需再检查其他碰撞
                return; 
            }
        }
    }
}