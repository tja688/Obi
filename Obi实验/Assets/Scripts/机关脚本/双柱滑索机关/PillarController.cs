using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using Obi;

public class PillarController : MonoBehaviour
{
    [Header("核心求解器与目标Actor")]
    [Tooltip("【重要】这里必须引用【玩家Actor】所在的那个Solver")]
    [SerializeField] private ObiSolver playerSolver; 
    [Tooltip("代表玩家的Obi Actor")]
    [SerializeField] private ObiActor playerActor;   

    [Header("场景对象引用")]
    [SerializeField] private Transform pillarParentA;
    [SerializeField] private Transform pillarParentB;
    [SerializeField] private Rigidbody cartRigidbody;

    // [修正] 重新添加了在之前版本中遗漏的运动参数
    [Header("运动参数")]
    [SerializeField] private float moveDuration = 2.0f;
    [SerializeField] private float gracePeriodDuration = 0.5f;
    [SerializeField] private float moveDistance = 1.6f;
    [Tooltip("释放玩家后，抓取功能失效的冷却时间（秒）")]
    [SerializeField] private float attachmentGracePeriodDuration = 1.5f;

    [Header("Obi 交互设置")]
    [SerializeField] private ObiCollider pillarAObiCollider;
    [SerializeField] private ObiCollider pillarBObiCollider;

    [Header("调试设置")]
    [Tooltip("勾选后，将在控制台打印详细的碰撞检测日志")]
    [SerializeField] private bool enableDebugLogging = true;

    private ObiParticleAttachment playerAttachment;
    public bool isCartInGracePeriod { get; private set; } = false;
    public bool isAttachmentInGracePeriod { get; private set; } = false;
    private bool isMoving = false;
    private Transform lockedPillarParent = null; 
    private bool isPillarAHigh = true;
    private CancellationTokenSource cts;
    
    #region Unity生命周期
    void Start()
    {
        cts = new CancellationTokenSource();
        if (PlayerControl_Ball.instance != null) { playerAttachment = PlayerControl_Ball.instance.GetComponent<ObiParticleAttachment>(); }
        if (playerSolver == null || playerActor == null) { Debug.LogError("PillarController 初始化失败: 请在Inspector中指定 Player Solver 和 Player Actor！", this); enabled = false; return; }
        
        playerSolver.OnCollision += HandleObiCollision;
    }
    
    void OnDestroy() 
    { 
        if (playerSolver != null) { playerSolver.OnCollision -= HandleObiCollision; }
        if (cts != null) { cts.Cancel(); cts.Dispose(); } 
    }
    #endregion

    private void HandleObiCollision(ObiSolver solver, ObiNativeContactList contacts)
    {
        if (solver != playerSolver) return;

        var world = ObiColliderWorld.GetInstance();

        for (int i = 0; i < contacts.count; ++i)
        {
            var contact = contacts[i];
            
            if (TryParseCollisionPair(contact, world, out ObiActor hitActor, out ObiColliderBase hitCollider))
            {
                if (enableDebugLogging)
                {
                    Debug.Log($"[PillarController] 解码成功: Actor='{hitActor?.name ?? "NULL"}' <--> Collider='{hitCollider?.name ?? "NULL"}'");
                }

                if (hitActor == playerActor && (hitCollider == pillarAObiCollider || hitCollider == pillarBObiCollider))
                {
                    if (enableDebugLogging)
                    {
                        Debug.Log($"<color=lime>[PillarController] 验证成功! 玩家 '{playerActor.name}' 撞到了柱子 '{hitCollider.name}'。执行逻辑...</color>");
                    }
                    RequestPillarSwap(hitCollider as ObiCollider);
                    return; 
                }
            }
        }
    }

