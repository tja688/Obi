using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ShadowProjectorFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public RenderPassEvent passEvent = RenderPassEvent.BeforeRenderingOpaques;
    }

    [SerializeField] private Settings settings = new Settings();

    private ShadowProjectorPass mPass;

    public override void Create()
    {
        mPass = new ShadowProjectorPass(settings.passEvent);
    }

    protected override void Dispose(bool disposing)
    {
        mPass?.Dispose();
        base.Dispose(disposing);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!ShadowProjectorContext.IsReady)
        {
            return;
        }

        if (renderingData.cameraData.renderType != CameraRenderType.Base)
        {
            return;
        }

        renderer.EnqueuePass(mPass);
    }

    private class ShadowProjectorPass : ScriptableRenderPass
    {
        private static readonly ProfilingSampler ProfilingSampler = new ProfilingSampler("ShadowProjectorPass");
        private RTHandle m_ShadowHandle;
        private RTHandle m_PuppetHandle;

        public ShadowProjectorPass(RenderPassEvent passEvent)
        {
            renderPassEvent = passEvent;
        }

        public void Dispose()
        {
            m_ShadowHandle?.Release();
            m_PuppetHandle?.Release();
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var shadowTexture = ShadowProjectorContext.ShadowTexture;
            if (shadowTexture == null)
            {
                return;
            }

            if (m_ShadowHandle == null || m_ShadowHandle.rt != shadowTexture)
            {
                m_ShadowHandle?.Release();
                m_ShadowHandle = RTHandles.Alloc(shadowTexture);
            }

            ConfigureTarget(m_ShadowHandle);
            ConfigureClear(ClearFlag.Color, Color.black);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!ShadowProjectorContext.IsReady)
            {
                return;
            }

            var shadowTexture = ShadowProjectorContext.ShadowTexture;
            var projectorMaterial = ShadowProjectorContext.ProjectorMaterial;
            if (shadowTexture == null || projectorMaterial == null)
            {
                return;
            }

            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, ProfilingSampler))
            {
                cmd.SetRenderTarget(m_ShadowHandle);
                cmd.ClearRenderTarget(true, true, Color.black);
                cmd.SetViewProjectionMatrices(ShadowProjectorContext.ViewMatrix, ShadowProjectorContext.ProjectionMatrix);
                cmd.SetViewport(new Rect(0f, 0f, shadowTexture.width, shadowTexture.height));

                var drawItems = ShadowProjectorContext.DrawItems;
                for (int i = 0; i < drawItems.Count; i++)
                {
                    var mesh = drawItems[i].Mesh;
                    if (mesh == null)
                    {
                        continue;
                    }

                    cmd.DrawMesh(mesh, Matrix4x4.identity, projectorMaterial, 0, 0, drawItems[i].PropertyBlock);
                }

                if (ShadowProjectorContext.HasPuppetProjection)
                {
                    var puppetTexture = ShadowProjectorContext.PuppetTexture;
                    var puppetMaterial = ShadowProjectorContext.PuppetProjectorMaterial;
                    if (puppetTexture != null && puppetMaterial != null)
                    {
                        if (m_PuppetHandle == null || m_PuppetHandle.rt != puppetTexture)
                        {
                            m_PuppetHandle?.Release();
                            m_PuppetHandle = RTHandles.Alloc(puppetTexture);
                        }

                        cmd.SetRenderTarget(m_PuppetHandle);
                        cmd.ClearRenderTarget(true, true, Color.white);
                        cmd.SetViewport(new Rect(0f, 0f, puppetTexture.width, puppetTexture.height));

                        for (int i = 0; i < drawItems.Count; i++)
                        {
                            var mesh = drawItems[i].Mesh;
                            if (mesh == null)
                            {
                                continue;
                            }

                            cmd.DrawMesh(mesh, Matrix4x4.identity, puppetMaterial, 0, 0, drawItems[i].PropertyBlock);
                        }
                    }
                }

                var camera = renderingData.cameraData.camera;
                cmd.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
