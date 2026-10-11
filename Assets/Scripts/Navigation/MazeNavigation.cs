using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NeonPrism
{
    /// <summary>
    /// Навигационная сетка лабиринта. Строится один раз в начале игры по коллайдерам стен (слой Obstacle Mask):
    /// узлы сетки, в которых помещается «танк» (круг радиусом agentRadius), считаются проходимыми.
    /// Кратчайший путь ищется алгоритмом A* (8 направлений, без срезания углов) и затем спрямляется.
    /// Если на сцене нет объекта MazeNavigation, его автоматически создаёт первый враг.
    /// </summary>
    public class MazeNavigation : MonoBehaviour
    {
        public static MazeNavigation Instance { get; private set; }

        [SerializeField] private LayerMask obstacleMask;
        [Tooltip("Шаг сетки в юнитах. 0.5 = 8 px при 16 PPU.")]
        [SerializeField] private float cellSize = 0.5f;
        [Tooltip("Радиус танка для проверки проходимости. 0 = авто (по размеру коллайдера врага).")]
        [SerializeField] private float agentRadius = 0f;
        [SerializeField] private float boundsPadding = 1f;
        [SerializeField] private bool drawGizmos = false;

        private const int MaxSearchRing = 6;
        private const float Diagonal = 1.41421356f;
        private static readonly int[] Dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
        private static readonly int[] Dy = { 0, 0, 1, -1, 1, -1, 1, -1 };

        private struct HeapItem
        {
            public int node;
            public float f;
        }

        private bool[] walkable;
        private int width, height;
        private Vector2 origin;
        private readonly List<int> walkableCells = new List<int>();

        private float[] gCost;
        private int[] parent;
        private int[] openStamp;
        private int[] closedStamp;
        private int stamp;
        private HeapItem[] heap = new HeapItem[256];
        private int heapCount;
        private readonly List<Vector2> rawPath = new List<Vector2>();

        private float autoRadius;

        public bool IsReady { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        public static MazeNavigation GetOrCreate(LayerMask mask)
        {
            if (Instance != null) return Instance;

            GameObject go = new GameObject("MazeNavigation (auto)");
            MazeNavigation nav = go.AddComponent<MazeNavigation>();
            nav.obstacleMask = mask;
            return nav;
        }

        /// <summary>Враги сообщают свой размер до построения сетки (строится через 2 физ. шага после старта).</summary>
        public void RegisterAgentRadius(float radius)
        {
            autoRadius = Mathf.Max(autoRadius, radius);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (Instance == this) StartCoroutine(BuildRoutine());
        }

        private IEnumerator BuildRoutine()
        {
            // Даём физике (в т.ч. Composite Collider тайлмапы) построить геометрию.
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Build();
        }

        [ContextMenu("Rebuild")]
        public void Build()
        {
            IsReady = false;
            float radius = agentRadius > 0f ? agentRadius : (autoRadius > 0f ? autoRadius : 0.4f);
            Physics2D.SyncTransforms();

            Collider2D[] all = FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
            bool hasBounds = false;
            Bounds bounds = new Bounds();
            for (int i = 0; i < all.Length; i++)
            {
                Collider2D c = all[i];
                if (c == null || !c.enabled) continue;
                if (((1 << c.gameObject.layer) & obstacleMask.value) == 0) continue;

                Bounds cb = c.bounds;
                if (cb.size.sqrMagnitude < 1e-6f) continue;
                if (!hasBounds) { bounds = cb; hasBounds = true; }
                else bounds.Encapsulate(cb);
            }

            if (!hasBounds)
            {
                Debug.LogError("MazeNavigation: не найдено ни одного коллайдера на слое препятствий (Obstacle Mask). " +
                               "Проверьте слой стен и маску в EnemyAI.", this);
                return;
            }

            float minX = Mathf.Floor((bounds.min.x - boundsPadding) / cellSize) * cellSize;
            float minY = Mathf.Floor((bounds.min.y - boundsPadding) / cellSize) * cellSize;
            float maxX = Mathf.Ceil((bounds.max.x + boundsPadding) / cellSize) * cellSize;
            float maxY = Mathf.Ceil((bounds.max.y + boundsPadding) / cellSize) * cellSize;

            origin = new Vector2(minX, minY);
            width = Mathf.RoundToInt((maxX - minX) / cellSize) + 1;
            height = Mathf.RoundToInt((maxY - minY) / cellSize) + 1;

            if ((long)width * height > 2000000L)
            {
                Debug.LogError("MazeNavigation: слишком большая сетка. Увеличьте Cell Size.", this);
                return;
            }

            int n = width * height;
            walkable = new bool[n];
            gCost = new float[n];
            parent = new int[n];
            openStamp = new int[n];
            closedStamp = new int[n];
            stamp = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    walkable[y * width + x] = Physics2D.OverlapCircle(NodePosition(x, y), radius, obstacleMask) == null;
                }
            }

            KeepLargestRegion(n);

            walkableCells.Clear();
            for (int i = 0; i < n; i++)
            {
                if (walkable[i]) walkableCells.Add(i);
            }

            IsReady = walkableCells.Count > 0;
            if (!IsReady)
            {
                Debug.LogError("MazeNavigation: проходимых узлов нет. Уменьшите Agent Radius или Cell Size.", this);
            }
        }

        // Оставляем только самую большую связную область: замкнутые «карманы» внутри стен не нужны.
        private void KeepLargestRegion(int n)
        {
            int[] label = new int[n];
            Stack<int> stack = new Stack<int>();
            int current = 0, bestLabel = -1, bestSize = 0;

            for (int i = 0; i < n; i++)
            {
                if (!walkable[i] || label[i] != 0) continue;

                current++;
                int size = 0;
                label[i] = current;
                stack.Push(i);
                while (stack.Count > 0)
                {
                    int c = stack.Pop();
                    size++;
                    int cx = c % width, cy = c / width;
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = cx + Dx[d], ny = cy + Dy[d];
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                        int ni = ny * width + nx;
                        if (!walkable[ni] || label[ni] != 0) continue;
                        label[ni] = current;
                        stack.Push(ni);
                    }
                }

                if (size > bestSize)
                {
                    bestSize = size;
                    bestLabel = current;
                }
            }

            for (int i = 0; i < n; i++)
            {
                if (walkable[i] && label[i] != bestLabel) walkable[i] = false;
            }
        }

        private Vector2 NodePosition(int x, int y)
        {
            return origin + new Vector2(x, y) * cellSize;
        }

        private bool TryGetNearestWalkable(Vector2 p, out int index)
        {
            index = -1;
            int cx = Mathf.RoundToInt((p.x - origin.x) / cellSize);
            int cy = Mathf.RoundToInt((p.y - origin.y) / cellSize);
            float best = float.MaxValue;

            for (int r = 0; r <= MaxSearchRing; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        int x = cx + dx, y = cy + dy;
                        if (x < 0 || y < 0 || x >= width || y >= height) continue;
                        int i = y * width + x;
                        if (!walkable[i]) continue;

                        float d = (NodePosition(x, y) - p).sqrMagnitude;
                        if (d < best)
                        {
                            best = d;
                            index = i;
                        }
                    }
                }
                if (index >= 0) return true;
            }
            return false;
        }

        private bool IsWalkableAt(Vector2 p)
        {
            int x = Mathf.RoundToInt((p.x - origin.x) / cellSize);
            int y = Mathf.RoundToInt((p.y - origin.y) / cellSize);
            if (x < 0 || y < 0 || x >= width || y >= height) return false;
            return walkable[y * width + x];
        }

        private bool LineWalkable(Vector2 a, Vector2 b)
        {
            Vector2 delta = b - a;
            float dist = delta.magnitude;
            if (dist < 1e-4f) return true;

            float step = cellSize * 0.4f;
            int steps = Mathf.CeilToInt(dist / step);
            for (int i = 1; i <= steps; i++)
            {
                if (!IsWalkableAt(a + delta * ((float)i / steps))) return false;
            }
            return true;
        }

        /// <summary>Случайная проходимая точка на расстоянии [min, max] от from.</summary>
        public bool TryGetRandomWalkable(Vector2 from, float minDistance, float maxDistance, out Vector2 point)
        {
            point = from;
            if (!IsReady || walkableCells.Count == 0) return false;

            for (int attempt = 0; attempt < 40; attempt++)
            {
                int i = walkableCells[Random.Range(0, walkableCells.Count)];
                Vector2 p = NodePosition(i % width, i / width);
                float d = Vector2.Distance(from, p);
                if (d >= minDistance && d <= maxDistance)
                {
                    point = p;
                    return true;
                }
            }

            int j = walkableCells[Random.Range(0, walkableCells.Count)];
            point = NodePosition(j % width, j / width);
            return true;
        }

        /// <summary>Кратчайший путь from -> to (A*). Результат — список мировых точек без стартовой.</summary>
        public bool TryFindPath(Vector2 from, Vector2 to, List<Vector2> result)
        {
            result.Clear();
            if (!IsReady) return false;
            if (!TryGetNearestWalkable(from, out int start) || !TryGetNearestWalkable(to, out int goal)) return false;

            if (start == goal)
            {
                result.Add(NodePosition(goal % width, goal / width));
                return true;
            }

            stamp++;
            heapCount = 0;
            gCost[start] = 0f;
            parent[start] = -1;
            openStamp[start] = stamp;
            HeapPush(start, Heuristic(start, goal));

            bool found = false;
            while (heapCount > 0)
            {
                int cur = HeapPop().node;
                if (closedStamp[cur] == stamp) continue;
                closedStamp[cur] = stamp;
                if (cur == goal) { found = true; break; }

                int cx = cur % width, cy = cur / width;
                for (int d = 0; d < 8; d++)
                {
                    int nx = cx + Dx[d], ny = cy + Dy[d];
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                    int ni = ny * width + nx;
                    if (!walkable[ni] || closedStamp[ni] == stamp) continue;

                    bool diagonal = Dx[d] != 0 && Dy[d] != 0;
                    if (diagonal && (!walkable[cy * width + nx] || !walkable[ny * width + cx])) continue; // не срезаем углы

                    float ng = gCost[cur] + (diagonal ? Diagonal : 1f);
                    if (openStamp[ni] == stamp && ng >= gCost[ni]) continue;

                    openStamp[ni] = stamp;
                    gCost[ni] = ng;
                    parent[ni] = cur;
                    HeapPush(ni, ng + Heuristic(ni, goal));
                }
            }

            if (!found) return false;

            rawPath.Clear();
            for (int n = goal; n != -1; n = parent[n]) rawPath.Add(NodePosition(n % width, n / width));
            rawPath.Reverse();

            SmoothPath(from, rawPath, result);
            return result.Count > 0;
        }

        // Спрямление пути: от текущей точки идём к самой дальней видимой точке пути.
        private void SmoothPath(Vector2 from, List<Vector2> points, List<Vector2> result)
        {
            result.Clear();
            Vector2 anchor = from;
            int i = 0;
            while (i < points.Count)
            {
                int far = i;
                for (int j = points.Count - 1; j > i; j--)
                {
                    if (LineWalkable(anchor, points[j]))
                    {
                        far = j;
                        break;
                    }
                }
                result.Add(points[far]);
                anchor = points[far];
                i = far + 1;
            }
        }

        private float Heuristic(int a, int b)
        {
            float dx = Mathf.Abs(a % width - b % width);
            float dy = Mathf.Abs(a / width - b / width);
            return (dx + dy) + (Diagonal - 2f) * Mathf.Min(dx, dy);
        }

        private void HeapPush(int node, float f)
        {
            if (heapCount == heap.Length) System.Array.Resize(ref heap, heapCount * 2);
            int i = heapCount++;
            while (i > 0)
            {
                int p = (i - 1) >> 1;
                if (heap[p].f <= f) break;
                heap[i] = heap[p];
                i = p;
            }
            heap[i] = new HeapItem { node = node, f = f };
        }

        private HeapItem HeapPop()
        {
            HeapItem top = heap[0];
            heapCount--;
            if (heapCount > 0)
            {
                HeapItem last = heap[heapCount];
                int i = 0;
                while (true)
                {
                    int l = 2 * i + 1;
                    if (l >= heapCount) break;
                    int r = l + 1;
                    int m = (r < heapCount && heap[r].f < heap[l].f) ? r : l;
                    if (heap[m].f >= last.f) break;
                    heap[i] = heap[m];
                    i = m;
                }
                heap[i] = last;
            }
            return top;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!drawGizmos || !IsReady) return;
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.35f);
            Vector3 size = Vector3.one * (cellSize * 0.25f);
            for (int i = 0; i < walkableCells.Count; i++)
            {
                int c = walkableCells[i];
                Gizmos.DrawCube(NodePosition(c % width, c / width), size);
            }
        }
#endif
    }
}
