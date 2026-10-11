using UnityEngine;

namespace NeonPrism
{
    public enum EnemyState
    {
        Patrol,
        Chase,
        Attack
    }

    /// <summary>
    /// Конечный автомат врага: Patrol -> (выстрел игрока / сближение) -> Chase/Attack на 5 секунд -> Patrol.
    /// Chase — идём к игроку по кратчайшему пути; Attack — есть прямая линия огня, башня стреляет.
    /// </summary>
    [RequireComponent(typeof(EnemyMovement))]
    public class EnemyAI : MonoBehaviour
    {
        [Header("Ссылки")]
        [SerializeField] private EnemyTurretAim turretAim;
        [SerializeField] private EnemyShooting shooting;
        [Tooltip("Слой стен лабиринта: проверка линии огня и построение навигации.")]
        [SerializeField] private LayerMask obstacleMask;

        [Header("Агро")]
        [Tooltip("Порог сближения в пикселях (16 px = 1 юнит). Считается по зазору между корпусами.")]
        [SerializeField] private float proximityPixels = 5f;
        [SerializeField] private float agroDuration = 5f;
        [Tooltip("Слышимость выстрела в юнитах. 0 = слышит с любой точки карты.")]
        [SerializeField] private float hearingRadius = 0f;
        [Tooltip("Вкл: во время агро враг знает точную позицию игрока. Выкл: только позицию последнего выстрела.")]
        [SerializeField] private bool trackLivePlayerPosition = true;
        [Tooltip("Задержка до первого выстрела после тревоги, сек.")]
        [SerializeField] private float reactionTime = 0.4f;

        [Header("Атака")]
        [SerializeField] private float maxFireRange = 14f;
        [Tooltip("В режиме Attack враг останавливается на этой дистанции. 0 = всегда сближается.")]
        [SerializeField] private float keepDistance = 3f;
        [Tooltip("Стреляет, когда ствол смотрит на игрока с точностью до этого угла.")]
        [SerializeField] private float fireAngleTolerance = 6f;
        [Tooltip("Сколько секунд после потери линии огня остаётся режим Attack (против мерцания состояний).")]
        [SerializeField] private float attackHoldTime = 0.3f;

        [Header("Патруль")]
        [Tooltip("Необязательно: точки патруля по кругу. Пусто = случайные точки лабиринта.")]
        [SerializeField] private Transform[] patrolPoints;
        [SerializeField] private float patrolMinDistance = 4f;
        [SerializeField] private float patrolMaxDistance = 14f;
        [SerializeField] private float patrolPause = 0.6f;
        [SerializeField] private float patrolSpeedMultiplier = 1f;
        [SerializeField] private float chaseSpeedMultiplier = 1.3f;

        private EnemyMovement movement;
        private Collider2D ownCollider;
        private Transform player;
        private Collider2D playerCollider;
        private float nextPlayerSearch;

        private float agroTimer;
        private Vector2 lastKnownPlayerPos;
        private float lastLosTime = -999f;

        private bool hasPatrolDestination;
        private float patrolTimer;
        private int patrolIndex = -1;

        public EnemyState State { get; private set; } = EnemyState.Patrol;
        public float AgroTimeLeft => agroTimer;

        private void Awake()
        {
            movement = GetComponent<EnemyMovement>();
            ownCollider = GetComponent<Collider2D>();
            if (turretAim == null) turretAim = GetComponentInChildren<EnemyTurretAim>();
            if (shooting == null) shooting = GetComponentInChildren<EnemyShooting>();

            if (turretAim == null || shooting == null)
            {
                Debug.LogError("EnemyAI: не найдены EnemyTurretAim / EnemyShooting на башне.", this);
                enabled = false;
                return;
            }
            if (obstacleMask.value == 0)
            {
                Debug.LogError("EnemyAI: не задан Obstacle Mask (слой стен).", this);
            }

            movement.Configure(obstacleMask);
            shooting.Configure(obstacleMask);
            movement.SpeedMultiplier = patrolSpeedMultiplier;
            patrolTimer = Random.Range(0f, Mathf.Max(0.01f, patrolPause));
        }

        private void OnEnable()
        {
            PlayerShooting.OnShoot += HandlePlayerShot;
        }

        private void OnDisable()
        {
            PlayerShooting.OnShoot -= HandlePlayerShot;
        }

        private void HandlePlayerShot(Vector2 playerPosition)
        {
            if (hearingRadius > 0f && Vector2.Distance(transform.position, playerPosition) > hearingRadius) return;
            TriggerAgro(playerPosition);
        }

        private void TriggerAgro(Vector2 playerPosition)
        {
            lastKnownPlayerPos = playerPosition;
            agroTimer = agroDuration;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            AcquirePlayer();

            if (agroTimer > 0f) agroTimer = Mathf.Max(0f, agroTimer - dt);
            if (player != null && IsPlayerClose()) TriggerAgro(player.position);

            bool agro = agroTimer > 0f;
            bool wasAgro = State != EnemyState.Patrol;

            if (agro && !wasAgro) OnAgroStart();
            else if (!agro && wasAgro) OnAgroEnd();

            if (agro) TickAgro();
            else TickPatrol(dt);
        }

