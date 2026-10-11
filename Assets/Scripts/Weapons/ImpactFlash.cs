using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NeonPrism
{
    /// <summary>
    /// Кратковременная вспышка в точке рикошета: Point Light 2D 10x10 px с затуханием 0.1–0.15 сек.
    /// Префаб ImpactFlash = пустой объект + Light2D (тип Point) + этот скрипт.
    /// </summary>
    [RequireComponent(typeof(Light2D))]
    public class ImpactFlash : MonoBehaviour
    {
        [Tooltip("Диаметр пятна света в пикселях (10x10 px).")]
        [SerializeField] private float spotSizePixels = 10f;
        [Range(0.1f, 0.5f)]
        [SerializeField] private float duration = 0.12f;
        [SerializeField] private float startIntensity = 2f;

        private Light2D light2D;
        private float elapsed;

        private void Awake()
        {
            light2D = GetComponent<Light2D>();

            // Задаем радиусы напрямую для URP 2D
            light2D.pointLightInnerRadius = 0.5f;
            light2D.pointLightOuterRadius = 2f; // Желаемый радиус вспышки (поставь 3f, 4f или 5f)

            light2D.intensity = startIntensity;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            light2D.intensity = startIntensity * (1f - t);
        }
    }
}
