using UnityEngine;

// 使用此属性可确保该脚本所附加的游戏对象上必须有一个Rigidbody组件。
// 如果没有，Unity会自动为您添加一个。
[RequireComponent(typeof(Rigidbody))]
public class SetMassAndGravity : MonoBehaviour
{
    // 用于存储对Rigidbody组件的引用
    private Rigidbody rb;

    // Awake在脚本实例被加载时调用
    void Awake()
    {
        // 获取附加在同一个游戏对象上的Rigidbody组件
        rb = GetComponent<Rigidbody>();
        
        // 初始提示信息
        Debug.Log("质量设置脚本已准备就绪。请按数字键 1、2、3、4 或 5 来设置质量。");
    }

    // Update每帧调用一次
    void Update()
    {
        // 检测按键输入
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            ApplySettings(100f);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            ApplySettings(500f);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            ApplySettings(1000f);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            ApplySettings(5000f);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            ApplySettings(10000f);
        }
    }

    /// <summary>
    /// 应用质量和重力设置的辅助方法
    /// </summary>
    /// <param name="newMass">要设置的新质量</param>
    private void ApplySettings(float newMass)
    {
        // 检查引用是否有效
        if (rb != null)
        {
            // 设置刚体质量
            rb.mass = newMass;

            // 确保启用了重力
            rb.useGravity = true;

            // 在控制台打印一条消息，方便调试和确认
            Debug.Log($"已设置对象 '{gameObject.name}' 的质量为: {rb.mass}, 并已启用重力。");
        }
    }
}