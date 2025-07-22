using System.Collections.Generic;
using UnityEngine;
using Obi;

/// <summary>
/// 极简质量修改器 (穩定基線測試)
/// 目標：用最簡單、最直接的方式驗證“動態修改質量”這一核心操作的可行性。
/// 功能：當按下 Q 鍵時，立即將此軟體對象的所有粒子的質量設置為一個指定值。
/// </summary>
[RequireComponent(typeof(ObiSoftbody))]
public class SimpleMassChanger : MonoBehaviour
{
    [Header("碰撞目标与质量设置")]
    [Tooltip("將需要檢測碰撞的物體的碰撞體(Collider)拖拽到這裡。")]
    public List<Collider> targetColliders = new List<Collider>();

    [Tooltip("與目標物體碰撞時，軟體的總質量將被設置為此值。")]
    public float massOnCollision = 100f;

    // 内部状态变量
    private ObiSoftbody softbody;
    private ObiSolver solver;
    private float originalTotalMass;
    private bool isInitialized = false;

    // 【核心状态】使用一个 HashSet 來進行高效的碰撞體查找，性能遠高於 List.Contains()。
    private HashSet<Collider> targetColliderSet;

    // 【核心状态】用於追蹤當前是否正處於“質量被修改”的狀態，以避免重複設置。
    private bool massHasBeenModified = false;

    #region 初始化与事件订阅

    void Awake()
    {
        softbody = GetComponent<ObiSoftbody>();
        // 在 Awake 中初始化 HashSet，確保在任何事件觸發前它都已準備就緒。
        targetColliderSet = new HashSet<Collider>(targetColliders);
    }

    void OnEnable()
    {
        // 藍圖加載是異步的，我們需要監聽它以確保能安全地訪問 Solver。
        softbody.OnBlueprintLoaded += OnBlueprintLoaded;
        if (softbody.isLoaded)
        {
            OnBlueprintLoaded(softbody, softbody.sourceBlueprint);
        }
    }

    void OnDisable()
    {
        // 好習慣：在禁用時取消訂閱，防止內存洩漏和意外調用。
        softbody.OnBlueprintLoaded -= OnBlueprintLoaded;
        if (solver != null)
        {
            solver.OnCollision -= HandleCollision;
        }
        isInitialized = false;
    }

    private void OnBlueprintLoaded(ObiActor actor, ObiActorBlueprint blueprint)
    {
        solver = softbody.solver;
        if (solver != null)
        {
            // 【關鍵】訂閱 Solver 的 OnCollision 事件。
            solver.OnCollision -= HandleCollision; // 先取消，防止重複訂閱
            solver.OnCollision += HandleCollision;

            // 存儲原始質量，以便之後恢復。
            originalTotalMass = softbody.massScale;
            Debug.Log($"軟體原始總質量已記錄: {originalTotalMass}");
        }
        isInitialized = true;
    }

    #endregion

    #region 核心碰撞處理邏輯

    /// <summary>
    /// 這個方法會在每個物理步驟結束後被 ObiSolver 調用，包含了該步驟中所有的接觸信息。
    /// </summary>
    private void HandleCollision(ObiSolver solver, ObiNativeContactList contacts)
    {
        if (!isInitialized || targetColliderSet.Count == 0) return;

        bool foundTargetThisFrame = false;
        var world = ObiColliderWorld.GetInstance();

        // 遍歷當前物理幀的所有接觸點
        foreach (var contact in contacts)
        {
            // 根據官方示例，我們可以忽略距離太遠的“接觸點”
            if (contact.distance > 0.01f) continue;
            
            // contact.bodyA 是粒子索引，contact.bodyB 是另一個物體（碰撞體）的句柄索引。
            var colliderHandle = world.colliderHandles[contact.bodyB];
            var obiCollider = colliderHandle.owner;

            if (obiCollider != null)
            {
                // 從 ObiCollider 獲取其關聯的 Unity Collider 組件
                var unityCollider = obiCollider.GetComponent<Collider>();
                if (unityCollider != null && targetColliderSet.Contains(unityCollider))
                {
                    // 只要找到一個接觸點與我們的目標列表匹配，就立即標記並跳出循環。
                    foundTargetThisFrame = true;
                    break;
                }
            }
        }

        // --- 状态机逻辑 ---
        // 根據本幀的檢測結果，與上一幀的狀態進行比較，決定是否需要修改質量。

        // 情況一：本幀檢測到碰撞，且質量尚未被修改 -> 剛進入碰撞狀態
        if (foundTargetThisFrame && !massHasBeenModified)
        {
            Debug.Log("<color=red>檢測到與目標物體發生碰撞！正在增加質量...</color>");
            softbody.SetMass(massOnCollision);
            massHasBeenModified = true; // 更新狀態，防止下一幀重複設置
        }
        // 情況二：本幀未檢測到碰撞，但質量此前被修改過 -> 剛脫離碰撞狀態
        else if (!foundTargetThisFrame && massHasBeenModified)
        {
            Debug.Log("<color=green>與目標物體的碰撞已結束。正在恢復原始質量...</color>");
            softbody.SetMass(originalTotalMass);
            massHasBeenModified = false; // 更新狀態
        }
    }

    #endregion
}