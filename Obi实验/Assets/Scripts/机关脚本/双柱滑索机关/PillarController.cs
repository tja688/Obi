using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using Obi;

public class PillarController : MonoBehaviour
{
    [Header("场景对象引用")]
    [Tooltip("柱子A的稳定父对象")]
    [SerializeField] private Transform pillarParentA;
    [Tooltip("柱子B的稳定父对象")]
    [SerializeField] private Transform pillarParentB;
    [SerializeField] private Rigidbody cartRigidbody;

    [Header("运动参数")]
    [Tooltip("柱子升降动画的持续时间（秒）")]
    [SerializeField] private float moveDuration = 2.0f;
    [Tooltip("解锁后的冷却时间（秒），期间车子不会被再次锁定")]
    [SerializeField] private float gracePeriodDuration = 0.5f;
    [Tooltip("每次升降的固定距离")]
    [SerializeField] private float moveDistance = 1.6f;

    [Header("Obi 碰撞设置 (玩家 vs 柱子)")]
    [SerializeField] private ObiSolver obiSolver;
    [SerializeField] private ObiActor playerActor;
    // [修改] 将独立的触发器引用改为柱子自身的Obi碰撞体引用
    [Tooltip("柱子A上用于接收玩家碰撞的Obi Collider")]
    [SerializeField] private ObiCollider pillarAObiCollider;
    [Tooltip("柱子B上用于接收玩家碰撞的Obi Collider")]
    [SerializeField] private ObiCollider pillarBObiCollider;


    public bool IsCartInGracePeriod { get; private set; } = false;
    
    private bool isPillarAHigh = true;
    private bool isMoving = false;
    private CancellationTokenSource cts;

    #region Unity生命周期
    void Start()
    {
        // 验证所有必要的引用是否都已设置
        if (pillarParentA == null || pillarParentB == null || obiSolver == null || playerActor == null || cartRigidbody == null || pillarAObiCollider == null || pillarBObiCollider == null)
        {
            Debug.LogError($"[{name}] 核心组件引用未设置，请检查Inspector！脚本已禁用。", this);
            enabled = false;
            return;
        }
        
        cts = new CancellationTokenSource();
    }
    
    void OnEnable()
    {
        if (obiSolver != null)
        {
            obiSolver.OnCollision += OnObiPlayerCollision;
        }
    }

    void OnDisable()
    {
        if (obiSolver != null)
        {
            obiSolver.OnCollision -= OnObiPlayerCollision;
        }
    }

    void OnDestroy()
    {
        if (cts != null)
        {
            cts.Cancel();
            cts.Dispose();
        }
    }
    #endregion

    #region 核心逻辑 (移动和锁定/解锁部分无需修改)
    public void LockCartToPillarParent(Transform pillarParentTransform)
    {
        if (cartRigidbody != null && cartRigidbody.transform.parent != pillarParentTransform)
        {
            cartRigidbody.isKinematic = true;
            cartRigidbody.transform.SetParent(pillarParentTransform, worldPositionStays: true);
            Debug.Log($"运输车已硬锁定到父对象: {pillarParentTransform.name}。");
        }
    }
    
    private async UniTask MovePillarsAsync(CancellationToken token)
    {
        isMoving = true;
        
        Vector3 startPosA = pillarParentA.localPosition;
        Vector3 startPosB = pillarParentB.localPosition;
        Vector3 targetPosA, targetPosB;

        if (isPillarAHigh)
        {
            targetPosA = startPosA - new Vector3(0, moveDistance, 0);
            targetPosB = startPosB + new Vector3(0, moveDistance, 0);
        }
        else
        {
            targetPosA = startPosA + new Vector3(0, moveDistance, 0);
            targetPosB = startPosB - new Vector3(0, moveDistance, 0);
        }

        float elapsedTime = 0f;
        while (elapsedTime < moveDuration)
        {
            if (token.IsCancellationRequested) { isMoving = false; return; }

            elapsedTime += Time.deltaTime;
            float t = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp01(elapsedTime / moveDuration));
            
            pillarParentA.localPosition = Vector3.Lerp(startPosA, targetPosA, t);
            pillarParentB.localPosition = Vector3.Lerp(startPosB, targetPosB, t);

            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        pillarParentA.localPosition = targetPosA;
        pillarParentB.localPosition = targetPosB;
        isPillarAHigh = !isPillarAHigh;
        Debug.Log("父对象移动完成。");
        isMoving = false;
        
        await UnlockCartAsync();
    }

    private async UniTask UnlockCartAsync()
    {
        if (cartRigidbody != null && cartRigidbody.transform.parent != null)
        {
            cartRigidbody.transform.SetParent(null, worldPositionStays: true);
            IsCartInGracePeriod = true;
            cartRigidbody.isKinematic = false;
            Debug.Log($"运输车已解锁，进入 {gracePeriodDuration} 秒冷却期。");
            await UniTask.Delay(System.TimeSpan.FromSeconds(gracePeriodDuration), cancellationToken: cts.Token);
            IsCartInGracePeriod = false;
            Debug.Log("冷却期结束。");
        }
    }
    #endregion

    #region 碰撞与触发 (逻辑修改点)
    /// <summary>
    /// Obi碰撞回调：检测玩家与【柱子】的接触
    /// </summary>
    private void OnObiPlayerCollision(ObiSolver solver, ObiNativeContactList contacts)
    {
        if (isMoving) return;
        for (int i = 0; i < contacts.count; ++i)
        {
            Oni.Contact contact = contacts[i];
            
            // [修改] 调用新的辅助方法IsPillarObiCollider来判断
            if ((IsPlayerParticle(contact.bodyA) && IsPillarObiCollider(contact.bodyB)) ||
                (IsPlayerParticle(contact.bodyB) && IsPillarObiCollider(contact.bodyA)))
            {
                TriggerPillarSwap();
                return; 
            }
        }
    }

    private void TriggerPillarSwap()
    {
        if (isMoving) return;
        Debug.Log("玩家触碰柱子，触发升降流程。");
        MovePillarsAsync(cts.Token).Forget();
    }
    
    /// <summary>
    /// [修改] 新的辅助方法，用于判断碰撞体是否为两个目标柱子之一
    /// </summary>
    private bool IsPillarObiCollider(int colliderIndex)
    {
        var world = ObiColliderWorld.GetInstance();
        if (colliderIndex < 0 || colliderIndex >= world.colliderHandles.Count) return false;
        var owner = world.colliderHandles[colliderIndex].owner;
        return owner == pillarAObiCollider || owner == pillarBObiCollider;
    }

    private bool IsPlayerParticle(int particleIndex)
    {
        if (particleIndex < 0 || particleIndex >= obiSolver.particleToActor.Length) return false;
        var particleOwnerInfo = obiSolver.particleToActor[particleIndex];
        return particleOwnerInfo != null && particleOwnerInfo.actor == playerActor;
    }
    #endregion
}