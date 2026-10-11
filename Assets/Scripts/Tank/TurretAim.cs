using UnityEngine;

namespace NeonPrism
{
    /// <summary>
    /// Башня (дочерний объект корпуса) независимо от корпуса поворачивается к курсору.
    /// Вешается на корень Turret. Работает в LateUpdate, чтобы учесть интерполяцию корпуса и позицию камеры.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class TurretAim : MonoBehaviour
    {
        [Tooltip("Градусов/сек. 0 = мгновенное наведение.")]
        [SerializeField] private float rotateSpeed = 0f;

        private Camera cam;

        /// <summary>Мировая позиция курсора на плоскости игры.</summary>
        public Vector2 AimWorldPoint { get; private set; }

        private void Start()
        {
            cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("TurretAim: не найдена камера с тегом MainCamera.", this);
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            Vector2 screen = GameInput.MouseScreenPosition;
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            AimWorldPoint = world;

            Vector2 dir = AimWorldPoint - (Vector2)transform.position;
            if (dir.sqrMagnitude < 1e-6f) return;

            // Спрайт ствола смотрит вверх (+Y), поэтому -90°.
            float target = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            float current = transform.eulerAngles.z;
            float next = rotateSpeed <= 0f
                ? target
                : Mathf.MoveTowardsAngle(current, target, rotateSpeed * Time.deltaTime);

            transform.rotation = Quaternion.Euler(0f, 0f, next);
        }
    }
}
