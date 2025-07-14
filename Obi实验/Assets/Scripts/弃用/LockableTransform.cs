using UnityEngine;
using Cysharp.Threading.Tasks; // 引入 UniTask 命名空间
using System.Threading;

/// <summary>
/// 控制对象在锁定和解锁位置之间切换的工具脚本。
/// 解锁位置由移动方向和距离决定。
/// 使用 UniTask 处理移动动画。
/// </summary>
public class LockableTransform : MonoBehaviour
{
    [Header("解锁设置")]
    [Tooltip("设置对象解锁时移动的方向（脚本会自动归一化）")]
    public Vector3 moveDirection = Vector3.up; // 默认为向上

    [Tooltip("设置对象解锁时沿指定方向移动的距离")]
    public float moveDistance = 2f; // 默认为2米

    [Header("动画设置")]
    [Tooltip("对象从一个位置移动到另一个位置所需的时间（秒）")]
    public float moveDuration = 0.5f;

    // 私有变量
    private Transform targetTransform;    // 缓存自身的Transform组件
    private Vector3 lockedPosition;       // 锁定时（初始状态）的位置
    private Vector3 calculatedUnlockedPosition; // 计算出的解锁位置
    private bool isLocked = true;         // 当前是否处于锁定状态，默认为true
    private bool isMoving = false;        // 是否正在移动中，防止重复触发
    private CancellationTokenSource cts;  // 用于取消正在进行的UniTask

    /// <summary>
    /// Awake 在对象加载时调用
    /// </summary>
    private void Awake()
    {
        // 获取并缓存Transform组件
        targetTransform = transform;
        // 将对象的初始位置记录为“锁定位置”
        lockedPosition = targetTransform.position;

        // ★ 新增：提前计算出解锁位置
        // 方向向量归一化，确保移动距离的精确性
        calculatedUnlockedPosition = lockedPosition + (moveDirection.normalized * moveDistance);

        // 初始化CancellationTokenSource
        cts = new CancellationTokenSource();
    }

    /// <summary>
    /// OnDestroy 在对象销毁时调用
    /// </summary>
    private void OnDestroy()
    {
        // 当对象被销毁时，取消所有正在运行的UniTask并释放资源
        cts.Cancel();
        cts.Dispose();
    }

    /// <summary>
    /// Update 每帧调用一次
    /// </summary>
    private void Update()
    {
        // 检测玩家是否按下了空格键
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (isMoving)
            {
                return;
            }
            _ = ToggleLockStateAsync();
        }
    }

    /// <summary>
    /// 切换锁定状态的异步方法
    /// </summary>
    private async UniTask ToggleLockStateAsync()
    {
        isMoving = true;
        isLocked = !isLocked;

        // ★ 修改：根据新的状态从预先计算好的位置中选择目标
        Vector3 targetPosition = isLocked ? lockedPosition : calculatedUnlockedPosition;

        await MoveToPositionAsync(targetPosition, this.cts.Token);

        isMoving = false;
    }

    /// <summary>
    /// 使用UniTask将对象平滑移动到目标位置
    /// </summary>
    /// <param name="targetPosition">目标位置</param>
    /// <param name="cancellationToken">用于中途取消任务的Token</param>
    private async UniTask MoveToPositionAsync(Vector3 targetPosition, CancellationToken cancellationToken)
    {
        Vector3 startPosition = targetTransform.position;
        float elapsedTime = 0f;

        while (elapsedTime < moveDuration)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            float t = elapsedTime / moveDuration;
            targetTransform.position = Vector3.Lerp(startPosition, targetPosition, t);

            elapsedTime += Time.deltaTime;
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        targetTransform.position = targetPosition;
    }
}