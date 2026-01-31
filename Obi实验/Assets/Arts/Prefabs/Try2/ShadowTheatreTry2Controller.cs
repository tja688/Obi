using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class ShadowTheatreTry2Controller : MonoBehaviour
{
    [Header("Scene References")]
    public Camera shadowCam;
    public Light shadowLight;
    public Renderer screenRenderer;
    public Transform puppet;

    [Header("Screen")]
    public Vector2 screenSize = new Vector2(10f, 6f);

    [Header("Shadow Look")]
    public float shadowSoftnessScale = 0.15f;
    public Color shadowColor = new Color(0.2f, 0.2f, 0.2f, 1f);

    [Header("Render Texture")]
    public int renderTextureSize = 2048;

    [Header("Layer")]
    public string puppetLayerName = "PuppetLayer";

    private Material _runtimeMaterial;
    private RenderTexture _runtimeRT;

    private static readonly int ShadowTexId = Shader.PropertyToID("_ShadowTex");
    private static readonly int ShadowVPId = Shader.PropertyToID("_ShadowVP");
    private static readonly int ShadowSoftnessId = Shader.PropertyToID("_ShadowSoftness");
    private static readonly int ShadowColorId = Shader.PropertyToID("_ShadowColor");

    private const string Try2Path = "Assets/Arts/Prefabs/Try2";
    private const string MaterialPath = Try2Path + "/ShadowScreenProjection.mat";
    private const string RenderTexturePath = Try2Path + "/RT_ShadowProjection_Try2.renderTexture";

    private void OnEnable()
    {
        EnsureSetup();
        UpdateProjection();
    }

    private void Update()
    {
        EnsureSetup();
        UpdateProjection();
    }

    private void OnDisable()
    {
        if (_runtimeRT != null)
        {
            _runtimeRT.Release();
            _runtimeRT = null;
        }
    }

    private void EnsureSetup()
    {
        EnsureLight();
        EnsureCamera();
        EnsureScreen();
        EnsurePuppet();
        EnsureMaterialAndRT();
        EnsureMainCameraMask();
    }

    private void EnsureLight()
    {
        if (shadowLight != null)
        {
            return;
        }

        var lightTransform = transform.Find("ShadowLight");
        if (lightTransform == null)
        {
            var lightObject = new GameObject("ShadowLight");
            lightObject.transform.SetParent(transform);
            lightObject.transform.localPosition = new Vector3(0f, 0f, 5f);
            lightObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            shadowLight = lightObject.AddComponent<Light>();
        }
        else
        {
            shadowLight = lightTransform.GetComponent<Light>();
            if (shadowLight == null)
            {
                shadowLight = lightTransform.gameObject.AddComponent<Light>();
            }
        }

        shadowLight.type = LightType.Spot;
        shadowLight.color = new Color(1f, 0.9f, 0.75f, 1f);
        shadowLight.intensity = 3f;
        shadowLight.range = 20f;
        shadowLight.spotAngle = 35f;
    }

    private void EnsureCamera()
    {
        if (shadowCam == null)
        {
            var camTransform = transform.Find("ShadowCam");
            if (camTransform == null)
            {
                var camObject = new GameObject("ShadowCam");
                camObject.transform.SetParent(transform);
                camObject.transform.localPosition = new Vector3(0f, 0f, 5f);
                camObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                shadowCam = camObject.AddComponent<Camera>();
            }
            else
            {
                shadowCam = camTransform.GetComponent<Camera>();
                if (shadowCam == null)
                {
                    shadowCam = camTransform.gameObject.AddComponent<Camera>();
                }
            }
        }

        if (shadowLight != null)
        {
            shadowCam.transform.position = shadowLight.transform.position;
            shadowCam.transform.rotation = shadowLight.transform.rotation;
            shadowCam.fieldOfView = shadowLight.spotAngle;
        }

        shadowCam.orthographic = false;
        shadowCam.nearClipPlane = 0.1f;
        shadowCam.farClipPlane = 50f;
        shadowCam.clearFlags = CameraClearFlags.SolidColor;
        shadowCam.backgroundColor = Color.white;
        shadowCam.aspect = 1f;

        int layer = LayerMask.NameToLayer(puppetLayerName);
        if (layer >= 0)
        {
            shadowCam.cullingMask = 1 << layer;
        }
        else
        {
            shadowCam.cullingMask = 0;
        }
    }

    private void EnsureScreen()
    {
        if (screenRenderer != null)
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
        screenObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        screenObject.transform.localScale = new Vector3(screenSize.x, screenSize.y, 1f);
        screenRenderer = screenObject.GetComponent<Renderer>();
    }

    private void EnsurePuppet()
    {
        if (puppet != null)
        {
            return;
        }

        var puppetObject = GameObject.Find("Puppet");
        if (puppetObject == null)
        {
            puppetObject = new GameObject("Puppet");
            puppetObject.transform.SetParent(transform);
            puppetObject.transform.localPosition = new Vector3(0f, 0f, 2f);
            puppetObject.transform.localRotation = Quaternion.identity;
            puppetObject.transform.localScale = new Vector3(1.5f, 2.2f, 1f);
        }

        puppet = puppetObject.transform;

        var spriteRenderer = puppetObject.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = puppetObject.AddComponent<SpriteRenderer>();
        }

        if (spriteRenderer.sprite == null)
        {
            spriteRenderer.sprite = CreateFallbackSprite();
        }
        spriteRenderer.color = Color.black;

        int layer = LayerMask.NameToLayer(puppetLayerName);
        if (layer >= 0)
        {
            puppetObject.layer = layer;
        }
    }

    private Sprite CreateFallbackSprite()
    {
        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
    }

    private void EnsureMaterialAndRT()
    {
        if (screenRenderer == null)
        {
            return;
        }

        Material material = screenRenderer.sharedMaterial;
        if (material == null || material.shader == null || material.shader.name != "Custom/ShadowScreenProjection")
        {
#if UNITY_EDITOR
            material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                var shader = Shader.Find("Custom/ShadowScreenProjection");
                if (shader != null)
                {
                    material = new Material(shader);
                    AssetDatabase.CreateAsset(material, MaterialPath);
                    AssetDatabase.SaveAssets();
                }
            }
#endif
            if (material == null)
            {
                var shader = Shader.Find("Custom/ShadowScreenProjection");
                if (shader != null)
                {
                    _runtimeMaterial = new Material(shader);
                    material = _runtimeMaterial;
                }
            }

            if (material != null)
            {
                screenRenderer.sharedMaterial = material;
            }
        }

        var rt = EnsureRenderTexture();
        if (shadowCam != null && rt != null)
        {
            shadowCam.targetTexture = rt;
        }
        if (material != null && rt != null)
        {
            material.SetTexture(ShadowTexId, rt);
        }
    }

    private RenderTexture EnsureRenderTexture()
    {
#if UNITY_EDITOR
        var existing = AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTexturePath);
        if (existing != null)
        {
            return existing;
        }

        var rt = new RenderTexture(renderTextureSize, renderTextureSize, 0, RenderTextureFormat.ARGB32)
        {
            name = "RT_ShadowProjection_Try2",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            useMipMap = false,
            autoGenerateMips = false
        };
        rt.Create();
        AssetDatabase.CreateAsset(rt, RenderTexturePath);
        AssetDatabase.SaveAssets();
        return rt;
