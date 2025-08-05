using UnityEngine;

public class CollisionNotifier : MonoBehaviour
{
    [Header("要通知的主脚本")]
    [Tooltip("拖入带有 RecordDeformationOnMassIncrease 脚本的那个ObiSoftbody对象")]
    public RecordDeformationOnMassIncrease deformationRecorder;

    void Awake()
    {
        // 安全检查
        if (deformationRecorder == null)
        {
            Debug.LogError("错误：请在Inspector面板中指定 Deformation Recorder！");
            enabled = false;
        }
    }

    // 当此物体发生碰撞时，Unity会自动调用这个方法
    void OnCollisionEnter(Collision collision)
    {
        // 检查是否碰撞到了标签为 "Floor" 的物体
        if (collision.gameObject.CompareTag("Floor"))
        {
            // 调用主脚本的公共方法来停止模拟
            if (deformationRecorder != null)
            {
                Debug.Log($"刚体 {gameObject.name} 触碰到了 'Floor', 发出停止信号。");
                deformationRecorder.StopSimulationByCollision();
            }
        }
    }
}