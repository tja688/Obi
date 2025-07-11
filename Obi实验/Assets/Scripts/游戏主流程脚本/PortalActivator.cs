using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// 传送门激活控制器（增强版）
/// 可指定材质索引和颜色属性名，精确控制颜色渐变
/// </summary>
public class PortalActivator : MonoBehaviour
{
    [Header("传送门组件")]
    [Tooltip("传送门特效的GameObject")]
    public GameObject portalEffect;

    [Tooltip("传送门触碰检测的GameObject")]
    public GameObject portalTrigger;

    [Header("目标对象与材质设置")]
    [Tooltip("需要被替换材质的游戏对象。")]
    public GameObject targetObject;

    [Tooltip("要替换上去的新材质。请从项目资源中拖拽材质到此栏。")]
    public Material newMaterial;

    [Tooltip("要替换的材质在材质组中的索引（从0开始）。")]
    public int materialIndex = 2;

    private bool hasBeenTriggered = false;
    
    
    void Start()
    {
        if (portalEffect != null) portalEffect.SetActive(false);
        if (portalTrigger != null) portalTrigger.SetActive(false);
    }

    public void ActivatePortalSequence()
    {
        if (hasBeenTriggered)
        {
            Debug.Log("材质替换请求被忽略，因为它已经执行过一次了。");
            return;
        }
        
        if (targetObject == null || newMaterial == null)
        {
            Debug.LogError("错误：目标对象(Target Object)或新材质(New Material)未在Inspector中设置！", this);
            return;
        }

        var targetRenderer = targetObject.GetComponent<Renderer>();
        if (targetRenderer == null)
        {
            Debug.LogError("错误：在目标对象上找不到Renderer组件！", this);
            return;
        }

        // 获取当前所有的材质数组。
        var currentMaterials = targetRenderer.materials;

        // 检查索引是否有效。
        if (materialIndex < 0 || materialIndex >= currentMaterials.Length)
        {
            Debug.LogError($"错误：指定的材质索引 {materialIndex} 无效。此对象只有 {currentMaterials.Length} 个材质。", this);
            return;
        }
        
        // 替换指定索引的材质。
        currentMaterials[materialIndex] = newMaterial;

        // 将修改后的材质数组重新应用到Renderer上。
        targetRenderer.materials = currentMaterials;

        // 关键步骤：将标志位设为true，以防止将来再次执行。
        hasBeenTriggered = true;
        
        if (portalEffect != null) portalEffect.SetActive(true);
        
        if (portalTrigger != null) portalTrigger.SetActive(true);
    }
    
}