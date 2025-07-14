// CounterweightController.cs
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// 控制单个配重块的行为，包括沿路径移动、材质变化和复位。
/// </summary>
public class CounterweightController : MonoBehaviour
{
    [Header("路径设置")]
    [Tooltip("配重块移动路径的起点。")]
    public Transform pathStart;
    [Tooltip("配重块移动路径的终点。")]
    public Transform pathEnd;

    [Header("移动参数")]
    [Tooltip("被拖动时移动到目标位置的平滑速度。")]
    public float moveSpeed = 8f;

    [Header("视觉表现")]
    [Tooltip("当被玩家抓取时替换的材质。")]
    public Material grabbedMaterial;
    
    // 私有变量
    private Material defaultMaterial;
    private new Renderer renderer;
    private float currentNormalizedPos;
    private float targetNormalizedPos;
    private Vector3 initialPosition; // 用于复位的物理位置
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
            Debug.LogError("请为配重块设置路径的起点和终点！", this);
            enabled = false;
            return;
        }
        
        // 记录初始物理位置用于复位，并据此计算初始化的归一化位置
        initialPosition = transform.position;
        float totalDist = Vector3.Distance(pathStart.position, pathEnd.position);
        if (totalDist > 0)
        {
            float distFromStart = Vector3.Distance(transform.position, pathStart.position);
            currentNormalizedPos = targetNormalizedPos = Mathf.Clamp01(distFromStart / totalDist);
        }
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
        // 根据归一化位置更新配重块的物理位置
        transform.position = Vector3.Lerp(pathStart.position, pathEnd.position, currentNormalizedPos);
    }

    // 在编辑器中绘制路径Gizmos，便于观察
    private void OnDrawGizmosSelected()
    {
        if (pathStart != null && pathEnd != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(pathStart.position, pathEnd.position);
            Gizmos.DrawWireSphere(pathStart.position, 0.1f);
            Gizmos.DrawWireSphere(pathEnd.position, 0.1f);
        }
    }
    #endregion

    #region 公共方法
    
    public void SetTargetNormalizedPosition(float normalizedPosition)
    {
        targetNormalizedPos = Mathf.Clamp01(normalizedPosition);
    }

    public float GetCurrentNormalizedPosition()
    {
        return currentNormalizedPos;
    }
    
    public async UniTask ResetToInitialPositionAsync()
    {
        Debug.Log($"开始复位 {gameObject.name} 到初始位置", this);
        
        // 将目标位置设置为初始的归一化位置
        float totalDist = Vector3.Distance(pathStart.position, pathEnd.position);
        float initialNormPos = 0;
        if(totalDist > 0)
        {
            float distFromStart = Vector3.Distance(initialPosition, pathStart.position);
            initialNormPos = Mathf.Clamp01(distFromStart / totalDist);
        }
        targetNormalizedPos = initialNormPos;
        
        // 使用UniTask.WaitUntil等待配重块位置足够接近初始物理位置
        await UniTask.WaitUntil(() =>
        {
            return Vector3.Distance(transform.position, initialPosition) < 0.3f;
        }, cancellationToken: cts.Token);

        // 确保最终位置精确
        transform.position = initialPosition;
        currentNormalizedPos = targetNormalizedPos;
        Debug.Log($"{gameObject.name} 复位完成。", this);
    }

    public void SetGrabbedMaterial()
    {
        if (renderer != null && grabbedMaterial != null)
        {
            renderer.material = grabbedMaterial;
        }
    }

    public void SetDefaultMaterial()
    {
        if (renderer != null && defaultMaterial != null)
        {
            renderer.material = defaultMaterial;
        }
    }

    #endregion
}