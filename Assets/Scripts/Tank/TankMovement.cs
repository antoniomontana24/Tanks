using UnityEngine;

namespace NeonPrism
{
    /// <summary>
    /// Движение корпуса: W/S — вперёд/назад по transform.up, A/D — поворот.
    /// Вся физика — в FixedUpdate через rb.linearVelocity и rb.MoveRotation.
    /// Также принимает импульс отдачи от TankShooting.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class TankMovement : MonoBehaviour
    {
        [Header("Движение")]
        [Tooltip("Максимальная скорость, юнитов/сек (при 16 PPU: 3.5 = 56 px/сек).")]
        [SerializeField] private float moveSpeed = 3.5f;
        [Tooltip("Разгон/торможение, юнитов/сек^2.")]
        [SerializeField] private float acceleration = 30f;
        [Tooltip("Угловая скорость поворота корпуса, градусов/сек.")]
        [SerializeField] private float rotateSpeed = 150f;

        private Rigidbody2D rb;
        private float moveInput;
        private float turnInput;
        private float currentSpeed;

        private Vector2 recoilVelocity;
        private int recoilStepsLeft;

        private void Reset()
        {
            ConfigureBody(GetComponent<Rigidbody2D>());
            GetComponent<Rigidbody2D>().mass = 10f;
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            ConfigureBody(rb);
        }

        private static void ConfigureBody(Rigidbody2D body)
        {
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
        }

        private void Update()
        {
            moveInput = GameInput.Move;
            turnInput = GameInput.Turn;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            // Столкновения не должны раскручивать корпус — угол задаём только мы.
            rb.angularVelocity = 0f;
            float newRotation = rb.rotation + turnInput * rotateSpeed * dt;
            rb.MoveRotation(newRotation);

            currentSpeed = Mathf.MoveTowards(currentSpeed, moveInput * moveSpeed, acceleration * dt);

            // Направление берём из нового угла (transform.up обновится только после шага физики).
            Vector2 heading = Quaternion.Euler(0f, 0f, newRotation) * Vector3.up;
            Vector2 velocity = heading * currentSpeed;

            if (recoilStepsLeft > 0)
            {
                velocity += recoilVelocity;
                recoilStepsLeft--;
            }

            rb.linearVelocity = velocity;
        }

        /// <summary>
        /// Отдача: сдвиг ровно на distance юнитов в направлении direction за duration секунд.
        /// Скорость отдачи постоянна и действует целое число физических шагов, поэтому
        /// смещение точное. Сдвиг выполняет сам физический движок, так что танк
        /// не может быть вытолкнут сквозь стену (при упоре кормой в стену он просто не сдвинется).
        /// </summary>
        public void ApplyRecoil(Vector2 direction, float distance, float duration)
        {
            if (direction.sqrMagnitude < 1e-8f || distance <= 0f) return;

            float dt = Time.fixedDeltaTime;
            int steps = Mathf.Max(1, Mathf.RoundToInt(duration / dt));
            recoilVelocity = direction.normalized * (distance / (steps * dt));
            recoilStepsLeft = steps;
        }
    }
}
