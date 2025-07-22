using UnityEngine;
using System.Collections.Generic;
using Obi;

// V1.0 - 动态鳞片组，负责维护组内鳞片的相对位置
public class ScaleGroup
{
    public ObiColliderBase TargetCollider { get; private set; }
    public List<ScaleProxy> ActiveScales { get; private set; }

    private ScaleProxy anchorScale; // 锚点鳞片
    private Dictionary<ScaleProxy, Vector3> relativePositions; // 相对于锚点的位置
    private Dictionary<ScaleProxy, Quaternion> relativeRotations; // 相对于锚点的旋转

    public ScaleGroup(ObiColliderBase targetCollider)
    {
        this.TargetCollider = targetCollider;
        this.ActiveScales = new List<ScaleProxy>();
        this.relativePositions = new Dictionary<ScaleProxy, Vector3>();
        this.relativeRotations = new Dictionary<ScaleProxy, Quaternion>();
    }

    public void AddScale(ScaleProxy newScale)
    {
        if (anchorScale == null)
        {
            // 如果这是第一个鳞片，它就成为锚点
            anchorScale = newScale;
        }

        ActiveScales.Add(newScale);

        // 计算并存储相对于锚点的变换信息
        // 使用 InverseTransformPoint/Rotation 来获得局部坐标
        Transform anchorTransform = anchorScale.transform;
        relativePositions[newScale] = anchorTransform.InverseTransformPoint(newScale.transform.position);
        relativeRotations[newScale] = Quaternion.Inverse(anchorTransform.rotation) * newScale.transform.rotation;
    }

    public void RemoveScale(ScaleProxy scaleToRemove)
    {
        ActiveScales.Remove(scaleToRemove);
        relativePositions.Remove(scaleToRemove);
        relativeRotations.Remove(scaleToRemove);

        // 如果锚点被移除了，需要选举一个新的锚点
        if (anchorScale == scaleToRemove)
        {
            if (ActiveScales.Count > 0)
            {
                // 简单地选择第一个作为新锚点，并重新计算所有相对位置
                anchorScale = ActiveScales[0];
                RecalculateRelativeTransforms();
            }
            else
            {
                anchorScale = null;
            }
        }
    }

    /// <summary>
    /// 在 LateUpdate 中调用，强制所有鳞片与锚点保持相对静止
    /// </summary>
    public void UpdateGroupTransforms()
    {
        if (anchorScale == null || !anchorScale.gameObject.activeInHierarchy) return;

        Transform anchorTransform = anchorScale.transform;
        
        // 锚点的位置由物理引擎（与外部碰撞体交互）决定
        // 我们需要更新其他所有鳞片的位置
        foreach (var scale in ActiveScales)
        {
            if (scale == anchorScale) continue; // 跳过锚点自身

            // 从存储的相对变换中计算出目标世界变换
            Vector3 targetPosition = anchorTransform.TransformPoint(relativePositions[scale]);
            Quaternion targetRotation = anchorTransform.rotation * relativeRotations[scale];
            
            // 使用 Rigidbody.MovePosition/Rotation 来平滑地更新，避免物理冲突
            scale.Rigidbody.MovePosition(targetPosition);
            scale.Rigidbody.MoveRotation(targetRotation);
        }
    }

    private void RecalculateRelativeTransforms()
    {
        relativePositions.Clear();
        relativeRotations.Clear();
        if (anchorScale == null) return;
        
        Transform anchorTransform = anchorScale.transform;
        foreach (var scale in ActiveScales)
        {
            relativePositions[scale] = anchorTransform.InverseTransformPoint(scale.transform.position);
            relativeRotations[scale] = Quaternion.Inverse(anchorTransform.rotation) * scale.transform.rotation;
        }
    }
    
    public bool IsEmpty()
    {
        return ActiveScales.Count == 0;
    }
}