using System.Collections;
using UnityEngine;

public class PuppetDriftJitter : MonoBehaviour
{
    [Header("Drift Rotation Settings")]
    [Tooltip("Max random rotation in degrees for each axis.")]
    [SerializeField] private float maxAngleX = 3f;
    [SerializeField] private float maxAngleY = 6f;
    [SerializeField] private float maxAngleZ = 2f;

    [Tooltip("Minimum duration for one drift rotation.")]
    [SerializeField] private float minDuration = 0.2f;

    [Tooltip("Maximum duration for one drift rotation.")]
    [SerializeField] private float maxDuration = 0.4f;

    [Tooltip("Ease-out strength for rotation. Higher = faster start, slower end.")]
    [Range(1.0f, 10.0f)]
    [SerializeField] private float rotationEase = 3.0f;

    [Tooltip("Smooth time for Z correction during rotation.")]
    [Range(0.01f, 0.5f)]
    [SerializeField] private float zSmoothTime = 0.08f;

    [Tooltip("If true, keep all renderers at Z >= 0. If false, keep them at Z <= 0.")]
    [SerializeField] private bool keepOnPositiveZ = true;

    private Coroutine mActiveCoroutine;
    private Renderer[] mRenderers;

    private void Awake()
    {
        CacheRenderers();
    }

    private void OnTransformChildrenChanged()
    {
        CacheRenderers();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            TriggerDrift();
        }
    }

    [ContextMenu("Trigger Drift")]
    public void TriggerDrift()
    {
        if (mActiveCoroutine != null)
        {
            return;
        }

        Vector3 deltaEuler = new Vector3(
            Random.Range(-maxAngleX, maxAngleX),
            Random.Range(-maxAngleY, maxAngleY),
            Random.Range(-maxAngleZ, maxAngleZ));

        float min = Mathf.Max(0.01f, Mathf.Min(minDuration, maxDuration));
        float max = Mathf.Max(min, Mathf.Max(minDuration, maxDuration));
        float duration = Random.Range(min, max);
        mActiveCoroutine = StartCoroutine(PlayDrift(deltaEuler, duration));
    }

    private IEnumerator PlayDrift(Vector3 deltaEuler, float duration)
    {
        Quaternion startRot = transform.localRotation;
        Quaternion endRot = startRot * Quaternion.Euler(deltaEuler);

        float elapsed = 0f;
        float halfDuration = Mathf.Max(duration * 0.5f, 0.01f);
        float baseZ = transform.position.z;
        float currentZ = baseZ;
        float zVelocity = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Quaternion targetRotation;

            if (t <= 0.5f)
            {
                float outT = Mathf.Clamp01(elapsed / halfDuration);
                float easedT = EaseOut(outT);
                targetRotation = Quaternion.Slerp(startRot, endRot, easedT);
            }
            else
            {
                float inElapsed = elapsed - halfDuration;
                float inT = Mathf.Clamp01(inElapsed / halfDuration);
                float easedT = EaseOut(inT);
                targetRotation = Quaternion.Slerp(endRot, startRot, easedT);
            }

            transform.localRotation = targetRotation;

            float targetZ = CalculateTargetWorldZ(baseZ);
            currentZ = Mathf.SmoothDamp(currentZ, targetZ, ref zVelocity, zSmoothTime);
            SetWorldZ(currentZ);
            yield return null;
        }

        transform.localRotation = startRot;
        SetWorldZ(CalculateTargetWorldZ(baseZ));

        mActiveCoroutine = null;
    }

    private void CacheRenderers()
    {
        mRenderers = GetComponentsInChildren<Renderer>(true);
        if (mRenderers == null || mRenderers.Length == 0)
        {
            Debug.LogWarning($"No renderers found for Z screen constraint on {name}.", this);
        }
    }

    private float EaseOut(float t)
    {
        return 1.0f - Mathf.Pow(1.0f - t, rotationEase);
    }

    private void SetWorldZ(float z)
    {
        Vector3 pos = transform.position;
        pos.z = z;
        transform.position = pos;
    }

    private float CalculateTargetWorldZ(float baseZ)
    {
        if (mRenderers == null || mRenderers.Length == 0)
        {
            return baseZ;
        }

        bool hasBounds = false;
        Bounds combinedBounds = new Bounds();
        for (int i = 0; i < mRenderers.Length; i++)
        {
            Renderer renderer = mRenderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            return baseZ;
        }

        float currentZ = transform.position.z;
        if (keepOnPositiveZ)
        {
            float minZAtBase = combinedBounds.min.z + (baseZ - currentZ);
            if (minZAtBase < 0f)
            {
                return baseZ - minZAtBase;
            }

            return baseZ;
        }

        float maxZAtBase = combinedBounds.max.z + (baseZ - currentZ);
        if (maxZAtBase > 0f)
        {
            return baseZ - maxZAtBase;
        }

        return baseZ;
    }
}
