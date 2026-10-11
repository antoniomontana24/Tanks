using UnityEngine;

namespace NeonPrism
{
    /// <summary>
    /// Стрельба по ЛКМ и отдача. Выполняется после TurretAim, поэтому использует актуальный угол ствола.
    /// Каждый выстрел сообщает врагам позицию игрока (PlayerShooting.NotifyShot).
    /// </summary>
    [DefaultExecutionOrder(75)]
    [RequireComponent(typeof(TankMovement))]
    public class TankShooting : MonoBehaviour
    {
        [SerializeField] private Bullet bulletPrefab;
        [Tooltip("Пустой объект на срезе ствола (ось Y = направление выстрела).")]
        [SerializeField] private Transform firePoint;
        [Tooltip("Ось вращения башни. Если пусто — родитель FirePoint.")]
        [SerializeField] private Transform barrelPivot;
        [Tooltip("Слой(и) стен. Нужен, чтобы пуля не рождалась внутри стены при стрельбе вплотную.")]
        [SerializeField] private LayerMask obstacleMask;

        [Header("Параметры")]
        [SerializeField] private float fireCooldown = 0.5f;
        [Tooltip("Удержание ЛКМ = непрерывный огонь.")]
        [SerializeField] private bool automatic = false;

        [Header("Отдача")]
        [SerializeField] private float recoilPixels = 5f;
        [Tooltip("Время, за которое происходит сдвиг (сек). Округляется до целого числа физ. шагов.")]
        [SerializeField] private float recoilDuration = 0.08f;

        private TankMovement movement;
        private Collider2D[] ownColliders;
        private float nextFireTime;

        private void Awake()
        {
            movement = GetComponent<TankMovement>();
            ownColliders = GetComponentsInChildren<Collider2D>();

            if (barrelPivot == null && firePoint != null) barrelPivot = firePoint.parent;
            if (bulletPrefab == null || firePoint == null)
            {
                Debug.LogError("TankShooting: не назначены Bullet Prefab или Fire Point.", this);
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            bool wantsToFire = automatic ? GameInput.FireHeld : GameInput.FirePressedThisFrame;
            if (wantsToFire && Time.time >= nextFireTime && Fire())
            {
                nextFireTime = Time.time + fireCooldown;
                PlayerShooting.NotifyShot(transform.position); // «звук выстрела» для ИИ врагов
            }
        }

        private bool Fire()
        {
            Vector2 aimDir = firePoint.up;
            Vector2 muzzle = firePoint.position;
            Vector2 pivot = barrelPivot != null ? (Vector2)barrelPivot.position : (Vector2)transform.position;
            float radius = bulletPrefab.WorldRadius;

            Vector2 spawnPos = muzzle;
            Vector2 bulletDir = aimDir;

            // Ствол упёрся в стену: пуля рождается у поверхности и сразу рикошетит.
            RaycastHit2D hit = Physics2D.Linecast(pivot, muzzle + aimDir * radius, obstacleMask);
            if (hit.collider != null)
            {
                if (hit.normal.sqrMagnitude < 0.5f) return false; // ось башни внутри стены — осечка
                spawnPos = hit.point + hit.normal * (radius * 1.1f);
                bulletDir = Vector2.Reflect(aimDir, hit.normal);
            }

            float angle = Mathf.Atan2(bulletDir.y, bulletDir.x) * Mathf.Rad2Deg - 90f;
            Bullet bullet = Instantiate(bulletPrefab, spawnPos, Quaternion.Euler(0f, 0f, angle));
            bullet.Launch(bulletDir, ownColliders);

            // Отдача строго против направления ствола.
            movement.ApplyRecoil(-aimDir, PixelUnits.ToUnits(recoilPixels), recoilDuration);
            return true;
        }
    }
}
