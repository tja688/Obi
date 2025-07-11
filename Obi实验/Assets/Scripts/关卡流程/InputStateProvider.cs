// Filename: InputStateProvider.cs
using UnityEngine;

public class InputStateProvider : MonoBehaviour
{
    [Tooltip("此组件提供的输入状态配置")]
    [SerializeField] private InputState definedState;

    /// <summary>
    /// 公开方法，用于被UnityEvent调用以激活此状态。
    /// </summary>
    public void ActivateState()
    {
        if (InputStateManager.Instance != null)
        {
            InputStateManager.Instance.PushState(definedState);
        }
    }

    /// <summary>
    /// 公开方法，用于被UnityEvent调用以退出此状态。
    /// </summary>
    public void DeactivateState()
    {
        if (InputStateManager.Instance != null)
        {
            InputStateManager.Instance.PopState(definedState);
        }
    }
}