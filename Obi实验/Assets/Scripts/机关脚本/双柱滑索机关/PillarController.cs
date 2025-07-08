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
    // [新增] 抓取逻辑的冷却时间
    [Tooltip("释放玩家后，抓取功能失效的冷却时间（秒）")]
    [SerializeField] private float attachmentGracePeriodDuration = 1.5f;


    [Header("Obi 交互设置")]
    [SerializeField] private ObiSolver obiSolver;
    [SerializeField] private ObiActor playerActor;
    [SerializeField] private ObiCollider pillarAObiCollider;
    [SerializeField] private ObiCollider pillarBObiCollider;
    [SerializeField] private ObiParticleAttachment playerAttachment;

    // --- 公共状态属性 ---
    public bool IsCartInGracePeriod { get; private set; } = false;
    // [新增] 抓取冷却期状态
    public bool IsAttachmentInGracePeriod { get; private set; } = false;


    private bool isMoving = false;
    private Transform lockedPillarParent = null; 
    private bool isPillarAHigh = true;
    private CancellationTokenSource cts;

    #region Unity生命周期
    void Start()
    {
        // ... Start方法无需修改 ...
        if (playerAttachment == null) { Debug.LogError($"[{name}] 未指定玩家的ObiParticleAttachment脚本引用！", this); enabled = false; return; }
        cts = new CancellationTokenSource();
    }
    
    void OnEnable() { if (obiSolver != null) obiSolver.OnCollision += OnObiPlayerCollisionWithPillar; }
    void OnDisable() { if (obiSolver != null) obiSolver.OnCollision -= OnObiPlayerCollisionWithPillar; }
    void OnDestroy() { if (cts != null) { cts.Cancel(); cts.Dispose(); } }
    #endregion

    #region 核心逻辑
    /// <summary>
    /// [已修改] 硬锁定方法现在会启动【抓取冷却期】
    /// </summary>
    public void LockCartToPillarParent(Transform pillarParentTransform)
    {
        if (cartRigidbody != null && cartRigidbody.transform.parent != pillarParentTransform)
        {
            cartRigidbody.isKinematic = true; 
            cartRigidbody.transform.SetParent(pillarParentTransform, worldPositionStays: true);
            lockedPillarParent = pillarParentTransform;
            Debug.Log($"运输车已硬锁定到父对象: {pillarParentTransform.name}。");

            if (playerAttachment.enabled)
            {
                Debug.Log("小车已固定，开始释放玩家...");
                playerAttachment.enabled = false;
                playerAttachment.target = null;
                
                // [新增] 释放玩家后，立刻启动抓取冷却计时
                StartAttachmentGracePeriod().Forget();
            }
        }
    }
    
    /// <summary>
    /// [新增] 启动抓取冷却期的异步方法
    /// </summary>
    private async UniTask StartAttachmentGracePeriod()
    {
        IsAttachmentInGracePeriod = true;
        Debug.Log($"抓取功能进入 {attachmentGracePeriodDuration} 秒冷却。");
        await UniTask.Delay(System.TimeSpan.FromSeconds(attachmentGracePeriodDuration), cancellationToken: cts.Token);
        IsAttachmentInGracePeriod = false;
        Debug.Log("抓取功能冷却结束。");
    }

    // ... RequestPillarSwap, InitiatePillarSwap, UnlockCartAsync 等方法无需修改 ...
    public void RequestPillarSwap(ObiCollider triggeredByPillar = null) { if (isMoving) return; if (lockedPillarParent != null && triggeredByPillar != null) { if ((triggeredByPillar == pillarAObiCollider && lockedPillarParent == pillarParentA) || (triggeredByPillar == pillarBObiCollider && lockedPillarParent == pillarParentB)) { return; } } InitiatePillarSwap().Forget(); }
    private async UniTask InitiatePillarSwap() { isMoving = true; Vector3 sA = pillarParentA.localPosition; Vector3 sB = pillarParentB.localPosition; Vector3 tA, tB; if (isPillarAHigh) { tA = sA - new Vector3(0, moveDistance, 0); tB = sB + new Vector3(0, moveDistance, 0); } else { tA = sA + new Vector3(0, moveDistance, 0); tB = sB - new Vector3(0, moveDistance, 0); } float e = 0f; while (e < moveDuration) { if (cts.IsCancellationRequested) { isMoving = false; return; } e += Time.deltaTime; float t = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp01(e / moveDuration)); pillarParentA.localPosition = Vector3.Lerp(sA, tA, t); pillarParentB.localPosition = Vector3.Lerp(sB, tB, t); await UniTask.Yield(PlayerLoopTiming.Update, cts.Token); } pillarParentA.localPosition = tA; pillarParentB.localPosition = tB; isPillarAHigh = !isPillarAHigh; isMoving = false; await UnlockCartAsync(); }
    private async UniTask UnlockCartAsync() { if (cartRigidbody != null && cartRigidbody.transform.parent != null) { lockedPillarParent = null; cartRigidbody.transform.SetParent(null, worldPositionStays: true); IsCartInGracePeriod = true; cartRigidbody.isKinematic = false; await UniTask.Delay(System.TimeSpan.FromSeconds(gracePeriodDuration), cancellationToken: cts.Token); IsCartInGracePeriod = false; } }
    #endregion
    
    #region 碰撞与触发 (这部分无需修改)
    private void OnObiPlayerCollisionWithPillar(ObiSolver solver, ObiNativeContactList contacts) { for (int i = 0; i < contacts.count; ++i) { Oni.Contact contact = contacts[i]; ObiCollider cP = GetCollidedPillar(contact.bodyA) ?? GetCollidedPillar(contact.bodyB); ObiActor cA = GetPlayerActor(contact.bodyA) ?? GetPlayerActor(contact.bodyB); if (cP != null && cA != null) { RequestPillarSwap(cP); return; } } }
    private ObiCollider GetCollidedPillar(int c) { var w=ObiColliderWorld.GetInstance(); if(c<0||c>=w.colliderHandles.Count)return null;var o=w.colliderHandles[c].owner;return(o==pillarAObiCollider||o==pillarBObiCollider)?(ObiCollider)o:null; }
    private ObiActor GetPlayerActor(int p) { if(p<0||p>=obiSolver.particleToActor.Length)return null;var i=obiSolver.particleToActor[p];return(i!=null&&i.actor==playerActor)?playerActor:null; }
    #endregion
}