    private bool TryParseCollisionPair(Oni.Contact contact, ObiColliderWorld world, out ObiActor actor, out ObiColliderBase collider)
    {
        actor = null;
        collider = null;

        if (IsParticleFromOurActor(contact.bodyA))
        {
            actor = this.playerActor;
            if (contact.bodyB >= 0 && contact.bodyB < world.colliderHandles.Count)
            {
                collider = world.colliderHandles[contact.bodyB].owner;
            }
        }
        else if (IsParticleFromOurActor(contact.bodyB))
        {
            actor = this.playerActor;
            if (contact.bodyA >= 0 && contact.bodyA < world.colliderHandles.Count)
            {
                collider = world.colliderHandles[contact.bodyA].owner;
            }
        }

        return actor != null && collider != null;
    }

    private bool IsParticleFromOurActor(int particleSolverIndex)
    {
        if (playerSolver == null || !playerSolver.gameObject.activeInHierarchy || particleSolverIndex < 0 || particleSolverIndex >= playerSolver.particleToActor.Length)
            return false;
            
        var p = playerSolver.particleToActor[particleSolverIndex];
        return p != null && p.actor == this.playerActor;
    }

    #region 核心逻辑 (现在可以正确访问成员变量)
    public void LockCartToPillarParent(Transform pillarParentTransform) { if (cartRigidbody != null && cartRigidbody.transform.parent != pillarParentTransform) { cartRigidbody.isKinematic = true; cartRigidbody.transform.SetParent(pillarParentTransform, worldPositionStays: true); lockedPillarParent = pillarParentTransform; Debug.Log($"运输车已硬锁定到父对象: {pillarParentTransform.name}。"); if (playerAttachment != null && playerAttachment.enabled) { Debug.Log("小车已固定，开始释放玩家..."); playerAttachment.enabled = false; playerAttachment.target = null; StartAttachmentGracePeriod().Forget(); } } }
    private async UniTask StartAttachmentGracePeriod() { isAttachmentInGracePeriod = true; Debug.Log($"抓取功能进入 {attachmentGracePeriodDuration} 秒冷却。"); await UniTask.Delay(System.TimeSpan.FromSeconds(attachmentGracePeriodDuration), cancellationToken: cts.Token); isAttachmentInGracePeriod = false; Debug.Log("抓取功能冷却结束。"); }
    public void RequestPillarSwap(ObiCollider triggeredByPillar = null) { if (isMoving) return; if (lockedPillarParent != null && triggeredByPillar != null) { if ((triggeredByPillar == pillarAObiCollider && lockedPillarParent == pillarParentA) || (triggeredByPillar == pillarBObiCollider && lockedPillarParent == pillarParentB)) { return; } } InitiatePillarSwap().Forget(); }
    private async UniTask InitiatePillarSwap() { isMoving = true; Vector3 sA = pillarParentA.localPosition; Vector3 sB = pillarParentB.localPosition; Vector3 tA, tB; if (isPillarAHigh) { tA = sA - new Vector3(0, moveDistance, 0); tB = sB + new Vector3(0, moveDistance, 0); } else { tA = sA + new Vector3(0, moveDistance, 0); tB = sB - new Vector3(0, moveDistance, 0); } float e = 0f; while (e < moveDuration) { if (cts.IsCancellationRequested) { isMoving = false; return; } e += Time.deltaTime; float t = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp01(e / moveDuration)); pillarParentA.localPosition = Vector3.Lerp(sA, tA, t); pillarParentB.localPosition = Vector3.Lerp(sB, tB, t); await UniTask.Yield(PlayerLoopTiming.Update, cts.Token); } pillarParentA.localPosition = tA; pillarParentB.localPosition = tB; isPillarAHigh = !isPillarAHigh; isMoving = false; await UnlockCartAsync(); }
    private async UniTask UnlockCartAsync() { if (cartRigidbody != null && cartRigidbody.transform.parent != null) { lockedPillarParent = null; cartRigidbody.transform.SetParent(null, worldPositionStays: true); isCartInGracePeriod = true; cartRigidbody.isKinematic = false; await UniTask.Delay(System.TimeSpan.FromSeconds(gracePeriodDuration), cancellationToken: cts.Token); isCartInGracePeriod = false; } }
    #endregion
}