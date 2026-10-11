using UnityEngine;

namespace NeonPrism
{
    /// <summary>
    /// Стрельба врага: тайминг, проверка линии огня, спавн пуль из FirePoint.
    /// Вешается на Turret.
    /// </summary>
    public class EnemyShooting : MonoBehaviour
    {
        [SerializeField] private Bullet bulletPrefab;
        [Tooltip("Пустой объект на срезе ствола (ось Y = направление выстрела).")]
        [SerializeField] private Transform firePoint;
        [Tooltip("Ось вращения башни. Если пусто — этот объект.")]
        [SerializeField] private Transform barrelPivot;

        [Header("Скорострельность")]
        [Tooltip("Интервал между выстрелами выбирается случайно в этом диапазоне, сек.")]
        [SerializeField] private float fireIntervalMin = 1.0f;
        [SerializeField] private float fireIntervalMax = 1.5f;

        private LayerMask obstacleMask;
        private Collider2D[] ownColliders;
        private float nextFireTime;

        public bool CanFire => Time.time >= nextFireTime;
        public Vector2 MuzzlePosition => firePoint != null ? (Vector2)firePoint.position : (Vector2)transform.position;

        public void Configure(LayerMask mask)
        {
            obstacleMask = mask;
        }

        private void Awake()
        {
            Health health = GetComponentInParent<Health>();
            Transform root = health != null ? health.transform : transform.root;
            ownColliders = root.GetComponentsInChildren<Collider2D>();

            if (barrelPivot == null) barrelPivot = transform;
            if (bulletPrefab == null || firePoint == null)
            {
                Debug.LogError("EnemyShooting: не назначены Bullet Prefab или Fire Point.", this);
                enabled = false;
            }
        }

        /// <summary>Отложить следующий выстрел (время реакции после тревоги).</summary>
        public void DelayShots(float seconds)
        {
            nextFireTime = Mathf.Max(nextFireTime, Time.time + seconds);
        }

        /// <summary>
        /// Есть ли прямая линия огня до цели: ствол не торчит из стены и пуля (с её радиусом)
        /// долетит до цели, не задев стену.
        /// </summary>
        public bool HasLineOfFire(Vector2 target)
        {
            if (firePoint == null) return false;

            Vector2 muzzle = firePoint.position;
            Vector2 pivot = barrelPivot != null ? (Vector2)barrelPivot.position : muzzle;

            if (Physics2D.Linecast(pivot, muzzle, obstacleMask).collider != null) return false;

            Vector2 delta = target - muzzle;
            float dist = delta.magnitude;
            if (dist < 0.01f) return true;

            float radius = bulletPrefab != null ? bulletPrefab.WorldRadius * 0.9f : 0.1f;
            RaycastHit2D hit = Physics2D.CircleCast(muzzle, radius, delta / dist, dist, obstacleMask);
            return hit.collider == null;
        }

        public bool TryFire()
        {
            if (!enabled || !CanFire) return false;

            Bullet bullet = BulletSpawner.Spawn(bulletPrefab, firePoint, barrelPivot, obstacleMask, ownColliders);
            if (bullet == null) return false;

            nextFireTime = Time.time + Random.Range(fireIntervalMin, Mathf.Max(fireIntervalMin, fireIntervalMax));
            return true;
        }
    }
}
