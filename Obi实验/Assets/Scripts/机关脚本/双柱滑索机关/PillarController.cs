using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using Obi;

/// <summary>
/// 核心控制器（已更新）：
/// 1. 移动对象改为稳定的【柱子父对象】。
/// 2. 锁定/解锁逻辑保持不变，但操作对象由CartCollisionHandler传入。
/// </summary>
public class PillarController : MonoBehaviour
{
    [Header("场景对象引用")]
    // **[已修改]** 引用稳定的父对象而非柱子模型
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

    [Header("Obi 碰撞设置 (玩家 vs 触发器)")]
    [SerializeField] private ObiSolver obiSolver;
    [SerializeField] private ObiActor playerActor; 
    [SerializeField] private ObiCollider triggerAObiCollider;
    [SerializeField] private ObiCollider triggerBObiCollider;

    public bool IsCartInGracePeriod { get; private set; } = false;

    private Vector3 initialPosA;
    private Vector3 initialPosB;
    private bool isMoving = false;
    private CancellationTokenSource cts;

    #region Unity生命周期
    void Start()
    {
        if (pillarParentA == null || pillarParentB == null || obiSolver == null || playerActor == null || cartRigidbody == null)
        {
            Debug.LogError($"[{name}] 核心组件引用未设置，脚本已禁用!", this);
            enabled = false;
            return;
        }
        // **[已修改]** 记录父对象的初始位置
        initialPosA = pillarParentA.position;
        initialPosB = pillarParentB.position;
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

    #region 核心逻辑
    /// <summary>
    /// 硬锁定：将车设置为碰撞目标（触发球）的父对象
    /// </summary>
    public void LockCartToPillarParent(Transform pillarParentTransform)
    {
        if (cartRigidbody != null && cartRigidbody.transform.parent != pillarParentTransform)
        {
            cartRigidbody.isKinematic = true; 
            cartRigidbody.transform.SetParent(pillarParentTransform, worldPositionStays: true);
            Debug.Log($"运输车已硬锁定到父对象: {pillarParentTransform.name}。");
        }
    }
    
    /// <summary>
    /// 异步控制【父对象】的升降动画
    /// </summary>
    private async UniTask MovePillarsAsync(CancellationToken token)
    {
        isMoving = true;
        
        // **[已修改]** 获取父对象的当前和目标位置
        Vector3 startPosA = pillarParentA.position;
        Vector3 startPosB = pillarParentB.position;
        Vector3 targetPosA = new Vector3(initialPosA.x, initialPosB.y, initialPosA.z);
        Vector3 targetPosB = new Vector3(initialPosB.x, initialPosA.y, initialPosB.z);

        float elapsedTime = 0f;
        while (elapsedTime < moveDuration)
        {
            if (token.IsCancellationRequested) { isMoving = false; return; }

            elapsedTime += Time.deltaTime;
            float t = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp01(elapsedTime / moveDuration));
            
            // **[已修改]** 移动父对象
            pillarParentA.position = Vector3.Lerp(startPosA, targetPosA, t);
            pillarParentB.position = Vector3.Lerp(startPosB, targetPosB, t);

            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        // **[已修改]** 确保父对象到达最终位置
        pillarParentA.position = targetPosA;
        pillarParentB.position = targetPosB;
        
        var tempPosA = initialPosA;
        initialPosA = targetPosA;
        initialPosB = new Vector3(initialPosB.x, tempPosA.y, initialPosB.z);

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

    #region 碰撞与触发 (这部分无需修改)
    private void OnObiPlayerCollision(ObiSolver solver, ObiNativeContactList contacts)
    {
        if (isMoving) return;
        for (int i = 0; i < contacts.count; ++i)
        {
            Oni.Contact contact = contacts[i];
            if ((IsPlayerParticle(contact.bodyA) && IsTriggerCollider(contact.bodyB)) ||
                (IsPlayerParticle(contact.bodyB) && IsTriggerCollider(contact.bodyA)))
            {
                TriggerPillarSwap();
                return; 
            }
        }
    }
    private void TriggerPillarSwap()
    {
        if (isMoving) return;
        Debug.Log("玩家触发升降流程。");
        MovePillarsAsync(cts.Token).Forget();
    }
    private bool IsTriggerCollider(int colliderIndex)
    {
        var world = ObiColliderWorld.GetInstance();
        if (colliderIndex < 0 || colliderIndex >= world.colliderHandles.Count) return false;
        var owner = world.colliderHandles[colliderIndex].owner;
        return owner == triggerAObiCollider || owner == triggerBObiCollider;
    }
    private bool IsPlayerParticle(int particleIndex)
    {
        if (particleIndex < 0 || particleIndex >= obiSolver.particleToActor.Length) return false;
        var particleOwnerInfo = obiSolver.particleToActor[particleIndex];
        return particleOwnerInfo != null && particleOwnerInfo.actor == playerActor;
    }
    #endregion
}