        // ------------------------------------------------------------ игрок

        private void AcquirePlayer()
        {
            if (player != null) return;
            if (Time.time < nextPlayerSearch) return;
            nextPlayerSearch = Time.time + 0.5f;

            TankMovement tank = FindFirstObjectByType<TankMovement>();
            if (tank != null)
            {
                player = tank.transform;
                playerCollider = tank.GetComponent<Collider2D>();
            }
        }

        private bool IsPlayerClose()
        {
            if (ownCollider == null || playerCollider == null) return false;

            float threshold = PixelUnits.ToUnits(proximityPixels);
            Vector2 delta = (Vector2)player.position - (Vector2)transform.position;

            // Быстрый отсев: далеко и по центрам, и по размерам.
            float reject = threshold + ownCollider.bounds.extents.magnitude + playerCollider.bounds.extents.magnitude;
            if (delta.sqrMagnitude > reject * reject) return false;

            if (delta.magnitude <= threshold) return true; // дословное условие из ТЗ (расстояние между центрами)

            ColliderDistance2D gap = ownCollider.Distance(playerCollider); // зазор между корпусами
            return gap.isValid && gap.distance <= threshold;
        }

        // ------------------------------------------------------------ состояния

        private void OnAgroStart()
        {
            State = EnemyState.Chase;
            lastLosTime = -999f;
            hasPatrolDestination = false;
            movement.Stop();
            movement.SpeedMultiplier = chaseSpeedMultiplier;
            shooting.DelayShots(reactionTime);
        }

        private void OnAgroEnd()
        {
            State = EnemyState.Patrol;
            turretAim.ReleaseAim();
            movement.Stop();
            movement.SpeedMultiplier = patrolSpeedMultiplier;
            hasPatrolDestination = false;
            patrolTimer = patrolPause;
        }

        private void TickAgro()
        {
            Vector2 target = GetTargetPosition();
            turretAim.AimAt(target);

            float muzzleDistance = Vector2.Distance(shooting.MuzzlePosition, target);
            bool lineOfFire = muzzleDistance <= maxFireRange && shooting.HasLineOfFire(target);
            if (lineOfFire) lastLosTime = Time.time;

            State = (Time.time - lastLosTime <= attackHoldTime) ? EnemyState.Attack : EnemyState.Chase;

            float bodyDistance = Vector2.Distance(transform.position, target);
            bool hold = State == EnemyState.Attack && lineOfFire && keepDistance > 0f && bodyDistance <= keepDistance;
            if (hold) movement.Stop();
            else movement.MoveTo(target, movement.IsStuck);

            if (lineOfFire && State == EnemyState.Attack &&
                turretAim.AimErrorDegrees <= fireAngleTolerance && shooting.CanFire)
            {
                shooting.TryFire();
            }
        }

        private Vector2 GetTargetPosition()
        {
            if (trackLivePlayerPosition && player != null) lastKnownPlayerPos = player.position;
            return lastKnownPlayerPos;
        }

        private void TickPatrol(float dt)
        {
            turretAim.ReleaseAim();

            if (hasPatrolDestination && (movement.Arrived || movement.PathFailed || movement.IsStuck))
            {
                patrolTimer = movement.Arrived ? patrolPause : 0.2f;
                hasPatrolDestination = false;
                movement.Stop();
            }

            if (hasPatrolDestination) return;

            patrolTimer -= dt;
            if (patrolTimer > 0f) return;

            if (PickPatrolDestination()) hasPatrolDestination = true;
            else patrolTimer = 0.3f; // сетка ещё не готова или нет точек — повторим позже
        }

        private bool PickPatrolDestination()
        {
            if (patrolPoints != null && patrolPoints.Length > 0)
            {
                for (int i = 0; i < patrolPoints.Length; i++)
                {
                    patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                    Transform t = patrolPoints[patrolIndex];
                    if (t != null)
                    {
                        movement.MoveTo(t.position);
                        return true;
                    }
                }
                return false;
            }

            MazeNavigation nav = MazeNavigation.Instance;
            if (nav == null || !nav.IsReady) return false;
            if (!nav.TryGetRandomWalkable(transform.position, patrolMinDistance, patrolMaxDistance, out Vector2 point)) return false;

            movement.MoveTo(point);
            return true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = State == EnemyState.Attack ? Color.red : (State == EnemyState.Chase ? Color.yellow : Color.green);
            Gizmos.DrawWireSphere(transform.position, 0.7f);
            if (State != EnemyState.Patrol) Gizmos.DrawLine(transform.position, lastKnownPlayerPos);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, PixelUnits.ToUnits(proximityPixels));
        }
#endif
    }
}
