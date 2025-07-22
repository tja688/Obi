using UnityEngine;

// V1.0 - 动态鳞片系统的物理代理
public class ScaleProxy : MonoBehaviour
{
    public DynamicScaleController OwnerController { get; private set; }
    public ScaleGroup ParentGroup { get; private set; }
    public int AttachedParticleSolverIndex { get; private set; }
    public Rigidbody Rigidbody { get; private set; }

    // 用于实现“惰性消失”的计时器
    public float timeSinceLastContact = 0f;

    void Awake()
    {
        Rigidbody = GetComponent<Rigidbody>();
        if (Rigidbody == null)
            Debug.LogError("ScaleProxy 预制体上缺少 Rigidbody 组件!", this);
    }

    /// <summary>
    /// 当从对象池取出并激活时调用
    /// </summary>
    public void Initialize(DynamicScaleController owner, ScaleGroup group, int particleSolverIndex, Vector3 position, Quaternion rotation)
    {
        this.OwnerController = owner;
        this.ParentGroup = group;
        this.AttachedParticleSolverIndex = particleSolverIndex;

        transform.position = position;
        transform.rotation = rotation;
        gameObject.SetActive(true);
        timeSinceLastContact = 0f;
    }

    /// <summary>
    /// 当被外部碰撞体接触时，由 Controller 调用来重置计时器
    /// </summary>
    public void NotifyContact()
    {
        timeSinceLastContact = 0f;
    }

    /// <summary>
    /// 回收到对象池之前调用
    /// </summary>
    public void Deactivate()
    {
        gameObject.SetActive(false);
        ParentGroup = null;
        OwnerController = null;
        AttachedParticleSolverIndex = -1;
    }
    
    /// <summary>
    /// [V1.3] 在约束创建时补全所属组的信息
    /// </summary>
    public void SetGroup(ScaleGroup group)
    {
        this.ParentGroup = group;
    }
}