using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class FindMissingMaterial : EditorWindow
{
    private static List<GameObject> objectsWithMissingMaterial = new List<GameObject>();

    [MenuItem("Tools/Find Objects With Missing Material")]
    public static void Find()
    {
        objectsWithMissingMaterial.Clear();
        GameObject[] allObjects = FindObjectsOfType<GameObject>();

        foreach (GameObject obj in allObjects)
        {
            // 检查 Mesh Renderer 组件
            MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                foreach (Material mat in renderer.sharedMaterials)
                {
                    if (mat == null)
                    {
                        objectsWithMissingMaterial.Add(obj);
                        Debug.LogWarning("Found object with missing material: " + obj.name, obj);
                        break; // 找到一个空材质就可以跳出内层循环
                    }
                }
            }

            // 如果有需要，也可以检查 Skinned Mesh Renderer
            SkinnedMeshRenderer skinnedRenderer = obj.GetComponent<SkinnedMeshRenderer>();
            if (skinnedRenderer != null)
            {
                foreach (Material mat in skinnedRenderer.sharedMaterials)
                {
                    if (mat == null)
                    {
                        objectsWithMissingMaterial.Add(obj);
                        Debug.LogWarning("Found object with missing material: " + obj.name, obj);
                        break;
                    }
                }
            }
        }

        if (objectsWithMissingMaterial.Count > 0)
        {
            Debug.Log("--------------------------------------------------");
            Debug.Log($"Total found: {objectsWithMissingMaterial.Count} objects with missing materials. See warnings for details.");
            // 将找到的物体在 Hierarchy 窗口中高亮显示
            Selection.objects = objectsWithMissingMaterial.ToArray();
        }
        else
        {
            Debug.Log("No objects with missing materials found in the current scene.");
        }
    }
}