using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class ShadowTheatreTry3Controller : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private Transform mLightTransform;
    [SerializeField] private Renderer mScreenRenderer;
    [SerializeField] private bool mAutoCollectPuppets = true;
    [SerializeField] private List<SpriteRenderer> mPuppets = new List<SpriteRenderer>();

    [Header("Shadow Settings")]
    [SerializeField] private float mLightRadius = 0.25f;
    [SerializeField] private float mBlurScale = 1f;
    [SerializeField] private float mMaxBlurPixels = 20f;
    [SerializeField] private Color mShadowTint = new Color(0.2f, 0.2f, 0.2f, 1f);
    [SerializeField] private float mShadowStrength = 1f;

    [Header("Depth Blur (Per Sprite)")]
    [SerializeField] private bool mEnableDepthBlur = true;
    [SerializeField, Tooltip("Distance to screen plane where blur starts (world units). Screen at z=0 => z-distance.")]
    private float mDepthBlurNear = 0.02f;
    [SerializeField, Tooltip("Distance to screen plane where blur reaches full strength (world units).")]
    private float mDepthBlurFar = 0.35f;
    [SerializeField, Tooltip("Compute blur per-vertex for better gradients when limbs rotate in 3D.")]
    private bool mPerVertexDepthBlur = true;

    [Header("Hybrid (Near Translucent)")]
    [SerializeField] private bool mEnableNearTranslucent = true;
    [SerializeField, Tooltip("When puppet is closer than this (world units), use translucent puppet projection (old look).")]
    private float mNearDistance = 0.05f;
    [SerializeField, Tooltip("When puppet is farther than this (world units), use Try3 shadow look.")]
    private float mFarDistance = 0.35f;
    [SerializeField, Range(0f, 1f)] private float mPuppetStrength = 0.85f;
    [SerializeField] private float mPuppetAlphaBoost = 1.0f;
    [SerializeField, Range(0f, 1f), Tooltip("Scale applied to per-pixel puppet/shadow blend (0 = puppet only).")]
    private float mShadowBlendScale = 1f;

    [Header("Render Texture")]
    [SerializeField] private int mRenderTextureSize = 2048;

    [Header("Assets (Auto-Created in Editor)")]
    [SerializeField] private Material mScreenMaterial;
    [SerializeField] private Material mProjectorMaterial;
    [SerializeField] private RenderTexture mShadowTexture;
    [SerializeField] private Material mPuppetProjectorMaterial;
    [SerializeField] private RenderTexture mPuppetTexture;

    [Header("Layer")]
    [SerializeField] private string mPuppetLayerName = "PuppetLayer";
    [SerializeField] private bool mApplyPuppetLayer = true;

    private readonly List<PuppetMesh> mPuppetMeshes = new List<PuppetMesh>();
    private readonly List<ShadowProjectorDrawItem> mDrawItems = new List<ShadowProjectorDrawItem>();

    private static readonly int ShadowTexId = Shader.PropertyToID("_ShadowTex");
    private static readonly int PuppetTexId = Shader.PropertyToID("_PuppetTex");
    private static readonly int PuppetStrengthId = Shader.PropertyToID("_PuppetStrength");
    private static readonly int ShadowBlendId = Shader.PropertyToID("_ShadowBlend");
    private static readonly int ShadowTintId = Shader.PropertyToID("_ShadowTint");
    private static readonly int ShadowStrengthId = Shader.PropertyToID("_ShadowStrength");
    private static readonly int LightCenterId = Shader.PropertyToID("_LightCenter");
    private static readonly int AlphaBoostId = Shader.PropertyToID("_AlphaBoost");

    private const string Try3Path = "Assets/Arts/Prefabs/Try3";
    private const string ScreenMaterialPath = Try3Path + "/ShadowScreenTry3.mat";
    private const string ProjectorMaterialPath = Try3Path + "/ShadowProjectorTry3.mat";
    private const string RenderTexturePath = Try3Path + "/RT_ShadowTry3.renderTexture";
    private const string PuppetProjectorMaterialPath = Try3Path + "/ShadowPuppetProjectorTry3.mat";
    private const string PuppetRenderTexturePath = Try3Path + "/RT_PuppetTry3.renderTexture";
    private const string ScreenShaderName = "Custom/ShadowScreenTry3";
    private const string ProjectorShaderName = "Custom/ShadowProjectorTry3";
    private const string PuppetProjectorShaderName = "Custom/ShadowPuppetProjectorTry3";

    private void OnEnable()
    {
        EnsureSetup();
    }

    private void Update()
    {
        EnsureSetup();
        UpdateProjection();
    }

    private void OnDisable()
    {
        ShadowProjectorContext.Clear();
    }

    private void EnsureSetup()
    {
        EnsureLight();
        EnsureScreen();
        EnsureAssets();
        EnsurePuppets();
        EnsureMainCameraMask();
    }

    private void EnsureLight()
    {
        if (mLightTransform != null)
        {
            return;
        }

        var lightTransform = transform.Find("ShadowLightTry3");
        if (lightTransform == null)
        {
            var lightObject = new GameObject("ShadowLightTry3");
            lightObject.transform.SetParent(transform);
            lightObject.transform.localPosition = new Vector3(0f, 0f, 5f);
            lightObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            lightTransform = lightObject.transform;
        }

        mLightTransform = lightTransform;

        Light lightComponent;
        if (!lightTransform.TryGetComponent(out lightComponent))
        {
            lightComponent = lightTransform.gameObject.AddComponent<Light>();
        }

        lightComponent.type = LightType.Point;
        lightComponent.color = new Color(1f, 0.9f, 0.75f, 1f);
        lightComponent.intensity = 3f;
        lightComponent.range = 20f;
    }

    private void EnsureScreen()
    {
        if (mScreenRenderer != null)
        {
            return;
        }

        var screenTransform = transform.Find("ShadowScreen");
        GameObject screenObject;
        if (screenTransform == null)
        {
            screenObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            screenObject.name = "ShadowScreen";
            screenObject.transform.SetParent(transform);
        }
        else
        {
            screenObject = screenTransform.gameObject;
        }

        screenObject.transform.localPosition = Vector3.zero;
        screenObject.transform.localRotation = Quaternion.identity;
        screenObject.transform.localScale = new Vector3(10f, 6f, 1f);

        if (!screenObject.TryGetComponent(out mScreenRenderer))
        {
            mScreenRenderer = screenObject.AddComponent<MeshRenderer>();
        }
    }

    private void EnsureAssets()
    {
#if UNITY_EDITOR
        EnsureMaterialAsset(ScreenMaterialPath, ScreenShaderName, ref mScreenMaterial, true);
        EnsureMaterialAsset(ProjectorMaterialPath, ProjectorShaderName, ref mProjectorMaterial, false);
        EnsureRenderTextureAsset();
        EnsureMaterialAsset(PuppetProjectorMaterialPath, PuppetProjectorShaderName, ref mPuppetProjectorMaterial, false);
        EnsurePuppetRenderTextureAsset();
#endif
        if (mShadowTexture == null)
        {
            mShadowTexture = CreateRuntimeRenderTexture();
        }

        if (mEnableNearTranslucent)
        {
            EnsureRuntimePuppetAssets();
        }
        else
        {
            mPuppetTexture = null;
            mPuppetProjectorMaterial = null;
        }

        if (mScreenRenderer != null && mScreenMaterial != null)
        {
            mScreenRenderer.sharedMaterial = mScreenMaterial;
        }
    }

