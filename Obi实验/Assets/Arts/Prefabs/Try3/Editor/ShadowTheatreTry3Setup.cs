#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class ShadowTheatreTry3Setup
{
    private const string RendererDataPath = "Assets/Settings/UniversalRenderPipelineAsset_Renderer.asset";

    [MenuItem("Tools/Shadow Theatre/Try3/Setup Renderer Feature")]
    public static void SetupRendererFeature()
    {
        var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererDataPath);
        if (rendererData == null)
        {
            Debug.LogError($"URP RendererData not found at {RendererDataPath}");
            return;
        }

        var features = rendererData.rendererFeatures;
        for (int i = 0; i < features.Count; i++)
        {
            if (features[i] is ShadowProjectorFeature)
            {
                return;
            }
        }

        var feature = ScriptableObject.CreateInstance<ShadowProjectorFeature>();
        feature.name = "ShadowProjectorFeature_Try3";
        rendererData.rendererFeatures.Add(feature);
        AssetDatabase.AddObjectToAsset(feature, rendererData);
        EditorUtility.SetDirty(rendererData);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
#endif
