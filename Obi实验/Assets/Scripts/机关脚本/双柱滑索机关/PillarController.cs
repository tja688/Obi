using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using Obi;

/// <summary>
/// 核心控制器（最终版）：
/// 新增职责：在锁定小车时，负责【释放】被抓住的玩家。
/// </summary>
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

    [Header("Obi 交互设置")]
    [SerializeField] private ObiSolver obiSolver;
    [SerializeField] private ObiActor playerActor;
    [SerializeField] private ObiCollider pillarAObiCollider;
    [SerializeField] private ObiCollider pillarBObiCollider;
    // [新增] 对玩家身上Attachment脚本的引用
    [Tooltip("玩家对象身上挂载的ObiParticleAttachment脚本")]
    [SerializeField] private ObiParticleAttachment playerAttachment;

    public bool IsCartInGracePeriod { get; private set; } = false;

    private bool isMoving = false;
    private Transform lockedPillarParent = null; 
    private bool isPillarAHigh = true;
    private CancellationTokenSource cts;

    #region Unity生命周期
    void Start()
    {
        if (playerAttachment == null)
        {
             Debug.LogError($"[{name}] 未指定玩家的ObiParticleAttachment脚本引用！", this);
             enabled = false;
             return;
        }
        // ... 其他引用检查 ...
        cts = new CancellationTokenSource();
    }
    
    // ... OnEnable/Disable/Destroy 保持不变 ...
    void OnEnable() { if (obiSolver != null) obiSolver.OnCollision += OnObiPlayerCollisionWithPillar; }
    void OnDisable() { if (obiSolver != null) obiSolver.OnCollision -= OnObiPlayerCollisionWithPillar; }
    void OnDestroy() { if (cts != null) { cts.Cancel(); cts.Dispose(); } }
    #endregion

    #region 核心逻辑
    /// <summary>
    /// [已修改] 硬锁定方法现在包含【释放玩家】的逻辑
    /// </summary>
    public void LockCartToPillarParent(Transform pillarParentTransform)
    {
        if (cartRigidbody != null && cartRigidbody.transform.parent != pillarParentTransform)
        {
            cartRigidbody.isKinematic = true; 
            cartRigidbody.transform.SetParent(pillarParentTransform, worldPositionStays: true);
            lockedPillarParent = pillarParentTransform;
            Debug.Log($"运输车已硬锁定到父对象: {pillarParentTransform.name}。");

            // --- [新增] 释放玩家的核心逻辑 ---
            // 检查Attachment是否正处于激活（抓取）状态
            if (playerAttachment.enabled)
            {
                Debug.Log("小车已固定，开始释放玩家...");
                // 步骤4a: 先失活ObiParticleAttachment
                playerAttachment.enabled = false;
                // 步骤4b: 再设置target为null，以触发内部清理
                playerAttachment.target = null;
            }
        }
    }
    
    public void RequestPillarSwap(ObiCollider triggeredByPillar = null)
    {
        if (isMoving) return;
        if (lockedPillarParent != null && triggeredByPillar != null)
        {
            if ((triggeredByPillar == pillarAObiCollider && lockedPillarParent == pillarParentA) ||
                (triggeredByPillar == pillarBObiCollider && lockedPillarParent == pillarParentB))
            {
                return;
            }
        }
        InitiatePillarSwap().Forget();
    }

    private async UniTask InitiatePillarSwap()
    {
        // ... 此方法内部的移动逻辑完全不变 ...
        isMoving = true;
        Vector3 startPosA = pillarParentA.localPosition;
        Vector3 startPosB = pillarParentB.localPosition;
        Vector3 targetPosA, targetPosB;
        if (isPillarAHigh) {
            targetPosA = startPosA - new Vector3(0, moveDistance, 0);
            targetPosB = startPosB + new Vector3(0, moveDistance, 0);
        } else {
            targetPosA = startPosA + new Vector3(0, moveDistance, 0);
            targetPosB = startPosB - new Vector3(0, moveDistance, 0);
        }
        float elapsedTime = 0f;
        while (elapsedTime < moveDuration) {
            if (cts.IsCancellationRequested) { isMoving = false; return; }
            elapsedTime += Time.deltaTime;
            float t = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp01(elapsedTime / moveDuration));
            pillarParentA.localPosition = Vector3.Lerp(startPosA, targetPosA, t);
            pillarParentB.localPosition = Vector3.Lerp(startPosB, targetPosB, t);
            await UniTask.Yield(PlayerLoopTiming.Update, cts.Token);
        }
        pillarParentA.localPosition = targetPosA;
        pillarParentB.localPosition = targetPosB;
        isPillarAHigh = !isPillarAHigh;
        isMoving = false;
        await UnlockCartAsync();
    }

    private async UniTask UnlockCartAsync()
    {
        if (cartRigidbody != null && cartRigidbody.transform.parent != null)
        {
            lockedPillarParent = null;
            cartRigidbody.transform.SetParent(null, worldPositionStays: true);
            IsCartInGracePeriod = true;
            cartRigidbody.isKinematic = false;
            await UniTask.Delay(System.TimeSpan.FromSeconds(gracePeriodDuration), cancellationToken: cts.Token);
            IsCartInGracePeriod = false;
        }
    }
    #endregion
    
    #region 碰撞与触发 (这部分无需修改)
    private void OnObiPlayerCollisionWithPillar(ObiSolver solver, ObiNativeContactList contacts)
    {
        for (int i = 0; i < contacts.count; ++i) {
            Oni.Contact contact = contacts[i];
            ObiCollider collidedPillar = GetCollidedPillar(contact.bodyA) ?? GetCollidedPillar(contact.bodyB);
            ObiActor collidedActor = GetPlayerActor(contact.bodyA) ?? GetPlayerActor(contact.bodyB);
            if (collidedPillar != null && collidedActor != null) {
                RequestPillarSwap(collidedPillar);
                return; 
            }
        }
    }
    private ObiCollider GetCollidedPillar(int c) { var w=ObiColliderWorld.GetInstance(); if(c<0||c>=w.colliderHandles.Count)return null;var o=w.colliderHandles[c].owner;return(o==pillarAObiCollider||o==pillarBObiCollider)?(ObiCollider)o:null; }
    private ObiActor GetPlayerActor(int p) { if(p<0||p>=obiSolver.particleToActor.Length)return null;var i=obiSolver.particleToActor[p];return(i!=null&&i.actor==playerActor)?playerActor:null; }
    #endregion
}