#if UNITY_EDITOR
    private void EnsureMaterialAsset(string assetPath, string shaderName, ref Material material, bool copyOld)
    {
        if (material == null)
        {
            material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        }

        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            return;
        }

        if (material == null)
        {
            material = new Material(shader);
            if (copyOld)
            {
                CopyFromLegacyMaterial(material);
            }
            AssetDatabase.CreateAsset(material, assetPath);
            AssetDatabase.SaveAssets();
        }
        else if (material.shader == null || material.shader.name != shaderName)
        {
            material.shader = shader;
            if (copyOld)
            {
                CopyFromLegacyMaterial(material);
            }
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
        }
    }

    private void CopyFromLegacyMaterial(Material target)
    {
        var legacy = AssetDatabase.LoadAssetAtPath<Material>("Assets/Arts/Prefabs/ShadowTheatre/ShadowScreen.mat");
        if (legacy == null)
        {
            return;
        }

        var mainTex = legacy.GetTexture("_MainTex");
        if (mainTex != null)
        {
            target.SetTexture("_MainTex", mainTex);
        }

        var normalTex = legacy.GetTexture("_NormalMap");
        if (normalTex != null)
        {
            target.SetTexture("_NormalMap", normalTex);
        }

        var dirtTex = legacy.GetTexture("_DirtTex");
        if (dirtTex != null)
        {
            target.SetTexture("_DirtTex", dirtTex);
        }

        if (legacy.HasProperty("_LightColor"))
        {
            target.SetColor("_LightColor", legacy.GetColor("_LightColor"));
        }

        target.SetColor("_ShadowTint", mShadowTint);
        target.SetFloat("_ShadowStrength", mShadowStrength);
    }

    private void EnsureRenderTextureAsset()
    {
        if (mShadowTexture == null)
        {
            mShadowTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTexturePath);
        }

        if (mShadowTexture == null)
        {
            mShadowTexture = CreateRenderTextureAsset();
            return;
        }

        bool needsUpdate = false;
        if (mShadowTexture.width != mRenderTextureSize || mShadowTexture.height != mRenderTextureSize)
        {
            mShadowTexture.width = mRenderTextureSize;
            mShadowTexture.height = mRenderTextureSize;
            needsUpdate = true;
        }

        if (mShadowTexture.format != RenderTextureFormat.ARGBHalf)
        {
            mShadowTexture.format = RenderTextureFormat.ARGBHalf;
            needsUpdate = true;
        }

        if (needsUpdate)
        {
            EditorUtility.SetDirty(mShadowTexture);
            AssetDatabase.SaveAssets();
        }
    }

    private RenderTexture CreateRenderTextureAsset()
    {
        var rt = new RenderTexture(mRenderTextureSize, mRenderTextureSize, 0, RenderTextureFormat.ARGBHalf,
            RenderTextureReadWrite.Linear)
        {
            name = "RT_ShadowTry3",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            useMipMap = false,
            autoGenerateMips = false
        };

        rt.Create();
        AssetDatabase.CreateAsset(rt, RenderTexturePath);
        AssetDatabase.SaveAssets();
        return rt;
    }
