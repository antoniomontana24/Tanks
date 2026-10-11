using System.Collections.Generic;
using UnityEngine;

namespace NeonPrism
{
    /// <summary>
    /// Полупрозрачный луч прицеливания с отражениями от стен.
    /// Вешается на Turret вместе с LineRenderer.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(LineRenderer))]
    public class AimLaser : MonoBehaviour
    {
        [SerializeField] private Transform firePoint;
        [SerializeField] private LayerMask obstacleMask;
        [SerializeField] private float lengthPixels = 300f; // Суммарная длина лазера в пикселях
        [SerializeField] private float widthPixels = 2f;
        [SerializeField] private Color color = new Color(0f, 1f, 1f, 1f);
        [Range(0.35f, 0.5f)]
        [SerializeField] private float alpha = 0.4f;

        [Header("Рикошеты")]
        [Range(1, 10)]
        [SerializeField] private int maxBounces = 4; // Максимальное количество отскоков

        private LineRenderer line;
        private float totalLength;
        private readonly List<Vector3> points = new List<Vector3>();

        private void Awake()
        {
            line = GetComponent<LineRenderer>();
            totalLength = PixelUnits.ToUnits(lengthPixels);

            line.useWorldSpace = true;
            line.numCapVertices = 0;
            line.startWidth = line.endWidth = PixelUnits.ToUnits(widthPixels);

            Color c = color;
            c.a = alpha;
            line.startColor = line.endColor = c;

            if (line.sharedMaterial == null)
            {
                line.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            }

            if (firePoint == null)
            {
                Debug.LogError("AimLaser: не назначен Fire Point.", this);
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (firePoint == null) return;

            DrawTrajectory();
        }

        private void DrawTrajectory()
        {
            points.Clear();

            Vector2 origin = firePoint.position;
            Vector2 dir = firePoint.up; // Направление выстрела дула
            float remainingLength = totalLength;

            points.Add(new Vector3(origin.x, origin.y, 0f));

            // Запас шагов = число отскоков + 1 финальный сегмент
            for (int i = 0; i <= maxBounces; i++)
            {
                if (remainingLength <= 0f) break;

                // Смещение 0.002f предотвращает застревание луча на месте удара (устраняет мигание)
                RaycastHit2D hit = Physics2D.Raycast(origin + dir * 0.002f, dir, remainingLength, obstacleMask);

                if (hit.collider != null)
                {
                    points.Add(new Vector3(hit.point.x, hit.point.y, 0f));
                    remainingLength -= hit.distance;

                    // Зеркальное отражение вектора от стены
                    dir = Vector2.Reflect(dir, hit.normal);
                    origin = hit.point;
                }
                else
                {
                    Vector2 end = origin + dir * remainingLength;
                    points.Add(new Vector3(end.x, end.y, 0f));
                    break;
                }
            }

            line.positionCount = points.Count;
            line.SetPositions(points.ToArray());
        }
    }
}