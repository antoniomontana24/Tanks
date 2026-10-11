using UnityEngine;

namespace NeonPrism
{
    /// <summary>
    /// Безопасный спавн пули для врагов (та же логика, что в TankShooting):
    /// если ствол упёрся в стену, пуля рождается у поверхности и сразу отражается.
    /// </summary>
    public static class BulletSpawner
    {
        public static Bullet Spawn(Bullet prefab, Transform firePoint, Transform barrelPivot,
                                   LayerMask obstacleMask, Collider2D[] ownerColliders)
        {
            if (prefab == null || firePoint == null) return null;

            Vector2 aimDir = firePoint.up;
            Vector2 muzzle = firePoint.position;
            Vector2 pivot = barrelPivot != null ? (Vector2)barrelPivot.position : muzzle;
            float radius = prefab.WorldRadius;

            Vector2 spawnPos = muzzle;
            Vector2 bulletDir = aimDir;

            RaycastHit2D hit = Physics2D.Linecast(pivot, muzzle + aimDir * radius, obstacleMask);
            if (hit.collider != null)
            {
                if (hit.normal.sqrMagnitude < 0.5f) return null; // ось башни внутри стены — осечка
                spawnPos = hit.point + hit.normal * (radius * 1.1f);
                bulletDir = Vector2.Reflect(aimDir, hit.normal);
            }

            float angle = Mathf.Atan2(bulletDir.y, bulletDir.x) * Mathf.Rad2Deg - 90f;
            Bullet bullet = Object.Instantiate(prefab, spawnPos, Quaternion.Euler(0f, 0f, angle));
            bullet.Launch(bulletDir, ownerColliders);
            return bullet;
        }
    }
}
