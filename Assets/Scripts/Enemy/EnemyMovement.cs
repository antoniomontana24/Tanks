using System.Collections.Generic;
using UnityEngine;

namespace NeonPrism
{
    /// <summary>
    /// Движение корпуса врага по лабиринту: строит путь через MazeNavigation и едет по нему
    /// (поворот корпуса + движение вперёд по transform.up) через Rigidbody2D в FixedUpdate.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class EnemyMovement : MonoBehaviour
    {
        [Header("Движение")]
        [Tooltip("Базовая скорость, юнитов/сек.")]
        [SerializeField] private float moveSpeed = 2.5f;
        [SerializeField] private float acceleration = 20f;
        [Tooltip("Градусов/сек.")]
        [SerializeField] private float rotateSpeed = 140f;
        [Tooltip("Если корпус смотрит мимо точки больше чем на этот угол — сначала разворот на месте.")]
        [Range(10f, 90f)]
        [SerializeField] private float turnInPlaceAngle = 45f;
        [Tooltip("Расстояние до точки пути, при котором она считается достигнутой.")]
        [SerializeField] private float reachDistance = 0.3f;

        [Header("Навигация")]
        [Tooltip("Минимальный интервал между перестроениями пути к движущейся цели, сек.")]
        [SerializeField] private float repathInterval = 0.4f;
        [Tooltip("Цель считается «сдвинувшейся», если ушла дальше этого расстояния.")]
        [SerializeField] private float repathDistance = 0.5f;
        [Tooltip("Если за это время враг не продвинулся — перестроить путь, затем сдаться.")]
        [SerializeField] private float stuckCheckTime = 1.2f;

        private Rigidbody2D rb;
        private LayerMask obstacleMask;

        private readonly List<Vector2> path = new List<Vector2>();
        private int pathIndex;
        private Vector2 currentDestination;
        private bool hasDestination;
        private bool needsPath;
        private float lastPathTime = -999f;

        private float currentSpeed;

        private float stuckTimer;
        private Vector2 stuckRefPos;
        private float stuckRefAngle;
        private int stuckRepaths;

        public float SpeedMultiplier { get; set; } = 1f;
        public bool HasPath => pathIndex < path.Count;
        /// <summary>Дошёл до конечной точки текущего маршрута.</summary>
        public bool Arrived { get; private set; }
        /// <summary>Маршрута к цели не существует.</summary>
        public bool PathFailed { get; private set; }
        /// <summary>Враг застрял (после повторного перестроения пути). Сбрасывается новым MoveTo.</summary>
        public bool IsStuck { get; private set; }
        public IReadOnlyList<Vector2> Path => path;

        public void Configure(LayerMask mask)
        {
            obstacleMask = mask;
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
            rb.linearDamping = 0f;
            rb.angularDamping = 0f;
        }

        private void Start()
        {
            MazeNavigation nav = MazeNavigation.GetOrCreate(obstacleMask);

            BoxCollider2D box = GetComponent<BoxCollider2D>();
            if (box != null)
            {
                Vector3 s = transform.lossyScale;
                float halfWidth = Mathf.Min(box.size.x * Mathf.Abs(s.x), box.size.y * Mathf.Abs(s.y)) * 0.5f;
                nav.RegisterAgentRadius(halfWidth * 0.9f);
            }
        }

        /// <summary>Ехать в точку. Путь строится при первом вызове и перестраивается, если цель заметно сдвинулась.</summary>
        public void MoveTo(Vector2 destination, bool forceRepath = false)
        {
            bool changed = !hasDestination ||
                           (destination - currentDestination).sqrMagnitude > repathDistance * repathDistance;
            if (!changed && !forceRepath) return;

            // Для движущейся цели не перестраиваем путь чаще, чем раз в repathInterval.
            if (changed && !forceRepath && hasDestination && Time.time - lastPathTime < repathInterval) return;

            currentDestination = destination;
            hasDestination = true;
            needsPath = true;
            IsStuck = false;
            stuckRepaths = 0;
        }

        public void Stop()
        {
            path.Clear();
            pathIndex = 0;
            hasDestination = false;
            needsPath = false;
            Arrived = false;
            PathFailed = false;
            IsStuck = false;
            stuckRepaths = 0;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (needsPath) TryBuildPath();

            rb.angularVelocity = 0f;
            float angle = rb.rotation;
            float newAngle = angle;
            float targetSpeed = 0f;
            Vector2 pos = rb.position;

            float reachSqr = reachDistance * reachDistance;
            while (pathIndex < path.Count && (path[pathIndex] - pos).sqrMagnitude <= reachSqr) pathIndex++;

            if (pathIndex < path.Count)
            {
                Vector2 to = path[pathIndex] - pos;
                float desired = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg - 90f;
                float delta = Mathf.DeltaAngle(angle, desired);
                float maxStep = rotateSpeed * dt;
                newAngle = angle + Mathf.Clamp(delta, -maxStep, maxStep);

                float error = Mathf.Abs(delta);
                float align = error > turnInPlaceAngle ? 0f : Mathf.Cos(error * Mathf.Deg2Rad);
                targetSpeed = moveSpeed * SpeedMultiplier * align;

                UpdateStuck(pos, angle, dt);
            }
            else
            {
                if (hasDestination && !needsPath && !PathFailed && path.Count > 0) Arrived = true;
                ResetStuckTracking();
            }

            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * dt);
            rb.MoveRotation(newAngle);

            Vector2 heading = Quaternion.Euler(0f, 0f, newAngle) * Vector3.up;
            rb.linearVelocity = heading * currentSpeed;
        }

        private void TryBuildPath()
        {
            MazeNavigation nav = MazeNavigation.Instance;
            if (nav == null || !nav.IsReady) return; // сетка ещё строится — попробуем на следующем шаге

            needsPath = false;
            lastPathTime = Time.time;

            if (!nav.TryFindPath(rb.position, currentDestination, path))
            {
                path.Clear();
                pathIndex = 0;
                PathFailed = true;
                Arrived = false;
                return;
            }

            PathFailed = false;
            Arrived = false;
            pathIndex = 0;
            ResetStuckTracking();
        }

        private void ResetStuckTracking()
        {
            stuckTimer = 0f;
            stuckRefPos = rb.position;
            stuckRefAngle = rb.rotation;
        }

        private void UpdateStuck(Vector2 pos, float angle, float dt)
        {
            stuckTimer += dt;

            bool progressed = (pos - stuckRefPos).sqrMagnitude > 0.01f ||
                              Mathf.Abs(Mathf.DeltaAngle(angle, stuckRefAngle)) > 8f;
            if (progressed)
            {
                stuckRefPos = pos;
                stuckRefAngle = angle;
                stuckTimer = 0f;
                stuckRepaths = 0;
                return;
            }

            if (stuckTimer < stuckCheckTime) return;

            stuckTimer = 0f;
            stuckRefPos = pos;
            stuckRefAngle = angle;

            if (stuckRepaths < 1)
            {
                stuckRepaths++;
                needsPath = true; // один раз пробуем перестроить путь из текущей точки
            }
            else
            {
                IsStuck = true;
                path.Clear();
                pathIndex = 0;
                stuckRepaths = 0;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (path == null || path.Count == 0) return;
            Gizmos.color = Color.yellow;
            Vector3 prev = transform.position;
            for (int i = pathIndex; i < path.Count; i++)
            {
                Vector3 p = path[i];
                Gizmos.DrawLine(prev, p);
                Gizmos.DrawWireSphere(p, 0.08f);
                prev = p;
            }
        }
#endif
    }
}