#else
        if (_runtimeRT == null || _runtimeRT.width != renderTextureSize)
        {
            _runtimeRT = new RenderTexture(renderTextureSize, renderTextureSize, 0, RenderTextureFormat.ARGB32)
            {
                name = "RT_ShadowProjection_Try2",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
                autoGenerateMips = false
            };
            _runtimeRT.Create();
        }
        return _runtimeRT;
#endif
    }

    private void EnsureMainCameraMask()
    {
        var mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        int layer = LayerMask.NameToLayer(puppetLayerName);
        if (layer < 0)
        {
            return;
        }

        mainCamera.cullingMask &= ~(1 << layer);
    }

    private void UpdateProjection()
    {
        if (shadowCam == null || screenRenderer == null)
        {
            return;
        }

        var material = screenRenderer.sharedMaterial;
        if (material == null)
        {
            return;
        }

        var projection = GL.GetGPUProjectionMatrix(shadowCam.projectionMatrix, true);
        var view = shadowCam.worldToCameraMatrix;
        material.SetMatrix(ShadowVPId, projection * view);
        material.SetColor(ShadowColorId, shadowColor);

        float softness = 0f;
        if (shadowLight != null && puppet != null)
        {
            var lightPos = shadowLight.transform.position;
            var dir = puppet.position - lightPos;
            if (dir.sqrMagnitude > 0.0001f)
            {
                var dirN = dir.normalized;
                var plane = new Plane(screenRenderer.transform.forward, screenRenderer.transform.position);
                if (plane.Raycast(new Ray(lightPos, dirN), out float tScreen))
                {
                    float tPuppet = Vector3.Dot(puppet.position - lightPos, dirN);
                    if (tPuppet > 0f)
                    {
                        softness = Mathf.Max(0f, (tScreen - tPuppet) / tPuppet) * shadowSoftnessScale;
                    }
                }
            }
        }
        material.SetFloat(ShadowSoftnessId, softness);
    }
}
