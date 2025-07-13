// PlayerSwitcher.cs
using UnityEngine;

/// <summary>
/// 一个简单的测试脚本，用于在两个可控对象之间切换。
/// 按 'K' 键来触发切换。
/// </summary>
public class PlayerSwitcher : MonoBehaviour
{
    [Header("玩家对象")]
    [Tooltip("在检视面板中指定第一个玩家对象。")]
    public GameObject playerObject1;

    [Tooltip("在检视面板中指定第二个玩家对象。")]
    public GameObject playerObject2;

    private IControllable controllable1;
    private IControllable controllable2;
    private bool isPlayer1Active = true;

    void Start()
    {
        // 确保 PlayerController 已经存在
        if (PlayerController.instance == null)
        {
            Debug.LogError("[PlayerSwitcher] 场景中找不到 PlayerController 实例！此脚本无法工作。");
            this.enabled = false; // 禁用此脚本
            return;
        }

        // 检查并获取两个玩家对象的 IControllable 接口
        if (playerObject1 != null)
        {
            controllable1 = playerObject1.GetComponent<IControllable>();
        }
        if (playerObject2 != null)
        {
            controllable2 = playerObject2.GetComponent<IControllable>();
        }

        // 验证玩家对象是否设置正确
        if (controllable1 == null || controllable2 == null)
        {
            Debug.LogError("[PlayerSwitcher] 请确保两个玩家对象都已在检视面板中正确指定，并且它们都挂载了实现了 IControllable 接口的脚本（如 PlayerControl_Ball.cs）。");
            this.enabled = false;
            return;
        }

        // 游戏开始时，默认将玩家1注册为当前控制对象
        PlayerController.instance.RegisterPlayer(controllable1);
        isPlayer1Active = true;
        Debug.Log("[PlayerSwitcher] 初始化完成，当前玩家是: " + playerObject1.name);
    }

    void Update()
    {
        // 当按下 'K' 键时
        if (Input.GetKeyDown(KeyCode.K))
        {
            SwitchPlayer();
        }
    }

    /// <summary>
    /// 执行玩家切换的逻辑
    /// </summary>
    private void SwitchPlayer()
    {
        if (isPlayer1Active)
        {
            // 如果当前是玩家1，则切换到玩家2
            Debug.Log("[PlayerSwitcher] 按下 'K' 键，切换到玩家2...");
            PlayerController.instance.RegisterPlayer(controllable2);
        }
        else
        {
            // 如果当前是玩家2，则切换到玩家1
            Debug.Log("[PlayerSwitcher] 按下 'K' 键，切换到玩家1...");
            PlayerController.instance.RegisterPlayer(controllable1);
        }

        // 翻转状态标志
        isPlayer1Active = !isPlayer1Active;
    }
}