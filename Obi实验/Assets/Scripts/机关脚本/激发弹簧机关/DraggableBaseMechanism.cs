// DraggableBaseMechanism.cs
using UnityEngine;
using System.Collections;

/// <summary>
/// 可拖拽基座机关的具体实现。
/// 玩家激活机关后，可以点击并拖动一个指定的对象。
/// 【新】松开鼠标后，对象会自动归位。
/// 该对象的移动范围由其自身附加的 ObjectLimit 脚本来限制。
/// </summary>
public class DraggableBaseMechanism : MechanismBase
{
    [Header("机关专属设置")]
    [Tooltip("玩家可以拖拽的目标游戏对象。该对象上应挂载 ObjectLimit 脚本。")]
    public GameObject draggableObject;

    [Tooltip("当目标对象被选中并拖拽时，应用此高亮材质。")]
    public Material grabbedMaterial;
    
    [Header("射线检测设置")]
    [Tooltip("指定哪些图层上的物体是可以被射线抓取的。请确保 Draggable Object 在此图层中。")]
    public LayerMask grabbableLayerMask;

    // --- 内部变量 ---
    private Camera mainCamera;
    private Renderer objectRenderer;
    private Material defaultMaterial; // 用于存储对象的原始材质
    private Vector3 initialLocalPosition; // 用于复位

    private bool isDragging = false;
    // 拖拽时，鼠标在世界空间中的点击点与物体枢轴点的偏移量
    private Vector3 worldSpaceDragOffset; 

    /// <summary>
    /// 初始化机关，获取必要的组件和初始状态。
    /// </summary>
    protected override void Start()
    {
        base.Start(); // 执行基类的Start逻辑
        mainCamera = Camera.main;

        if (draggableObject == null)
        {
            Debug.LogError("请在Inspector面板中指定 'Draggable Object'！", this);
            enabled = false;
            return;
        }

        // 获取并存储渲染器和默认材质，用于后续的颜色变化和复位
        objectRenderer = draggableObject.GetComponent<Renderer>();
        if (objectRenderer == null)
        {
            Debug.LogError($"指定的对象 '{draggableObject.name}' 上没有找到 Renderer 组件！", this);
            enabled = false;
            return;
        }
        defaultMaterial = objectRenderer.material;

        // 记录初始位置，以便复位
        initialLocalPosition = draggableObject.transform.localPosition;
    }

    /// <summary>
    /// 处理鼠标左键的按下与抬起事件。
    /// </summary>
    public override void OnLeftButton(bool isPressed)
    {
        if (isPressed)
        {
            // 鼠标按下时，进行射线检测，判断是否点中了目标对象
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            
            if (Physics.Raycast(ray, out RaycastHit hit, 200f, grabbableLayerMask))
            {
                // 确保射中的是我们想要控制的那个可拖拽对象
                if (hit.transform.gameObject == draggableObject)
                {
                    isDragging = true;
                    
                    // --- 计算拖拽偏移量 ---
                    // 为了实现平滑拖拽，我们需要计算鼠标点击点和物体中心点的偏移
                    // 1. 创建一个与相机视线垂直、且穿过物体中心的平面
                    Plane dragPlane = new Plane(mainCamera.transform.forward, draggableObject.transform.position);
                    // 2. 射线投射到这个平面上，得到世界空间的精确点击点
                    if (dragPlane.Raycast(ray, out float distance))
                    {
                        Vector3 hitPoint = ray.GetPoint(distance);
                        // 3. 计算并存储这个偏移量
                        worldSpaceDragOffset = draggableObject.transform.position - hitPoint;
                    }
                    else
                    {
                        // 如果射线投射失败（虽然不太可能），使用一个备用方案
                        worldSpaceDragOffset = Vector3.zero;
                    }

                    // 切换为高亮材质
                    objectRenderer.material = grabbedMaterial;
                    Debug.Log($"抓住了对象: {draggableObject.name}");
                }
            }
        }
        else
        {
            // 鼠标抬起时，释放控制
            if (isDragging)
            {
                isDragging = false;
                // 恢复为默认材质
                objectRenderer.material = defaultMaterial;
                Debug.Log($"松开了对象: {draggableObject.name}");

                // --- 【新增改动】 ---
                // 根据要求，松开鼠标后将对象瞬移回初始位置
                draggableObject.transform.localPosition = initialLocalPosition;
                Debug.Log($"对象 '{draggableObject.name}' 已归位。");
            }
        }
    }

    /// <summary>
    /// 处理鼠标移动事件，用于拖拽对象。
    /// </summary>
    public override void OnMouseMove(Vector2 position)
    {
        // 只有在抓取了对象时才处理鼠标移动
        if (!isDragging) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        // 同样使用之前定义的拖拽平面来计算鼠标当前在世界空间中的位置
        Plane dragPlane = new Plane(mainCamera.transform.forward, draggableObject.transform.position);
        if (dragPlane.Raycast(ray, out float distance))
        {
            // 计算出鼠标在世界中的目标位置，并加上之前算好的偏移量
            Vector3 targetWorldPos = ray.GetPoint(distance) + worldSpaceDragOffset;

            // 更新物体的位置
            // 注意：我们在这里设置的是世界坐标 (position)
            // 附加在对象上的 ObjectLimit 脚本会在其 Update/LateUpdate 中
            // 自动将这个位置约束到其定义的 localPosition 范围内。
            draggableObject.transform.position = targetWorldPos;
        }
    }

    /// <summary>
    /// 处理中途退出机关的逻辑。
    /// </summary>
    public override void OnQuit()
    {
        // 如果玩家在拖拽时按下了退出键，也应该释放控制并恢复材质
        if (isDragging)
        {
            isDragging = false;
            objectRenderer.material = defaultMaterial;
        }

        // 调用基类的方法，以触发状态切换到Resetting
        base.OnQuit();
    }

    /// <summary>
    /// 重写基类的复位序列，定义此机关专属的复位行为。
    /// </summary>
    protected override IEnumerator ResetSequence()
    {
        Debug.Log("可拖拽底座机关开始复位...");

        // 如果因为玩家离开触发器等原因导致复位时，物体仍处于拖拽状态，则强制取消。
        if (isDragging)
        {
            isDragging = false;
        }
        
        // 恢复对象的默认材质
        objectRenderer.material = defaultMaterial;

        // 将对象的位置复位到初始状态（这里使用瞬时复位）
        draggableObject.transform.localPosition = initialLocalPosition;

        // 等待一帧以确保所有状态更新
        yield return null;

        Debug.Log("可拖拽底座机关复位完成，切换到待机状态。");
        // 【重要】调用基类方法完成状态转换
        ChangeState(MechanismState.Standby);
    }
}