#endif

    private RenderTexture CreateRuntimeRenderTexture()
    {
        int size = Mathf.Max(256, mRenderTextureSize);
        var rt = new RenderTexture(size, size, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
        {
            name = "RT_ShadowTry3_Runtime",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            useMipMap = false,
            autoGenerateMips = false
        };
        rt.Create();
        return rt;
    }

#if UNITY_EDITOR
    private void EnsurePuppetRenderTextureAsset()
    {
        if (mPuppetTexture == null)
        {
            mPuppetTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(PuppetRenderTexturePath);
        }

        if (mPuppetTexture == null)
        {
            mPuppetTexture = CreatePuppetRenderTextureAsset();
            return;
        }

        bool needsUpdate = false;
        if (mPuppetTexture.width != mRenderTextureSize || mPuppetTexture.height != mRenderTextureSize)
        {
            mPuppetTexture.width = mRenderTextureSize;
            mPuppetTexture.height = mRenderTextureSize;
            needsUpdate = true;
        }

        if (mPuppetTexture.format != RenderTextureFormat.ARGB32)
        {
            mPuppetTexture.format = RenderTextureFormat.ARGB32;
            needsUpdate = true;
        }

        if (needsUpdate)
        {
            EditorUtility.SetDirty(mPuppetTexture);
            AssetDatabase.SaveAssets();
        }
    }

    private RenderTexture CreatePuppetRenderTextureAsset()
    {
        var rt = new RenderTexture(mRenderTextureSize, mRenderTextureSize, 0, RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Linear)
        {
            name = "RT_PuppetTry3",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            useMipMap = false,
            autoGenerateMips = false
        };

        rt.Create();
        AssetDatabase.CreateAsset(rt, PuppetRenderTexturePath);
        AssetDatabase.SaveAssets();
        return rt;
    }
#endif

    private void EnsureRuntimePuppetAssets()
    {
        if (mPuppetTexture == null)
        {
            mPuppetTexture = CreateRuntimePuppetRenderTexture();
        }

        if (mPuppetProjectorMaterial == null)
        {
            Shader shader = Shader.Find(PuppetProjectorShaderName);
            if (shader != null)
            {
                mPuppetProjectorMaterial = new Material(shader);
            }
        }

        if (mPuppetProjectorMaterial != null)
        {
            mPuppetProjectorMaterial.SetFloat(AlphaBoostId, mPuppetAlphaBoost);
        }
    }

    private RenderTexture CreateRuntimePuppetRenderTexture()
    {
        int size = Mathf.Max(256, mRenderTextureSize);
        var rt = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
        {
            name = "RT_PuppetTry3_Runtime",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            useMipMap = false,
            autoGenerateMips = false
        };
        rt.Create();
        return rt;
    }

    private void EnsurePuppets()
    {
        if (mAutoCollectPuppets)
        {
            CollectPuppets();
        }

        for (int i = mPuppetMeshes.Count - 1; i >= 0; i--)
        {
            if (mPuppetMeshes[i].Renderer == null || !mPuppets.Contains(mPuppetMeshes[i].Renderer))
            {
                DestroyMesh(mPuppetMeshes[i]);
                mPuppetMeshes.RemoveAt(i);
            }
        }

        for (int i = 0; i < mPuppets.Count; i++)
        {
            var puppet = mPuppets[i];
            if (puppet == null)
            {
                continue;
            }

            if (mApplyPuppetLayer)
            {
                int layer = LayerMask.NameToLayer(mPuppetLayerName);
                if (layer >= 0)
                {
                    puppet.gameObject.layer = layer;
                }
            }

            if (!ContainsRenderer(puppet))
            {
                mPuppetMeshes.Add(new PuppetMesh(puppet));
            }
        }
    }

    private bool ContainsRenderer(SpriteRenderer renderer)
    {
        for (int i = 0; i < mPuppetMeshes.Count; i++)
        {
            if (mPuppetMeshes[i].Renderer == renderer)
            {
                return true;
            }
        }
        return false;
    }

    private void DestroyMesh(PuppetMesh puppetMesh)
    {
        if (puppetMesh.Mesh == null)
        {
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            DestroyImmediate(puppetMesh.Mesh);
            return;
        }
#endif
        Destroy(puppetMesh.Mesh);
    }

    private void CollectPuppets()
    {
        mPuppets.Clear();
        int layer = LayerMask.NameToLayer(mPuppetLayerName);
        bool filterByLayer = layer >= 0;

        var sprites = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        for (int i = 0; i < sprites.Length; i++)
        {
            var sprite = sprites[i];
            if (sprite == null || sprite.sprite == null)
            {
                continue;
            }

            if (filterByLayer && sprite.gameObject.layer != layer)
            {
                continue;
            }

            mPuppets.Add(sprite);
        }
    }

    private void EnsureMainCameraMask()
    {
        var mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        int layer = LayerMask.NameToLayer(mPuppetLayerName);
        if (layer < 0)
        {
            return;
        }

        mainCamera.cullingMask &= ~(1 << layer);
    }

    private void UpdateProjection()
    {
        if (mLightTransform == null || mScreenRenderer == null || mProjectorMaterial == null || mShadowTexture == null)
        {
            ShadowProjectorContext.Clear();
            return;
        }

        mDrawItems.Clear();

        var screenTransform = mScreenRenderer.transform;
        Vector3 screenPos = screenTransform.position;
        Vector3 screenNormal = screenTransform.forward;
        float screenWorldWidth = Mathf.Max(0.001f, mScreenRenderer.bounds.size.x);
        float pixelsPerWorld = mShadowTexture.width / screenWorldWidth;

        Vector2 halfExtents = GetScreenHalfExtents();
        Matrix4x4 viewMatrix = screenTransform.worldToLocalMatrix;
        Matrix4x4 projectionMatrix = Matrix4x4.Ortho(-halfExtents.x, halfExtents.x, -halfExtents.y, halfExtents.y, -1f, 1f);

        Plane screenPlane = new Plane(screenNormal, screenPos);
        Vector3 lightPos = mLightTransform.position;

        for (int i = 0; i < mPuppetMeshes.Count; i++)
        {
            var puppetMesh = mPuppetMeshes[i];
            if (!puppetMesh.IsValid)
            {
                continue;
            }

            Vector3 puppetCenter = puppetMesh.Renderer.bounds.center;
            float blurPixels = ComputeBlurPixels(puppetCenter, screenPlane, lightPos, pixelsPerWorld);
            float depthBlend = ComputeShadowBlendFactor(Mathf.Abs(screenPlane.GetDistanceToPoint(puppetCenter)));
            if (!UpdatePuppetMesh(puppetMesh, lightPos, screenPlane, blurPixels, depthBlend, pixelsPerWorld))
            {
                continue;
            }

            mDrawItems.Add(puppetMesh.DrawItem);
        }

        RenderTexture puppetTexture = null;
        Material puppetMaterial = null;
        if (mEnableNearTranslucent && mPuppetTexture != null && mPuppetProjectorMaterial != null)
        {
            puppetTexture = mPuppetTexture;
            puppetMaterial = mPuppetProjectorMaterial;
        }

        ShadowProjectorContext.UpdateContext(mShadowTexture, mProjectorMaterial, puppetTexture, puppetMaterial,
            viewMatrix, projectionMatrix, mDrawItems);

        if (mScreenMaterial != null)
        {
            mScreenMaterial.SetTexture(ShadowTexId, mShadowTexture);
            if (puppetTexture != null)
            {
                mScreenMaterial.SetTexture(PuppetTexId, puppetTexture);
                mScreenMaterial.SetFloat(PuppetStrengthId, mPuppetStrength);
                mScreenMaterial.SetFloat(ShadowBlendId, mShadowBlendScale);
            }
            else
            {
                mScreenMaterial.SetTexture(PuppetTexId, null);
                mScreenMaterial.SetFloat(PuppetStrengthId, 0f);
                mScreenMaterial.SetFloat(ShadowBlendId, 1f);
            }
            mScreenMaterial.SetColor(ShadowTintId, mShadowTint);
            mScreenMaterial.SetFloat(ShadowStrengthId, mShadowStrength);
            UpdateLightCenter(lightPos);
        }
    }

    private float ComputeShadowBlendFactor(float distanceToScreen)
    {
        if (!mEnableNearTranslucent)
        {
            return 1f;
        }

        float denom = Mathf.Max(0.0001f, mFarDistance - mNearDistance);
        float t = (distanceToScreen - mNearDistance) / denom;
        t = Mathf.Clamp01(t);
        return Mathf.SmoothStep(0f, 1f, t);
    }

    private Vector2 GetScreenHalfExtents()
    {
        MeshFilter meshFilter;
        if (mScreenRenderer != null && mScreenRenderer.TryGetComponent(out meshFilter) && meshFilter.sharedMesh != null)
        {
            var bounds = meshFilter.sharedMesh.bounds;
            return new Vector2(Mathf.Max(0.001f, bounds.extents.x), Mathf.Max(0.001f, bounds.extents.y));
        }

        return new Vector2(0.5f, 0.5f);
    }

    private void UpdateLightCenter(Vector3 lightPos)
    {
        MeshFilter meshFilter;
        if (mScreenRenderer == null || !mScreenRenderer.TryGetComponent(out meshFilter) || meshFilter.sharedMesh == null)
        {
            return;
        }

        var bounds = meshFilter.sharedMesh.bounds;
        Vector3 local = mScreenRenderer.transform.InverseTransformPoint(lightPos);
        float u = Mathf.InverseLerp(bounds.min.x, bounds.max.x, local.x);
        float v = Mathf.InverseLerp(bounds.min.y, bounds.max.y, local.y);
        mScreenMaterial.SetVector(LightCenterId, new Vector4(u, v, 0f, 0f));
    }

    private float ComputeBlurPixels(Vector3 puppetPos, Plane screenPlane, Vector3 lightPos, float pixelsPerWorld)
    {
        float dPs = Mathf.Abs(screenPlane.GetDistanceToPoint(puppetPos));
        float dLp = Vector3.Distance(lightPos, puppetPos);
        if (dLp <= 0.0001f)
        {
            return 0f;
        }

        float blurWorld = mLightRadius * (dPs / dLp);
        float blurPixels = blurWorld * pixelsPerWorld * mBlurScale;
        if (mEnableDepthBlur)
        {
            float depthT = ComputeDepthBlurFactor(dPs);
            blurPixels *= depthT;
        }
        return Mathf.Clamp(blurPixels, 0f, mMaxBlurPixels);
    }

    private float ComputeDepthBlurFactor(float distanceToScreen)
    {
        float denom = Mathf.Max(0.0001f, mDepthBlurFar - mDepthBlurNear);
        float t = (distanceToScreen - mDepthBlurNear) / denom;
        t = Mathf.Clamp01(t);
        return Mathf.SmoothStep(0f, 1f, t);
    }

    private bool UpdatePuppetMesh(PuppetMesh puppetMesh, Vector3 lightPos, Plane screenPlane,
        float blurPixels, float depthBlend, float pixelsPerWorld)
    {
        if (!puppetMesh.TryEnsureMesh())
        {
            return false;
        }

        bool allProjected = true;
        var rendererTransform = puppetMesh.Renderer.transform;
        for (int i = 0; i < puppetMesh.SpriteVertices.Length; i++)
        {
            Vector3 local = new Vector3(puppetMesh.SpriteVertices[i].x, puppetMesh.SpriteVertices[i].y, 0f);
            Vector3 world = rendererTransform.TransformPoint(local);
            Vector3 projected;
            if (!TryProjectPoint(world, lightPos, screenPlane, out projected))
            {
                allProjected = false;
                projected = screenPlane.ClosestPointOnPlane(world);
            }

            float blurValue = blurPixels;
            float blendValue = depthBlend;
            if (mPerVertexDepthBlur)
            {
                blurValue = ComputeBlurPixels(world, screenPlane, lightPos, pixelsPerWorld);
                float distanceToScreen = Mathf.Abs(screenPlane.GetDistanceToPoint(world));
                blendValue = ComputeShadowBlendFactor(distanceToScreen);
            }
            puppetMesh.Vertices[i] = projected;
            puppetMesh.Colors[i] = new Color(1f, blurValue, blendValue, 1f);
        }

        if (!allProjected)
        {
            return false;
        }

        puppetMesh.ApplyMeshData();
        puppetMesh.UpdatePropertyBlock();
        return true;
    }

    private bool TryProjectPoint(Vector3 worldPoint, Vector3 lightPos, Plane screenPlane, out Vector3 projected)
    {
        Vector3 dir = worldPoint - lightPos;
        float denom = Vector3.Dot(screenPlane.normal, dir);
        if (Mathf.Abs(denom) < 0.0001f)
        {
            projected = worldPoint;
            return false;
        }

        float t = Vector3.Dot(screenPlane.normal, (screenPlane.normal * -screenPlane.distance) - lightPos) / denom;
        if (t <= 0f)
        {
            projected = worldPoint;
            return false;
        }

        projected = lightPos + dir * t;
        return true;
    }

    private class PuppetMesh
    {
        public readonly SpriteRenderer Renderer;
        public Sprite Sprite;
        public Mesh Mesh;
        public Vector2[] SpriteVertices;
        public Vector3[] Vertices;
        public Vector2[] UVs;
        public int[] Triangles;
        public Color[] Colors;
        public MaterialPropertyBlock PropertyBlock;

        public PuppetMesh(SpriteRenderer renderer)
        {
            Renderer = renderer;
        }

        public bool IsValid => Renderer != null && Renderer.sprite != null;

        public ShadowProjectorDrawItem DrawItem => new ShadowProjectorDrawItem
        {
            Mesh = Mesh,
            PropertyBlock = PropertyBlock
        };

        public bool TryEnsureMesh()
        {
            if (Renderer == null || Renderer.sprite == null)
            {
                return false;
            }

            if (Sprite != Renderer.sprite || Mesh == null)
            {
                Sprite = Renderer.sprite;
                SpriteVertices = Sprite.vertices;
                Vertices = new Vector3[SpriteVertices.Length];
                UVs = Sprite.uv;
                Triangles = ConvertTriangles(Sprite.triangles);
                Colors = new Color[SpriteVertices.Length];

                if (Mesh == null)
                {
                    Mesh = new Mesh
                    {
                        name = $"ShadowProj_{Renderer.name}"
                    };
                }
                else
                {
                    Mesh.Clear();
                }

                Mesh.MarkDynamic();
                Mesh.vertices = Vertices;
                Mesh.uv = UVs;
                Mesh.triangles = Triangles;
                Mesh.colors = Colors;
            }

            return true;
        }

        public void ApplyMeshData()
        {
            if (Mesh == null)
            {
                return;
            }

            Mesh.vertices = Vertices;
            Mesh.colors = Colors;
        }

        public void UpdatePropertyBlock()
        {
            if (PropertyBlock == null)
            {
                PropertyBlock = new MaterialPropertyBlock();
            }

            PropertyBlock.SetTexture("_MainTex", Renderer.sprite.texture);
            PropertyBlock.SetColor("_RendererColor", Renderer.color);
        }

        private static int[] ConvertTriangles(ushort[] triangles)
        {
            int[] result = new int[triangles.Length];
            for (int i = 0; i < triangles.Length; i++)
            {
                result[i] = triangles[i];
            }
            return result;
        }
    }
}
