using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NeonPrism
{
    /// <summary>
    /// Туман войны: Point Light 2D на танке с радиусом = половина большего габарита корпуса + 5–10 px.
    /// Всё за кругом остаётся в тени Global Light 2D (она должна быть очень тёмной).
    /// </summary>
    [RequireComponent(typeof(Light2D))]
    public class FogOfWarLight : MonoBehaviour
    {
        [Tooltip("Коллайдер корпуса — из него берутся габариты.")]
        [SerializeField] private BoxCollider2D hullCollider;
        [Range(5f, 30f)]
        [SerializeField] private float marginPixels = 8f;
        [SerializeField] private float intensity = 1f;

        private void Awake()
        {
            Light2D light2D = GetComponent<Light2D>();

            if (hullCollider == null) hullCollider = GetComponentInParent<BoxCollider2D>();
            if (hullCollider == null)
            {
                Debug.LogError("FogOfWarLight: не найден BoxCollider2D корпуса.", this);
                return;
            }

            Vector3 scale = hullCollider.transform.lossyScale;
            float halfWidth = hullCollider.size.x * Mathf.Abs(scale.x) * 0.5f;
            float halfLength = hullCollider.size.y * Mathf.Abs(scale.y) * 0.5f;

            light2D.pointLightInnerRadius = 0f;
            light2D.pointLightOuterRadius = Mathf.Max(halfWidth, halfLength) + PixelUnits.ToUnits(marginPixels);
            light2D.intensity = intensity;
        }
    }
}
