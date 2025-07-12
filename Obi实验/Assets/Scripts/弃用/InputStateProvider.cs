// Filename: InputStateProvider.cs
// Modified to include its own UnityEvents for activation and deactivation.

using UnityEngine;
using UnityEngine.Events; // 1. 引入 UnityEvent 命名空间

public class InputStateProvider : MonoBehaviour
{
    [Header("输入状态配置")]
    [Tooltip("此组件提供的输入状态配置")]
    [SerializeField] private InputState definedState;

    [Header("状态切换时触发的事件")]
    [Tooltip("当调用 ActivateState 成功后触发的事件")]
    public UnityEvent OnStateActivated; // 2. 添加公开的 UnityEvent，用于状态激活

    [Tooltip("当调用 DeactivateState 成功后触发的事件")]
    public UnityEvent OnStateDeactivated; // 3. 添加公开的 UnityEvent，用于状态停用

    /// <summary>
    /// 公开方法，用于被UnityEvent调用以激活此状态。
    /// </summary>
    public void ActivateState()
    {
        if (InputStateManager.Instance != null)
        {
            InputStateManager.Instance.PushState(definedState);
            
            // 4. 在状态成功推入后，调用（Invoke）此事件
            // 所有在 Inspector 中配置的函数都将被执行
            OnStateActivated?.Invoke();
        }
    }

    /// <summary>
    /// 公开方法，用于被UnityEvent调用以退出此状态。
    /// </summary>
    public void DeactivateState()
    {
        if (InputStateManager.Instance != null)
        {
            // 注意：PopState现在有安全检查，我们假设它成功时才触发事件
            // 如果需要更严格的成功回调，需要修改InputStateManager的PopState方法让它返回一个bool值
            InputStateManager.Instance.PopState(definedState);

            // 5. 在状态尝试弹出后，调用（Invoke）此事件
            OnStateDeactivated?.Invoke();
        }
    }
}