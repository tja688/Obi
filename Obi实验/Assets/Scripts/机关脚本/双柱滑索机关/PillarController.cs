using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using Obi;

public class PillarController : MonoBehaviour
{
    [Header("核心求解器与目标Actor")]
    [Tooltip("场景中驱动物理的核心Obi Solver")]
    [SerializeField] private ObiSolver targetSolver; // [新增]
    [Tooltip("代表玩家的Obi Actor")]
    [SerializeField] private ObiActor playerActor;   // [新增]

    [Header("场景对象引用")]
    [SerializeField] private Transform pillarParentA;
    [SerializeField] private Transform pillarParentB;
    [SerializeField] private Rigidbody cartRigidbody;

    [Header("运动参数")]
    [SerializeField] private float moveDuration = 2.0f;
    [SerializeField] private float gracePeriodDuration = 0.5f;
    [SerializeField] private float moveDistance = 1.6f;
    [Tooltip("释放玩家后，抓取功能失效的冷却时间（秒）")]
    [SerializeField] private float attachmentGracePeriodDuration = 1.5f;

    [Header("Obi 交互设置")]
    [SerializeField] private ObiCollider pillarAObiCollider;
    [SerializeField] private ObiCollider pillarBObiCollider;

    private ObiParticleAttachment _playerAttachment;

    public bool IsCartInGracePeriod { get; private set; } = false;
    public bool IsAttachmentInGracePeriod { get; private set; } = false;

    private bool _isMoving = false;
    private Transform _lockedPillarParent = null; 
    private bool _isPillarAHigh = true;
    private CancellationTokenSource _cts;
    
    // [删除] 不再需要外部管理器的回调实例
    
    #region Unity生命周期
    void Start()
    {
        _cts = new CancellationTokenSource();

        if (PlayerControl_Ball.instance != null)
        {
            _playerAttachment = PlayerControl_Ball.instance.GetComponent<ObiParticleAttachment>();
        }

        // [改造] 检查并直接订阅目标Solver的事件
        if (targetSolver == null || playerActor == null)
        {
            Debug.LogError("PillarController 初始化失败: 请在Inspector中指定 Target Solver 和 Player Actor！", this);
            enabled = false;
            return;
        }

        targetSolver.OnCollision += HandleObiCollision;
    }
    
    void OnDestroy() 
    { 
        // [改造] 在销毁时取消订阅
        if (targetSolver != null)
        {
            targetSolver.OnCollision -= HandleObiCollision;
        }

        if (_cts != null) 
        { 
            _cts.Cancel(); 
            _cts.Dispose(); 
        } 
    }
    #endregion

    // [新增] 新的碰撞处理函数，专门处理玩家与柱子的碰撞
    private void HandleObiCollision(ObiSolver solver, ObiNativeContactList contacts)
    {
        if (solver != targetSolver) return;

        var world = ObiColliderWorld.GetInstance();

        foreach (Oni.Contact contact in contacts)
        {
            var pair = ObiUtils.GetColliderActorPair(solver, world, contact);
            if (pair == null) continue;

            // 验证：碰撞方必须是玩家Actor，且碰撞体必须是两个柱子之一
            if (pair.Value.actor == playerActor && (pair.Value.collider == pillarAObiCollider || pair.Value.collider == pillarBObiCollider))
            {
                // 条件满足，请求交换
                RequestPillarSwap(pair.Value.collider as ObiCollider);
                break; // 处理一次即可
            }
        }
    }


    #region 核心逻辑 (这部分与您的原始脚本完全一致，未作修改)
    public void LockCartToPillarParent(Transform pillarParentTransform)
    {
        if (cartRigidbody != null && cartRigidbody.transform.parent != pillarParentTransform)
        {
            cartRigidbody.isKinematic = true; 
            cartRigidbody.transform.SetParent(pillarParentTransform, worldPositionStays: true);
            _lockedPillarParent = pillarParentTransform;
            Debug.Log($"运输车已硬锁定到父对象: {pillarParentTransform.name}。");

            if (_playerAttachment != null && _playerAttachment.enabled)
            {
                Debug.Log("小车已固定，开始释放玩家...");
                _playerAttachment.enabled = false;
                _playerAttachment.target = null;
                
                StartAttachmentGracePeriod().Forget();
            }
        }
    }
    
    private async UniTask StartAttachmentGracePeriod()
    {
        IsAttachmentInGracePeriod = true;
        Debug.Log($"抓取功能进入 {attachmentGracePeriodDuration} 秒冷却。");
        await UniTask.Delay(System.TimeSpan.FromSeconds(attachmentGracePeriodDuration), cancellationToken: _cts.Token);
        IsAttachmentInGracePeriod = false;
        Debug.Log("抓取功能冷却结束。");
    }

    public void RequestPillarSwap(ObiCollider triggeredByPillar = null) { if (_isMoving) return; if (_lockedPillarParent != null && triggeredByPillar != null) { if ((triggeredByPillar == pillarAObiCollider && _lockedPillarParent == pillarParentA) || (triggeredByPillar == pillarBObiCollider && _lockedPillarParent == pillarParentB)) { return; } } InitiatePillarSwap().Forget(); }
    private async UniTask InitiatePillarSwap() { _isMoving = true; Vector3 sA = pillarParentA.localPosition; Vector3 sB = pillarParentB.localPosition; Vector3 tA, tB; if (_isPillarAHigh) { tA = sA - new Vector3(0, moveDistance, 0); tB = sB + new Vector3(0, moveDistance, 0); } else { tA = sA + new Vector3(0, moveDistance, 0); tB = sB - new Vector3(0, moveDistance, 0); } float e = 0f; while (e < moveDuration) { if (_cts.IsCancellationRequested) { _isMoving = false; return; } e += Time.deltaTime; float t = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp01(e / moveDuration)); pillarParentA.localPosition = Vector3.Lerp(sA, tA, t); pillarParentB.localPosition = Vector3.Lerp(sB, tB, t); await UniTask.Yield(PlayerLoopTiming.Update, _cts.Token); } pillarParentA.localPosition = tA; pillarParentB.localPosition = tB; _isPillarAHigh = !_isPillarAHigh; _isMoving = false; await UnlockCartAsync(); }
    private async UniTask UnlockCartAsync() { if (cartRigidbody != null && cartRigidbody.transform.parent != null) { _lockedPillarParent = null; cartRigidbody.transform.SetParent(null, worldPositionStays: true); IsCartInGracePeriod = true; cartRigidbody.isKinematic = false; await UniTask.Delay(System.TimeSpan.FromSeconds(gracePeriodDuration), cancellationToken: _cts.Token); IsCartInGracePeriod = false; } }
    #endregion
}