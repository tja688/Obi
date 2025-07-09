using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using Obi;

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
    [Tooltip("需要监听其碰撞事件的玩家Solver")]
    [SerializeField] private ObiSolver playerSolver; // **修改点1: 明确引用玩家的Solver**
    [SerializeField] private ObiCollider pillarAObiCollider;
    [SerializeField] private ObiCollider pillarBObiCollider;

    private ObiActor _playerActor;
    private ObiParticleAttachment _playerAttachment;

    // --- 公共状态属性 ---
    public bool IsCartInGracePeriod { get; private set; } = false;
    public bool IsAttachmentInGracePeriod { get; private set; } = false;

    private bool _isMoving = false;
    private Transform _lockedPillarParent = null; 
    private bool _isPillarAHigh = true;
    private CancellationTokenSource _cts;

    #region Unity生命周期
    void Start()
    {
        _cts = new CancellationTokenSource();

        if (playerSolver == null)
        {
            Debug.LogError("PillarController: 玩家Solver(playerSolver)未指定！", this);
            return;
        }

        if (PlayerControl_Ball.instance != null)
        {
            _playerActor = PlayerControl_Ball.instance.GetComponent<ObiActor>();
            _playerAttachment = PlayerControl_Ball.instance.GetComponent<ObiParticleAttachment>();
        }
        else
        {
            Debug.LogError("PillarController: 无法找到玩家实例(PlayerControl_Ball.instance)！", this);
        }
    }
    
    // **修改点2: 监听和取消监听playerSolver**
    void OnEnable() { if (playerSolver != null) playerSolver.OnCollision += OnObiPlayerCollisionWithPillar; }
    void OnDisable() { if (playerSolver != null) playerSolver.OnCollision -= OnObiPlayerCollisionWithPillar; }
    void OnDestroy() { if (_cts != null) { _cts.Cancel(); _cts.Dispose(); } }
    #endregion

    #region 核心逻辑
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
    
    #region 碰撞与触发
    // **修改点3: 使用事件传入的solver作为上下文**
    private void OnObiPlayerCollisionWithPillar(ObiSolver solver, ObiNativeContactList contacts) 
    { 
        for (int i = 0; i < contacts.count; ++i) 
        { 
            Oni.Contact contact = contacts[i]; 
            // **修改点4 & 5: 将正确的solver上下文传入辅助函数**
            ObiCollider collidedPillar = GetCollidedPillar(contact.bodyA) ?? GetCollidedPillar(contact.bodyB); 
            ObiActor playerActor = GetPlayerActor(solver, contact.bodyA) ?? GetPlayerActor(solver, contact.bodyB); 
            
            if (collidedPillar != null && playerActor != null) 
            { 
                RequestPillarSwap(collidedPillar); 
                return; 
            } 
        } 
    }
    
    private ObiCollider GetCollidedPillar(int colliderIndex) 
    { 
        var world = ObiColliderWorld.GetInstance(); 
        if (colliderIndex < 0 || colliderIndex >= world.colliderHandles.Count) return null;
        var owner = world.colliderHandles[colliderIndex].owner;
        return (owner == pillarAObiCollider || owner == pillarBObiCollider) ? (ObiCollider)owner : null; 
    }

    // **修改点5: 辅助函数接收solver上下文**
    private ObiActor GetPlayerActor(ObiSolver solverContext, int particleIndex) 
    { 
        if (_playerActor == null || particleIndex < 0 || particleIndex >= solverContext.particleToActor.Length) return null;
        var actorHandle = solverContext.particleToActor[particleIndex];
        return (actorHandle != null && actorHandle.actor == _playerActor) ? _playerActor : null; 
    }
    #endregion
}