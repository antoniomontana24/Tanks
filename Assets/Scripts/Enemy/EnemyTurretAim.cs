using UnityEngine;

namespace NeonPrism
{
    /// <summary>
    /// Башня врага: дочерний объект корпуса (без Rigidbody2D, локальная позиция (0,0,0)).
    /// Вращается независимо от корпуса: в режиме тревоги держит прицел на точке, иначе
    /// возвращается к направлению корпуса и (опционально) покачивается, сканируя коридор.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class EnemyTurretAim : MonoBehaviour
    {
        [Tooltip("Градусов/сек.")]
        [SerializeField] private float rotateSpeed = 220f;
        [SerializeField] private bool scanInPatrol = true;
        [SerializeField] private float scanAngle = 40f;
        [Tooltip("Период полного качания влево-вправо, сек.")]
        [SerializeField] private float scanPeriod = 4f;

        private bool tracking;
        private Vector2 trackPoint;
        private float scanPhase;

        /// <summary>Угол (в градусах) между направлением ствола и целью после последнего поворота.</summary>
        public float AimErrorDegrees { get; private set; } = 180f;
        public bool IsTracking => tracking;

        public void AimAt(Vector2 worldPoint)
        {
            tracking = true;
            trackPoint = worldPoint;
        }

        public void ReleaseAim()
        {
            tracking = false;
        }

        private void Awake()
        {
            // Башня крепится строго в центре корпуса и не имеет собственной физики.
            transform.localPosition = Vector3.zero;
            if (GetComponent<Rigidbody2D>() != null)
            {
                Debug.LogWarning("EnemyTurretAim: у башни не должно быть Rigidbody2D — физика наследуется от корпуса.", this);
            }
            scanPhase = Random.value * Mathf.PI * 2f; // чтобы враги качались не синхронно
        }

        private void LateUpdate()
        {
            float current = transform.eulerAngles.z;
            float target;

            if (tracking)
            {
                Vector2 d = trackPoint - (Vector2)transform.position;
                target = d.sqrMagnitude < 1e-6f ? current : Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f;
            }
            else
            {
                float bodyAngle = transform.parent != null ? transform.parent.eulerAngles.z : 0f;
                float offset = 0f;
                if (scanInPatrol && scanPeriod > 0.01f)
                {
                    scanPhase += Time.deltaTime * Mathf.PI * 2f / scanPeriod;
                    offset = Mathf.Sin(scanPhase) * scanAngle;
                }
                target = bodyAngle + offset;
            }

            float next = Mathf.MoveTowardsAngle(current, target, rotateSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, 0f, next);
            AimErrorDegrees = Mathf.Abs(Mathf.DeltaAngle(next, target));
        }
    }
}
