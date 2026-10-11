using UnityEngine;

namespace NeonPrism
{
    /// <summary>
    /// Снаряд: идеальный рикошет без потери скорости, ровно 5 отскоков (на 6-м столкновении со стеной — детонация).
    /// Любой объект с Health (в т.ч. собственный танк) получает урон и уничтожает пулю.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public class Bullet : MonoBehaviour
    {
        [SerializeField] private float speed = 9f;
        [SerializeField] private int maxRicochets = 5;
        [SerializeField] private float maxLifetime = 7f;
        [SerializeField] private int damage = 1;
        [Tooltip("Первые секунды пуля не сталкивается с танком-владельцем (чтобы не задеть собственный ствол при вылете).")]
        [SerializeField] private float ownerGraceTime = 0.12f;
        [SerializeField] private ImpactFlash impactFlashPrefab;

        private Rigidbody2D rb;
        private CircleCollider2D circle;

        private Collider2D[] ignoredColliders;
        private float spawnTime;
        private bool graceEnded = true;
        private bool dead;

        private int ricochetCount;
        private Vector2 lastVelocity;
        private int stepId;
        private int lastHitStep = -1;

        /// <summary>Радиус пули в мировых единицах (работает и на ассете префаба).</summary>
        public float WorldRadius
        {
            get
            {
                CircleCollider2D c = GetComponent<CircleCollider2D>();
                Vector3 s = transform.lossyScale;
                return c.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y));
            }
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            circle = GetComponent<CircleCollider2D>();

            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.linearDamping = 0f;
            rb.angularDamping = 0f;
            rb.freezeRotation = true;

            // Страховка: если в префабе забыли материал отскока — создаём (Friction 0, Bounciness 1).
            if (circle.sharedMaterial == null)
            {
                circle.sharedMaterial = new PhysicsMaterial2D("Bullet_Bouncy_Runtime")
                {
                    friction = 0f,
                    bounciness = 1f
                };
            }
        }

        public void Launch(Vector2 direction, Collider2D[] ownerColliders)
        {
            Vector2 dir = direction.sqrMagnitude > 1e-8f ? direction.normalized : Vector2.up;
            rb.linearVelocity = dir * speed;
            lastVelocity = rb.linearVelocity;

            spawnTime = Time.time;
            ignoredColliders = ownerColliders;
            if (ignoredColliders != null && ownerGraceTime > 0f)
            {
                SetIgnoreOwner(true);
                graceEnded = false;
            }

            Destroy(gameObject, maxLifetime);
        }

        private void SetIgnoreOwner(bool ignore)
        {
            for (int i = 0; i < ignoredColliders.Length; i++)
            {
                if (ignoredColliders[i] != null)
                {
                    Physics2D.IgnoreCollision(circle, ignoredColliders[i], ignore);
                }
            }
        }

        private void Update()
        {
            if (!graceEnded && Time.time - spawnTime >= ownerGraceTime)
            {
                graceEnded = true;
                SetIgnoreOwner(false);
            }
        }

        private void FixedUpdate()
        {
            if (dead) return;

            stepId++;
            Vector2 v = rb.linearVelocity;
            if (v.sqrMagnitude < 1e-6f)
            {
                v = lastVelocity.sqrMagnitude > 1e-6f ? lastVelocity : Vector2.up * speed;
            }

            // Скорость строго постоянна: рикошет не может её ни замедлить, ни ускорить.
            v = v.normalized * speed;
            rb.linearVelocity = v;
            lastVelocity = v; // скорость ДО следующего шага физики
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (dead) return;

            // Попадание в танк (в т.ч. свой).
            Health health = collision.collider.GetComponentInParent<Health>();
            if (health != null)
            {
                health.TakeDamage(damage);
                Detonate();
                return;
            }

            if (collision.contactCount == 0) return;
            ContactPoint2D contact = collision.GetContact(0);

            // Нормаль всегда направляем от стены к пуле.
            Vector2 normal = contact.normal;
            Vector2 toCenter = rb.position - contact.point;
            if (toCenter.sqrMagnitude > 1e-8f && Vector2.Dot(normal, toCenter) < 0f) normal = -normal;

            // Два контакта за один физ. шаг (угол) — отражаем уже отражённую скорость и считаем как один рикошет.
            bool secondInStep = lastHitStep == stepId;
            lastHitStep = stepId;

            Vector2 incoming = secondInStep ? rb.linearVelocity : lastVelocity;
            Vector2 outgoing = Vector2.Dot(incoming, normal) < 0f ? Vector2.Reflect(incoming, normal) : incoming;
            if (outgoing.sqrMagnitude < 1e-6f) outgoing = -incoming;

            outgoing = outgoing.normalized * speed;
            rb.linearVelocity = outgoing;
            lastVelocity = outgoing;

            if (secondInStep) return;

            ricochetCount++;
            if (ricochetCount > maxRicochets)
            {
                Detonate(); // 6-е столкновение
                return;
            }

            SpawnFlash(contact.point);
        }

        private void SpawnFlash(Vector2 point)
        {
            if (impactFlashPrefab != null)
            {
                Instantiate(impactFlashPrefab, new Vector3(point.x, point.y, 0f), Quaternion.identity);
            }
        }

        private void Detonate()
        {
            dead = true;
            rb.simulated = false;
            Destroy(gameObject);
        }
    }
}
