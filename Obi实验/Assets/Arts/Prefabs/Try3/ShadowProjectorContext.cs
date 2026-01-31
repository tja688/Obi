using System.Collections.Generic;
using UnityEngine;

public struct ShadowProjectorDrawItem
{
    public Mesh Mesh;
    public MaterialPropertyBlock PropertyBlock;
}

public static class ShadowProjectorContext
{
    private static readonly List<ShadowProjectorDrawItem> DrawItemsInternal = new List<ShadowProjectorDrawItem>();

    public static IReadOnlyList<ShadowProjectorDrawItem> DrawItems => DrawItemsInternal;
    public static RenderTexture ShadowTexture { get; private set; }
    public static Material ProjectorMaterial { get; private set; }
    public static RenderTexture PuppetTexture { get; private set; }
    public static Material PuppetProjectorMaterial { get; private set; }
    public static Matrix4x4 ViewMatrix { get; private set; }
    public static Matrix4x4 ProjectionMatrix { get; private set; }

    public static bool IsReady => ShadowTexture != null && ProjectorMaterial != null;
    public static bool HasPuppetProjection => PuppetTexture != null && PuppetProjectorMaterial != null;

    public static void UpdateContext(RenderTexture shadowTexture, Material projectorMaterial,
        RenderTexture puppetTexture, Material puppetProjectorMaterial,
        Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix, List<ShadowProjectorDrawItem> items)
    {
        ShadowTexture = shadowTexture;
        ProjectorMaterial = projectorMaterial;
        PuppetTexture = puppetTexture;
        PuppetProjectorMaterial = puppetProjectorMaterial;
        ViewMatrix = viewMatrix;
        ProjectionMatrix = projectionMatrix;

        DrawItemsInternal.Clear();
        if (items != null)
        {
            DrawItemsInternal.AddRange(items);
        }
    }

    public static void Clear()
    {
        ShadowTexture = null;
        ProjectorMaterial = null;
        PuppetTexture = null;
        PuppetProjectorMaterial = null;
        DrawItemsInternal.Clear();
    }
}
