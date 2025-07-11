// Filename: InputState.cs
using System;
using UnityEngine;

[Serializable]
public class InputState
{
    [Tooltip("用于调试和识别的状态名称")]
    public string StateName = "New Input State";

    [Tooltip("在此状态下需要被激活的Action Map的名称列表")]
    public string[] ActionMapsToEnable;

    [Tooltip("是否禁用底层/默认的玩家输入？如果为true，则只有上面列表中的Action Map会被激活。")]
    public bool DisableBaseInputs = true;
}