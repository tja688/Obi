using UnityEngine;

/// <summary>
/// 动态推力脚本。
/// 当推盘对象进入触发器时，对自身刚体施加一个力。
/// 力的大小与推盘和基座的距离成反比（距离越近，力越大）。
/// </summary>
[RequireComponent(typeof(Rigidbody))] // 确保对象上一定有Rigidbody组件
[RequireComponent(typeof(Collider))]  // 确保对象上一定有Collider组件
public class DynamicThruster : MonoBehaviour
{
    [Header("关联对象")]
    [Tooltip("指定外部的推盘对象，它的进入和退出将控制推力逻辑")]
    public Transform pusherObject;

    [Tooltip("指定外部的基座对象，用于计算距离")]
    public Transform baseObject;

    [Header("推力设置")]
    [Tooltip("施加推力的世界坐标方向")]
    public Vector3 thrustDirection = Vector3.forward;

    [Tooltip("基础推力，即推盘和基座处于初始距离时的推力大小")]
    public float baseThrust = 10f;

    [Tooltip("推力增长率。例如：值为2，代表距离为0时，最大推力是基础推力的3倍 (1 + 2)")]
    [Min(0)]
    public float thrustGrowthRate = 2f;

    // --- 私有变量 ---
    private Rigidbody targetRigidbody;        // 缓存自身（被推对象）的刚体
    private float initialSystemDistance;      // 预存的推盘与基座的初始距离
    private bool isThrustActive = false;      // 推力逻辑是否被激活

    /// <summary>
    /// Awake在对象加载时调用，用于初始化
    /// </summary>
    private void Awake()
    {
        // 获取并缓存刚体组件
        targetRigidbody = GetComponent<Rigidbody>();

        // --- 安全性检查 ---
        if (pusherObject == null || baseObject == null)
        {
            Debug.LogError("错误：推盘对象 (Pusher Object) 或 基座对象 (Base Object) 未在Inspector中设置！", this);
            this.enabled = false; // 禁用此脚本以防后续出错
            return;
        }

        // 确保挂载此脚本的对象上的触发器被正确设置
        var myCollider = GetComponent<Collider>();
        if (!myCollider.isTrigger)
        {
            Debug.LogWarning("警告：为了让推力脚本正常工作，请将挂载此脚本对象上的Collider组件的 'Is Trigger' 属性勾选上。", this);
        }

        // 预计算并存储推盘和基座的初始（静态系统）距离
        initialSystemDistance = Vector3.Distance(pusherObject.position, baseObject.position);

        // 防止初始距离为0导致后续计算出现除零错误
        if (initialSystemDistance <= 0)
        {
            Debug.LogError("错误：推盘与基座的初始距离为0，无法计算推力增长。请确保它们在启动时有一定间距。", this);
            this.enabled = false;
            return;
        }
    }

    /// <summary>
    /// FixedUpdate是处理物理逻辑的理想位置，它以固定的时间间隔被调用
    /// </summary>
    private void FixedUpdate()
    {
        // 如果推力逻辑未激活，则不执行任何操作
        if (!isThrustActive)
        {
            return;
        }

        // 1. 计算当前推盘与基座的距离
        float currentDistance = Vector3.Distance(pusherObject.position, baseObject.position);

        // 2. 计算距离压缩率（0到1之间）
        // 当 currentDistance 等于 initialSystemDistance 时，压缩率为 0
        // 当 currentDistance 等于 0 时，压缩率为 1
        float compressionRatio = 1f - (currentDistance / initialSystemDistance);
        compressionRatio = Mathf.Clamp01(compressionRatio); // 将值限制在0-1范围内，防止超出

        // 3. 根据压缩率和增长率计算总推力
        // 公式：总推力 = 基础推力 * (1 + 增长率 * 压缩率)
        float totalThrust = baseThrust * (1 + thrustGrowthRate * compressionRatio);

        // 4. 对刚体施加力
        // 方向需要归一化，以确保推力大小仅由 totalThrust 控制
        targetRigidbody.AddForce(thrustDirection.normalized * totalThrust, ForceMode.Force);
    }

    #region 触发器监听
    /// <summary>
    /// 当其他Collider进入此对象的触发器时调用
    /// </summary>
    /// <param name="other">进入触发器的另一个Collider</param>
    private void OnTriggerEnter(Collider other)
    {
        // 检查进入的是否是我们指定的推盘对象
        if (other.transform == pusherObject)
        {
            isThrustActive = true;
            Debug.Log("推盘已进入，推力系统激活。");
        }
    }

    /// <summary>
    /// 当其他Collider退出此对象的触发器时调用
    /// </summary>
    /// <param name="other">退出触发器的另一个Collider</param>
    private void OnTriggerExit(Collider other)
    {
        // 检查退出的是否是我们指定的推盘对象
        if (other.transform == pusherObject)
        {
            isThrustActive = false;
            Debug.Log("推盘已退出，推力系统失效。");
        }
    }
    #endregion
}