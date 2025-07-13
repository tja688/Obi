// TwinPillarRingMechanism.cs
using UnityEngine;
using System.Collections;
using Cysharp.Threading.Tasks;

/// <summary>
/// 双柱铁环机关的具体实现。
/// </summary>
public class TwinPillarRingMechanism : MechanismBase
{
    [Header("机关专属设置")]
    [Tooltip("左边的铁环控制器。")]
    public RingController ringA;
    [Tooltip("右边的铁环控制器。")]
    public RingController ringB;
    [Tooltip("拖动铁环时的灵敏度。")]
    public float dragSensitivity = 0.5f;

    // 内部变量
    private RingController controlledRing;
    private float controlledRingStartNormPos;
    private Camera mainCamera;

    protected override void Start()
    {
        base.Start(); // 执行基类的Start逻辑
        mainCamera = Camera.main;
        if (ringA == null || ringB == null)
        {
            Debug.LogError("请关联两个铁环控制器！", this);
            enabled = false;
        }
    }

    #region 输入处理 (重写自基类)

    public override void OnLeftButton(bool isPressed)
    {
        if (isPressed)
        {
            // 鼠标按下时，进行射线检测，判断是否点中了某个铁环
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition); // 使用Input.mousePosition获取最新位置
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                if (hit.transform == ringA.transform || hit.transform == ringB.transform)
                {
                    controlledRing = hit.transform.GetComponent<RingController>();
                    controlledRing.SetGrabbedMaterial();
                    controlledRingStartNormPos = controlledRing.GetCurrentNormalizedPosition();
                    Debug.Log($"抓住了 {controlledRing.name}");
                }
            }
        }
        else
        {
            // 鼠标抬起时，释放控制
            if (controlledRing != null)
            {
                controlledRing.SetDefaultMaterial();
                Debug.Log($"松开了 {controlledRing.name}");
                controlledRing = null;
            }
        }
    }

    public override void OnMouseMove(Vector2 position)
    {
        // 只有在抓取了铁环时才处理鼠标移动
        if (controlledRing == null) return;
        
        // 我们只关心鼠标Y轴的变化来控制铁环上下移动
        float deltaY = Input.GetAxis("Mouse Y"); // 使用Input Manager的输入更平滑
        
        // 根据灵敏度计算归一化位置的变化量
        float change = deltaY * dragSensitivity * Time.deltaTime;
        controlledRingStartNormPos = Mathf.Clamp01(controlledRingStartNormPos + change);

        // 控制被抓取的铁环
        controlledRing.SetTargetNormalizedPosition(controlledRingStartNormPos);
        
        // 镜像控制另一个铁环
        RingController otherRing = (controlledRing == ringA) ? ringB : ringA;
        otherRing.SetTargetNormalizedPosition(1f - controlledRingStartNormPos);
    }
    
    public override void OnQuit()
    {
        // 如果玩家在抓取时按下了退出键，也应该释放控制
        if (controlledRing != null)
        {
            controlledRing.SetDefaultMaterial();
            controlledRing = null;
        }
    }

    #endregion

    #region 状态机逻辑 (重写自基类)

    protected override IEnumerator ResetSequence()
    {
        Debug.Log("双柱铁环机关开始复位...");

        // 将UniTask转换为协程，以便基类可以等待它完成
        yield return ResetRingsAsync().ToCoroutine();

        // 协程完成后，调用基类的方法来完成状态转换
        Debug.Log("双柱铁环机关复位完成，切换到待机状态。");
        ChangeState(MechanismState.Idle);
    }
    
    // 这是一个辅助的异步方法，用于处理两个铁环的并行复位
    private async UniTask ResetRingsAsync()
    {
        // 如果在复位时还抓着环，先松开
        if (controlledRing != null)
        {
            controlledRing.SetDefaultMaterial();
            controlledRing = null;
        }

        // 并行启动两个铁环的复位任务
        UniTask taskA = ringA.ResetToInitialPositionAsync();
        UniTask taskB = ringB.ResetToInitialPositionAsync();

        // 等待两个任务都完成
        await UniTask.WhenAll(taskA, taskB);
    }

    #endregion
}