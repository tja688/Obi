using UnityEngine;

public class GravityDropTest : MonoBehaviour
{
    [Header("重力设置")]
    public float initialMass = 1.0f; // 初始质量
    public float massStep = 0.1f;    // 质量增减步长
    public Vector3 startPosition;    // 初始位置

    private Rigidbody rb;
    private bool gravityEnabled = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        // 初始化设置
        startPosition = transform.position;
        rb.mass = initialMass;
        rb.useGravity = false;
    }

    void Update()
    {
        // 按G键切换重力
        if (Input.GetKeyDown(KeyCode.G))
        {
            ToggleGravity();
        }

        // 按W键增加质量
        if (Input.GetKey(KeyCode.W))
        {
            ChangeMass(massStep);
        }

        // 按D键减少质量
        if (Input.GetKey(KeyCode.D))
        {
            ChangeMass(-massStep);
        }

        // 按R键重置位置
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetPosition();
        }
    }

    // 切换重力状态
    void ToggleGravity()
    {
        gravityEnabled = !gravityEnabled;
        rb.useGravity = gravityEnabled;
        Debug.Log("重力状态: " + (gravityEnabled ? "开启" : "关闭"));
    }

    // 改变质量
    void ChangeMass(float step)
    {
        rb.mass = Mathf.Max(0.1f, rb.mass + step); // 确保质量不小于0.1
        Debug.Log("当前质量: " + rb.mass.ToString("F2"));
    }

    // 重置位置并关闭重力
    void ResetPosition()
    {
        transform.position = startPosition;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;
        gravityEnabled = false;
        rb.mass = initialMass;
        Debug.Log("已重置位置，重力关闭");
    }

    // 在Inspector中可视化初始位置
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(startPosition, 0.5f);
        Gizmos.DrawLine(transform.position, startPosition);
    }
}