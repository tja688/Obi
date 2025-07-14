// PulleyRopeMechanism.cs
using UnityEngine;
using System.Collections;
using Cysharp.Threading.Tasks;

/// <summary>
/// 滑轮绳索机关的总控制器。
/// 负责处理玩家输入、抓取/释放配重块，以及触发所有关联组件的复位。
/// </summary>
public class PulleyRopeMechanism : MechanismBase
{
    [Header("机关专属设置")]
    [Tooltip("左边的配重块控制器。")]
    public CounterweightController weightA;
    [Tooltip("右边的配重块控制器。")]
    public CounterweightController weightB;
    [Tooltip("拖动配重块时的灵敏度。")]
    public float dragSensitivity = 0.5f;

    [Header("射线检测设置")]
    [Tooltip("指定哪些图层上的物体是可以被射线抓取的（通常是配重块所在的层）。")]
    public LayerMask grabbableLayerMask;

    // 内部变量
    private CounterweightController controlledWeight;
    private float controlledWeightStartNormPos;
    private Camera mainCamera;

    protected override void Start()
    {
        base.Start(); // 执行基类的Start逻辑
        mainCamera = Camera.main;
        if (weightA == null || weightB == null)
        {
            Debug.LogError("请关联两个配重块控制器！", this);
            enabled = false;
        }
    }
    
    public override void OnLeftButton(bool isPressed)
    {
        if (isPressed)
        {
            // 鼠标按下时，进行射线检测
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, grabbableLayerMask))
            {
                // 检查射中的物体是否带有 CounterweightController 脚本
                if (hit.transform.TryGetComponent(out CounterweightController weight))
                {
                    controlledWeight = weight;
                    controlledWeight.SetGrabbedMaterial();
                    controlledWeightStartNormPos = controlledWeight.GetCurrentNormalizedPosition();
                    Debug.Log($"抓住了配重块: {controlledWeight.name}");
                }
            }
        }
        else
        {
            // 鼠标抬起时，释放控制
            if (controlledWeight != null)
            {
                controlledWeight.SetDefaultMaterial();
                Debug.Log($"松开了配重块: {controlledWeight.name}");
                controlledWeight = null;
            }
        }
    }

    public override void OnMouseMove(Vector2 position)
    {
        // 只有在抓取了配重块时才处理鼠标移动
        if (controlledWeight == null) return;
        
        // 使用鼠标Y轴的变化来控制配重块上下移动
        float deltaY = Input.GetAxis("Mouse Y");
        
        // 根据灵敏度计算归一化位置的变化量
        float change = deltaY * dragSensitivity * Time.deltaTime;
        controlledWeightStartNormPos = Mathf.Clamp01(controlledWeightStartNormPos + change);

        // 【重要】只控制当前被抓取的配重块，不再有镜像控制
        controlledWeight.SetTargetNormalizedPosition(controlledWeightStartNormPos);
    }
    
    public override void OnQuit()
    {
        if (controlledWeight != null)
        {
            controlledWeight.SetDefaultMaterial();
            controlledWeight = null;
        }
        base.OnQuit();
    }

    #region 状态机逻辑 (重写自基类)

    protected override IEnumerator ResetSequence()
    {
        Debug.Log("滑轮绳索机关开始复位...");
        yield return ResetWeightsAsync().ToCoroutine(); // 等待异步复位完成
        Debug.Log("滑轮绳索机关复位完成，切换到待机状态。");
        ChangeState(MechanismState.Standby);
        
    }
    
    private async UniTask ResetWeightsAsync()
    {
        if (controlledWeight != null)
        {
            controlledWeight.SetDefaultMaterial();
            controlledWeight = null;
        }

        // 并行启动两个配重块的复位任务
        UniTask taskA = weightA.ResetToInitialPositionAsync();
        UniTask taskB = weightB.ResetToInitialPositionAsync();

        // 等待两个任务都完成
        await UniTask.WhenAll(taskA, taskB);
    }

    #endregion
}