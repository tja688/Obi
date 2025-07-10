using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// 一个可配置的移动平台控制器，使用 UniTask 进行异步处理。
/// </summary>
public class PlatformMover : MonoBehaviour
{
    [Header("移动设置")]
    [Tooltip("平台移动的速度")]
    [SerializeField] private float moveSpeed = 3.0f;

    [Tooltip("平台的起点（可选，不填则使用初始位置）")]
    [SerializeField] private Transform startPoint;

    [Tooltip("平台的终点（必需）")]
    [SerializeField] private Transform endPoint;

    [Header("自动运行设置")]
    [Tooltip("勾选后，平台将在游戏开始时自动启动")]
    [SerializeField] private bool startOnAwake = true;

    [Tooltip("是否在起点和终点之间来回移动")]
    [SerializeField] private bool isLooping = true;

    [Tooltip("到达终点后的等待时间（秒）")]
    [SerializeField] private float waitAtEndPoint = 2.0f;

    [Tooltip("回到起点后的等待时间（秒）")]
    [SerializeField] private float waitAtStartPoint = 2.0f;

    private Vector3 _actualStartPos;
    private Vector3 _actualEndPos;
    private bool _isMoving = false;
    private CancellationTokenSource _cancellationTokenSource;

    private void Start()
    {
        // 初始化起点和终点位置
        // 如果 startPoint 未指定，则使用物体自身的初始位置作为起点
        _actualStartPos = startPoint == null ? transform.position : startPoint.position;
        
        if (endPoint == null)
        {
            Debug.LogError("错误：必须为平台指定一个终点！", this);
            return;
        }
        _actualEndPos = endPoint.position;

        // 将平台初始位置设置为起点
        transform.position = _actualStartPos;

        // 根据配置决定是否在游戏开始时自动启动
        if (startOnAwake)
        {
            StartMoving();
        }
    }

    /// <summary>
    /// 公开方法：启动平台移动。
    /// </summary>
    public void StartMoving()
    {
        if (_isMoving) return;
        _isMoving = true;
        
        // 创建一个新的 CancellationTokenSource 用于控制任务的取消
        _cancellationTokenSource = new CancellationTokenSource();
        
        // 启动移动循环的 UniTask
        MoveLoopAsync(_cancellationTokenSource.Token).Forget();
        Debug.Log("平台已启动。");
    }

    /// <summary>
    /// 公开方法：停止平台移动。
    /// </summary>
    public void StopMoving()
    {
        if (!_isMoving) return;
        
        // 取消正在进行的 UniTask
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;

        _isMoving = false;
        Debug.Log("平台已停止。");
    }

    /// <summary>
    /// 核心移动逻辑的异步循环。
    /// </summary>
    /// <param name="token">用于取消任务的 CancellationToken</param>
    private async UniTask MoveLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            // 从起点移动到终点
            await MoveToTargetAsync(_actualEndPos, token);
            if (token.IsCancellationRequested) break;

            // 如果不循环，则移动一次后停止
            if (!isLooping)
            {
                StopMoving();
                break;
            }

            // 在终点等待
            await UniTask.Delay(System.TimeSpan.FromSeconds(waitAtEndPoint), cancellationToken: token);
            if (token.IsCancellationRequested) break;

            // 从终点移动回起点
            await MoveToTargetAsync(_actualStartPos, token);
            if (token.IsCancellationRequested) break;
            
            // 在起点等待
            await UniTask.Delay(System.TimeSpan.FromSeconds(waitAtStartPoint), cancellationToken: token);
        }
    }

    /// <summary>
    /// 将平台移动到指定目标的异步方法。
    /// </summary>
    /// <param name="targetPosition">目标位置</param>
    /// <param name="token">用于取消任务的 CancellationToken</param>
    private async UniTask MoveToTargetAsync(Vector3 targetPosition, CancellationToken token)
    {
        while (Vector3.Distance(transform.position, targetPosition) > 0.01f)
        {
            // 检查任务是否已被取消
            if (token.IsCancellationRequested)
            {
                Debug.Log("移动任务被取消。");
                return;
            }

            // 使用 MoveTowards 平滑地移动平台
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
            
            // 等待下一帧
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        // 确保平台精确到达目标位置
        transform.position = targetPosition;
        Debug.Log($"平台已到达: {targetPosition}");
    }

    // 当脚本被销毁时，确保取消正在运行的任务
    private void OnDestroy()
    {
        StopMoving();
    }

    // 在编辑器中绘制辅助线，方便观察起点和终点
    private void OnDrawGizmos()
    {
        // 确保在编辑器模式下也能正确获取位置
        Vector3 startPos = startPoint != null ? startPoint.position : (Application.isPlaying ? _actualStartPos : transform.position);
        if (endPoint != null)
        {
            Vector3 endPos = endPoint.position;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(startPos, 0.3f);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(endPos, 0.3f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(startPos, endPos);
        }
    }
}