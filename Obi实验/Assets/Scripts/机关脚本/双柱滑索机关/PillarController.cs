using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using Obi;
using System; // 需要引入System命名空间来使用Action

public class PillarController : MonoBehaviour
{
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
    // **修改点**: 不再需要引用任何Solver，只需引用自身的碰撞体
    [SerializeField] private ObiCollider pillarAObiCollider;
    [SerializeField] private ObiCollider pillarBObiCollider;

    private ObiParticleAttachment _playerAttachment;

    // --- 公共状态属性 ---
    public bool IsCartInGracePeriod { get; private set; } = false;
    public bool IsAttachmentInGracePeriod { get; private set; } = false;

    // --- 私有状态 ---
    private bool _isMoving = false;
    private Transform _lockedPillarParent = null; 
    private bool _isPillarAHigh = true;
    private CancellationTokenSource _cts;
    
    // **修改点**: 为每个需要注册的回调创建一个Action实例，以便正确注销
    private Action<Oni.Contact> _onCollisionWithPillarA;
    private Action<Oni.Contact> _onCollisionWithPillarB;


    #region Unity生命周期
    void Start()
    {
        _cts = new CancellationTokenSource();

        // 初始化玩家附件的引用
        if (PlayerControl_Ball.instance != null)
        {
            _playerAttachment = PlayerControl_Ball.instance.GetComponent<ObiParticleAttachment>();
        }

        // 检查管理器是否存在
        if (ObiCollisionManager.Instance == null)
        {
            Debug.LogError("PillarController 初始化失败: 场景中缺少 ObiCollisionManager 实例！", this);
            return;
        }

        // **修改点**: 向管理器注册碰撞回调
        // 1. 为柱子A注册
        if (pillarAObiCollider != null)
        {
            _onCollisionWithPillarA = (contact) => { RequestPillarSwap(pillarAObiCollider); };
            ObiCollisionManager.Instance.RegisterCollisionCallback(pillarAObiCollider, _onCollisionWithPillarA);
        }
        // 2. 为柱子B注册
        if (pillarBObiCollider != null)
        {
            _onCollisionWithPillarB = (contact) => { RequestPillarSwap(pillarBObiCollider); };
            ObiCollisionManager.Instance.RegisterCollisionCallback(pillarBObiCollider, _onCollisionWithPillarB);
        }
    }
    
    void OnDestroy() 
    { 
        // **修改点**: 在销毁时，向管理器取消注册，释放资源
        if (ObiCollisionManager.Instance != null)
        {
            if (pillarAObiCollider != null)
            {
                ObiCollisionManager.Instance.UnregisterCollisionCallback(pillarAObiCollider, _onCollisionWithPillarA);
            }
            if (pillarBObiCollider != null)
            {
                ObiCollisionManager.Instance.UnregisterCollisionCallback(pillarBObiCollider, _onCollisionWithPillarB);
            }
        }

        if (_cts != null) 
        { 
            _cts.Cancel(); 
            _cts.Dispose(); 
        } 
    }
    #endregion

    #region 核心逻辑 (这部分无需修改)
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