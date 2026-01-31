using System.Collections;
using UnityEngine;

public class CharacterBlurTest : MonoBehaviour
{
    [Header("General Settings")]
    [Tooltip("The starting Z value (Rest position)")]
    [SerializeField] private float minZ = 0f;

    [Tooltip("The peak Z value (Blur position)")]
    [SerializeField] private float maxZ = 30f;

    [Tooltip("Controls the curve sharpness. Higher = more extreme acceleration/deceleration.")]
    [Range(1.0f, 10.0f)]
    [SerializeField] private float intensity = 3.0f;

    [Header("Blur (Key 1) Settings")]
    [Tooltip("Time taken for one leg of the journey (0->30 or 30->0). Total time is double this.")]
    [SerializeField] private float oneWayDuration = 0.5f;

    [Header("Rotation (Key 2/3) Settings")]
    [Tooltip("Total time for the rotation (180 degrees)")]
    [SerializeField] private float flipDuration = 1.0f;

    [Tooltip("Overshoot strength for the rotation ease (0 = no overshoot).")]
    [Range(0f, 5f)]
    [SerializeField] private float rotationOvershoot = 1.5f;

    [Header("Movement Settings")]
    [Tooltip("Movement speed for WASD")]
    [SerializeField] private float moveSpeed = 5f;

    private Coroutine mActiveCoroutine;
    void Update()
    {
        // Press 1 to trigger the simple blur effect
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            if (mActiveCoroutine == null)
            {
                mActiveCoroutine = StartCoroutine(PlayBlurEffect());
            }
        }

        // Press 2 to rotate left
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            if (mActiveCoroutine == null)
            {
                mActiveCoroutine = StartCoroutine(PlayRotateEffect(-1f));
            }
        }

        // Press 3 to rotate right
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            if (mActiveCoroutine == null)
            {
                mActiveCoroutine = StartCoroutine(PlayRotateEffect(1f));
            }
        }

        // WASD Movement
        HandleMovement();
    }

    private void HandleMovement()
    {
        float moveX = 0f;
        float moveY = 0f;

        if (Input.GetKey(KeyCode.W)) moveY += 1f;
        if (Input.GetKey(KeyCode.S)) moveY -= 1f;
        if (Input.GetKey(KeyCode.A)) moveX -= 1f;
        if (Input.GetKey(KeyCode.D)) moveX += 1f;

        if (moveX != 0 || moveY != 0)
        {
            Vector3 movement = new Vector3(moveX, moveY, 0).normalized * moveSpeed * Time.deltaTime;
            transform.localPosition += movement;
        }
    }

    private IEnumerator PlayBlurEffect()
    {
        // Phase 1: Go to Max (Slow then Fast -> EaseIn)
        float elapsed = 0f;
        while (elapsed < oneWayDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / oneWayDuration);

            // EaseIn: t^intensity

            float curveValue = Mathf.Pow(t, intensity);


            float z = Mathf.Lerp(minZ, maxZ, curveValue);
            SetLocalZ(z);


            yield return null;
        }
        SetLocalZ(maxZ);

        // Phase 2: Return to Min (Fast then Slow -> EaseOut)
        elapsed = 0f;
        while (elapsed < oneWayDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / oneWayDuration);

            // EaseOut: 1 - (1-t)^intensity

            float curveValue = 1.0f - Mathf.Pow(1.0f - t, intensity);

            // Lerp from Max back to Min

            float z = Mathf.Lerp(maxZ, minZ, curveValue);
            SetLocalZ(z);


            yield return null;
        }
        SetLocalZ(minZ);


        mActiveCoroutine = null;
    }

    private IEnumerator PlayRotateEffect(float direction)
    {
        Quaternion startRot = transform.localRotation;
        Quaternion endRot = startRot * Quaternion.Euler(0f, 180f * Mathf.Sign(direction), 0f);

        float elapsed = 0f;
        while (elapsed < flipDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flipDuration);

            float easedT = EaseOutBack(t, rotationOvershoot);
            transform.localRotation = Quaternion.SlerpUnclamped(startRot, endRot, easedT);
            yield return null;
        }

        // Ensure final state is exact
        transform.localRotation = endRot;


        mActiveCoroutine = null;
    }

    private void SetLocalZ(float z)
    {
        Vector3 pos = transform.localPosition;
        pos.z = z;
        transform.localPosition = pos;
    }

    private float EaseOutBack(float t, float overshoot)
    {
        float s = Mathf.Max(0f, overshoot);
        float tMinus = t - 1f;
        return 1f + (s + 1f) * tMinus * tMinus * tMinus + s * tMinus * tMinus;
    }
}
