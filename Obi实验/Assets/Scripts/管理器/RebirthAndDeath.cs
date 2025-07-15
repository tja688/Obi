// RebirthAndDeath.cs (最终修正版)
using UnityEngine;
using UnityEngine.SceneManagement;
using Obi;

/// <summary>
/// 【重构】负责处理角色的死亡与关卡重载。
/// 当玩家死亡或主动请求重启时，将重新加载当前关卡。
/// </summary>
public class RebirthAndDeath : MonoBehaviour
{
    [Header("碰撞体设置")]
    [Tooltip("碰到此碰撞体将触发死亡并重载关卡")]
    public ObiCollider deathPitCollider;
    [Tooltip("碰到此碰撞体将触发通关（逻辑可在此扩展）")]
    public ObiCollider finishCollider;

    private ObiSolver solver;
    
    // 【新增】加载锁，防止重复调用重载方法
    private bool isLoading = false;

    private void Awake()
    {
        EventCenter.AddListener<IControllable>("PlayerChange", OnPlayerChanged);

        EventCenter.AddListener("GameRestartRequested", RestartLevel);
    }

    private void OnPlayerChanged(IControllable newPlayer)
    {
        if (solver != null)
        {
            solver.OnCollision -= Solver_OnCollision;
        }

        var playerObject = newPlayer.controlledGameObject;
        solver = playerObject.GetComponentInParent<ObiSolver>();

        if (solver != null)
        {
            solver.OnCollision += Solver_OnCollision;
            Debug.Log($"[RebirthAndDeath] 已成功订阅新玩家 {playerObject.name} 的 ObiSolver 事件。");
        }
        else
        {
            Debug.LogError($"[RebirthAndDeath] 在新玩家 {playerObject.name} 的子对象中未找到 ObiSolver！", playerObject);
        }
    }

    private void Solver_OnCollision(ObiSolver s, ObiNativeContactList e)
    {
        var world = ObiColliderWorld.GetInstance();
        foreach (var contact in e)
        {
            if (!(contact.distance > 0.01)) continue;

            var col = world.colliderHandles[contact.bodyB].owner;

            if (col == deathPitCollider)
            {
                Debug.Log("[RebirthAndDeath] 玩家接触死亡区域，触发关卡重载。");
                RestartLevel();
                return;
            }

            if (col == finishCollider)
            {
                EventCenter.TriggerEvent("LevelFinished");
                this.enabled = false; 
                return;
            }
        }
    }
    
    public void RestartLevel()
    {
        // 【新增】检查加载锁
        if (isLoading) return;
        isLoading = true; // 上锁

        Debug.Log($"[RebirthAndDeath] 正在重载当前关卡: {SceneManager.GetActiveScene().name}");
        CleanUpListeners();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void CleanUpListeners()
    {
        if (solver != null)
        {
            solver.OnCollision -= Solver_OnCollision;
        }
        EventCenter.RemoveListener<IControllable>("PlayerChange", OnPlayerChanged);
        EventCenter.RemoveListener("GameRestartRequested", RestartLevel);
    }

    private void OnDestroy()
    {
        CleanUpListeners();
    }
}