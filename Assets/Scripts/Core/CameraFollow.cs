using UnityEngine;

namespace NeonPrism
{
    /// <summary>
    /// Плавное следование за танком с ограничением по границам карты.
    /// Выполняется раньше TurretAim, чтобы прицел не отставал на кадр.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float smoothTime = 0.12f;
        [SerializeField] private bool useBounds = true;
        [Tooltip("Границы карты в мировых координатах (x, y, ширина, высота).")]
        [SerializeField] private Rect worldBounds = new Rect(-20f, -20f, 40f, 40f);

        private Camera cam;
        private Vector3 velocity;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desired = new Vector3(target.position.x, target.position.y, transform.position.z);
            if (useBounds) desired = ClampToBounds(desired);

            transform.position = smoothTime > 0f
                ? Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime)
                : desired;
        }

        private Vector3 ClampToBounds(Vector3 pos)
        {
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;

            pos.x = ClampAxis(pos.x, worldBounds.xMin, worldBounds.xMax, halfW);
            pos.y = ClampAxis(pos.y, worldBounds.yMin, worldBounds.yMax, halfH);
            return pos;
        }

        // Если карта по оси меньше области видимости — центрируем камеру.
        private static float ClampAxis(float value, float min, float max, float half)
        {
            if (max - min <= half * 2f) return (min + max) * 0.5f;
            return Mathf.Clamp(value, min + half, max - half);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(worldBounds.center, worldBounds.size);
        }
#endif
    }
}
