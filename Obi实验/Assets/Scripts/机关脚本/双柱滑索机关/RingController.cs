// RingController.cs
using UnityEngine;
using Cysharp.Threading.Tasks; // 引入 UniTask
using System.Threading;

/// <summary>
/// 控制单个铁环的行为，包括沿路径移动、材质变化和复位。
/// </summary>
public class RingController : MonoBehaviour
{
    [Header("路径设置")]
    [Tooltip("铁环移动路径的起点。")]
    public Transform pathStart;
    [Tooltip("铁环移动路径的终点。")]
    public Transform pathEnd;

    [Header("移动参数")]
    [Tooltip("移动到目标位置的平滑速度。")]
    public float moveSpeed = 8f;
    [Tooltip("复位时的移动速度。")]
    public float resetSpeed = 10f;

    [Header("视觉表现")]
    [Tooltip("当被玩家抓取时替换的材质。")]
    public Material grabbedMaterial;
    
    // 私有变量
    private Material defaultMaterial;
    private new Renderer renderer; // 使用 new 关键字避免与旧版API冲突
    private float currentNormalizedPos;
    private float targetNormalizedPos;
    private CancellationTokenSource cts;

    #region Unity 生命周期
    private void Awake()
    {
        renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            defaultMaterial = renderer.material;
        }
        cts = new CancellationTokenSource();
    }

    private void Start()
    {
        if (pathStart == null || pathEnd == null)
        {
            Debug.LogError("请为铁环设置路径的起点和终点！", this);
            enabled = false;
            return;
        }
        // 根据初始物理位置计算归一化位置
        float totalDist = Vector3.Distance(pathStart.position, pathEnd.position);
        float distFromStart = Vector3.Distance(transform.position, pathStart.position);
        currentNormalizedPos = targetNormalizedPos = Mathf.Clamp01(distFromStart / totalDist);
    }

    private void OnDestroy()
    {
        // 在对象销毁时取消所有正在进行的UniTask
        cts?.Cancel();
        cts?.Dispose();
    }

    private void Update()
    {
        // 每帧平滑地更新归一化位置
        currentNormalizedPos = Mathf.Lerp(currentNormalizedPos, targetNormalizedPos, moveSpeed * Time.deltaTime);
        // 根据归一化位置更新铁环的物理位置
        transform.position = Vector3.Lerp(pathStart.position, pathEnd.position, currentNormalizedPos);
    }

    // 在编辑器中绘制路径Gizmos，便于观察和调整
    private void OnDrawGizmosSelected()
    {
        if (pathStart != null && pathEnd != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(pathStart.position, pathEnd.position);
            Gizmos.DrawWireSphere(pathStart.position, 0.15f);
            Gizmos.DrawWireSphere(pathEnd.position, 0.15f);
        }
    }
    #endregion

    #region 公共方法
    
    /// <summary>
    /// 设置铁环的目标位置（归一化，0为起点，1为终点）。
    /// </summary>
    public void SetTargetNormalizedPosition(float normalizedPosition)
    {
        targetNormalizedPos = Mathf.Clamp01(normalizedPosition);
    }

    /// <summary>
    /// 获取当前铁环的归一化位置。
    /// </summary>
    public float GetCurrentNormalizedPosition()
    {
        return currentNormalizedPos;
    }

    /// <summary>
    /// 异步方法，使用UniTask将铁环平滑地复位到其初始位置。
    /// </summary>
    public async UniTask ResetToInitialPositionAsync()
    {
        // 重新计算初始位置（以防路径被动态修改）
        float initialPos = targetNormalizedPos; // 假设复位前的最后位置就是初始位置
        if(Application.isPlaying) { // 运行时从start时的位置复位
            float totalDist = Vector3.Distance(pathStart.position, pathEnd.position);
            Vector3 initialPhysicalPos = Vector3.Lerp(pathStart.position, pathEnd.position, initialPos);
            float distFromStart = Vector3.Distance(initialPhysicalPos, pathStart.position);
            initialPos = Mathf.Clamp01(distFromStart / totalDist);
        }
        
        Debug.Log($"开始复位 {gameObject.name} 到 {initialPos}", this);
        
        // 使用UniTask.WaitUntil等待铁环位置接近目标
        await UniTask.WaitUntil(() =>
        {
            targetNormalizedPos = initialPos; // 持续设置目标为初始位置
            // 当物理位置足够接近时，任务完成
            return Vector3.Distance(transform.position, Vector3.Lerp(pathStart.position, pathEnd.position, initialPos)) < 0.01f;
        }, cancellationToken: cts.Token);

        // 确保最终位置精确
        currentNormalizedPos = targetNormalizedPos = initialPos;
        transform.position = Vector3.Lerp(pathStart.position, pathEnd.position, currentNormalizedPos);
        Debug.Log($"{gameObject.name} 复位完成。", this);
    }

    /// <summary>
    /// 切换到“被抓取”材质。
    /// </summary>
    public void SetGrabbedMaterial()
    {
        if (renderer != null && grabbedMaterial != null)
        {
            renderer.material = grabbedMaterial;
        }
    }

    /// <summary>
    /// 恢复为默认材质。
    /// </summary>
    public void SetDefaultMaterial()
    {
        if (renderer != null && defaultMaterial != null)
        {
            renderer.material = defaultMaterial;
        }
    }

    #endregion
}