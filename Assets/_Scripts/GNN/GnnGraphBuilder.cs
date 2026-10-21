using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Etiket kaynağı olan trigger: ağaç kutusu (Danger) veya sığınak kutusu (Safe).</summary>
public struct GnnLabelZone
{
    public Collider Collider;
    public GnnRiskClass Class;

    public bool Contains(Vector3 worldPoint) => (Collider.ClosestPoint(worldPoint) - worldPoint).sqrMagnitude < 1e-4f;
}

/// <summary>
/// Dağ yamacının arazi yüzeyinden GNN grafı kurar. Eğitim verisi ve runtime grafı aynı kodla
/// Editor'de üretilir; böylece özellikler birebir aynı hesaplanır.
///
/// ÖNEMLİ: Tree / SafeArea trigger kutuları özellik olarak KULLANILMAZ (trigger'lar fizik sorgularından
/// hariç tutulur). Kutular yalnızca eğitimde etiket kaynağıdır (ComputeLabels).
/// </summary>
public sealed class GnnGraphBuilder
{
    public static readonly string[] FeatureNames =
    {
        "slope",                  // 0: arazi eğimi (çığ 30-45° yamaçta kopar)
        "tree_distance",          // 1: en yakın ağaca uzaklık
        "rock_distance",          // 2: en yakın büyük kayaya uzaklık
        "shelter",                // 3: çığ yönünde önünü kesen kaya oranı (kaya arkası = 1)
        "avalanche_path_distance",// 4: çığ yoluna yatay uzaklık
        "relative_elevation",     // 5: çevreye göre yükselti (sırt > 0.5, çukur < 0.5)
        "openness",               // 6: açık yön oranı
        "overhead_clearance"      // 7: baş üstündeki ilk engele uzaklık (dal / kaya çıkıntısı)
    };

    public static readonly string[] ClassNames = { "Safe", "Exposed", "Danger" };
    public static int FeatureCount => FeatureNames.Length;

    private static readonly Vector3[] Dirs8 =
    {
        new Vector3(1, 0, 0), new Vector3(-1, 0, 0), new Vector3(0, 0, 1), new Vector3(0, 0, -1),
        new Vector3(0.7071f, 0, 0.7071f), new Vector3(-0.7071f, 0, 0.7071f),
        new Vector3(0.7071f, 0, -0.7071f), new Vector3(-0.7071f, 0, -0.7071f)
    };

    private static readonly float[] ShelterAngles = { -30f, -15f, 0f, 15f, 30f };

    private static readonly Vector2Int[] ForwardNeighbors =
    {
        new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, -1)
    };

    private readonly GnnGraphSettings settings;
    private readonly float S;
    private readonly HashSet<Collider> ignored = new HashSet<Collider>();
    private readonly List<Bounds> trees = new List<Bounds>();
    private readonly List<Bounds> rocks = new List<Bounds>();
    private readonly HashSet<Collider> rockColliders = new HashSet<Collider>();
    private readonly List<Vector3> avalanchePath;
    private readonly Vector3 avalancheOrigin;
    private readonly List<Vector3> normals = new List<Vector3>();
    private readonly RaycastHit[] hitBuffer = new RaycastHit[64];
    private readonly Collider[] colliderBuffer = new Collider[256];
    private readonly Bounds area;
    private readonly float minWalkableNormalY;

    public GnnGraph Graph { get; } = new GnnGraph();
    public bool HasArea { get; }
    public int TreeCount => trees.Count;
    public int RockCount => rocks.Count;
    public int PathPointCount => avalanchePath.Count;

    /// <param name="siteArea">Grafın kurulacağı alan (dünya). Boyutu sıfırsa graf kurulmaz.</param>
    /// <param name="avalanchePath">Çığ yolu: başlangıç noktasından aşağı doğru sıralı noktalar</param>
    /// <param name="ignoreRoots">Collider'ları ve renderer'ları yok sayılacak objeler (oyuncu, çığ topu)</param>
    public GnnGraphBuilder(GnnGraphSettings settings, Bounds siteArea, List<Vector3> avalanchePath,
        IEnumerable<Transform> ignoreRoots)
    {
        this.settings = settings;
        S = settings.worldScale;
        area = siteArea;
        this.avalanchePath = avalanchePath;
        avalancheOrigin = avalanchePath.Count > 0 ? avalanchePath[0] : siteArea.center + Vector3.forward * 1000f;
        HasArea = siteArea.size.x > 0f && siteArea.size.z > 0f;
        minWalkableNormalY = Mathf.Cos(settings.maxSlope * Mathf.Deg2Rad);

        var roots = new List<Transform>();
        foreach (Transform root in ignoreRoots)
        {
            if (root == null) continue;
            roots.Add(root);
            foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
                ignored.Add(c);
        }

        if (HasArea) CollectSources(roots);

        Graph.FeatureCount = FeatureCount;
        Graph.CellSize = settings.cellSize * S;
        Graph.GridOrigin = new Vector3(area.min.x, area.min.y, area.min.z);
        Graph.GridOffset = settings.gridOffset;
    }

    /// <summary>Grafı birkaç kareye yayarak kurar.</summary>
    public IEnumerator Build(int nodesPerStep)
    {
        if (!HasArea) yield break;
        Physics.SyncTransforms();

        float cs = Graph.CellSize;
        int countX = Mathf.CeilToInt(area.size.x / cs);
        int countZ = Mathf.CeilToInt(area.size.z / cs);
        int budget = 0;

        // 1) Düğümler: yürünebilir ve boş arazi hücreleri
        for (int ix = 0; ix < countX; ix++)
        {
            for (int iz = 0; iz < countZ; iz++)
            {
                var cell = new Vector2Int(ix, iz);
                if (TrySampleGround(CellCenter(cell), out Vector3 floorPoint, out Vector3 normal) && IsFree(floorPoint))
                {
                    Graph.CellToNode[cell] = Graph.Positions.Count;
                    Graph.Positions.Add(floorPoint);
                    Graph.Cells.Add(cell);
                    normals.Add(normal);
                }

                if (++budget >= nodesPerStep * 2) { budget = 0; yield return null; }
            }
        }

        // 2) Düğüm özellikleri
        int n = Graph.NodeCount;
        int f = FeatureCount;
        Graph.Features = new float[n * f];
        for (int i = 0; i < n; i++)
        {
            ComputeFeatures(Graph.Positions[i], normals[i], Graph.Features, i * f);
            if (++budget >= nodesPerStep) { budget = 0; yield return null; }
        }

        // 3) Kenarlar: arada engel olmayan, yürünebilir eğimdeki 8-komşular
        var adjacency = new List<int>[n];
        for (int i = 0; i < n; i++) adjacency[i] = new List<int>(8);
        for (int i = 0; i < n; i++)
        {
            Vector2Int cell = Graph.Cells[i];
            foreach (Vector2Int d in ForwardNeighbors)
            {
                if (!Graph.CellToNode.TryGetValue(cell + d, out int j)) continue;
                if (!IsConnected(Graph.Positions[i], Graph.Positions[j])) continue;
                adjacency[i].Add(j);
                adjacency[j].Add(i);
            }
            if (++budget >= nodesPerStep * 2) { budget = 0; yield return null; }
        }

        Graph.NeighborOffsets = new int[n + 1];
        for (int i = 0; i < n; i++) Graph.NeighborOffsets[i + 1] = Graph.NeighborOffsets[i] + adjacency[i].Count;
        Graph.NeighborIndices = new int[Graph.NeighborOffsets[n]];
        for (int i = 0; i < n; i++) adjacency[i].CopyTo(Graph.NeighborIndices, Graph.NeighborOffsets[i]);
    }

    public GnnGraph BuildImmediate()
    {
        IEnumerator routine = Build(int.MaxValue);
        while (routine.MoveNext()) { }
        return Graph;
    }

    private Vector3 CellCenter(Vector2Int cell)
    {
        float cs = Graph.CellSize;
        return new Vector3(
            Graph.GridOrigin.x + (cell.x + 0.5f + settings.gridOffset.x) * cs,
            area.max.y,
            Graph.GridOrigin.z + (cell.y + 0.5f + settings.gridOffset.y) * cs);
    }

    /// <summary>Hücredeki yürünebilir arazi noktası (eğim sınırı içinde).</summary>
    private bool TrySampleGround(Vector3 center, out Vector3 point, out Vector3 normal)
    {
        return TryTerrainPoint(center, out point, out normal) && normal.y >= minWalkableNormalY;
    }

    /// <summary>Arazi yüzeyi: yalnızca Terrain yüzeyleri zemin sayılır (kaya / ağaç tepesi değil).</summary>
    private bool TryTerrainPoint(Vector3 center, out Vector3 point, out Vector3 normal)
    {
        point = center;
        normal = Vector3.up;
        Vector3 origin = new Vector3(center.x, area.max.y + 10f * S, center.z);
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, hitBuffer, area.size.y + 20f * S, settings.layerMask, QueryTriggerInteraction.Ignore);

        bool found = false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = hitBuffer[i];
            if (!(h.collider is TerrainCollider) || ignored.Contains(h.collider)) continue;
            if (!found || h.point.y > point.y) { point = h.point; normal = h.normal; found = true; }
        }
        return found;
    }

    /// <summary>Hücre bir kaya / yapı tarafından dolduruluyorsa düğüm değil.</summary>
    private bool IsFree(Vector3 floorPoint)
    {
        float r = settings.bodyRadius * S;
        Vector3 bottom = floorPoint + Vector3.up * (r + 0.3f * S);
        Vector3 head = floorPoint + Vector3.up * 1.6f * S;
        int count = Physics.OverlapCapsuleNonAlloc(bottom, head, r, colliderBuffer, settings.layerMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider c = colliderBuffer[i];
            if (!(c is TerrainCollider) && !ignored.Contains(c)) return false;
        }
        return true;
    }

    private void ComputeFeatures(Vector3 fp, Vector3 normal, float[] output, int offset)
    {
        Vector3 up = Vector3.up;
        float probe = settings.hazardProbeDistance * S;
        Vector3 chest = fp + up * (1.2f * S);
        Vector3 waist = fp + up * (0.8f * S);

        // 0: Eğim
        float slope = Vector3.Angle(normal, up) / 60f;

        // 1-2: Ağaç / kaya uzaklığı
        float tree = probe, rock = probe;
        foreach (Bounds b in trees) tree = Mathf.Min(tree, Mathf.Sqrt(b.SqrDistance(chest)));
        foreach (Bounds b in rocks) rock = Mathf.Min(rock, Mathf.Sqrt(b.SqrDistance(chest)));

        // 3: Sığınak: çığın geldiği yöne bakan ışınların kaç tanesi bir kayaya çarpıyor
        Vector3 toAvalanche = avalancheOrigin - fp;
        toAvalanche.y = 0f;
        toAvalanche = toAvalanche.sqrMagnitude > 1e-4f ? toAvalanche.normalized : Vector3.forward;
        int blocked = 0;
        foreach (float angle in ShelterAngles)
        {
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * toAvalanche;
            if (NearestHit(chest, dir, settings.shelterProbeDistance * S, out RaycastHit h) && rockColliders.Contains(h.collider))
                blocked++;
        }

        // 4: Çığ yoluna yatay uzaklık
        float path = probe;
        for (int i = 0; i + 1 < avalanchePath.Count; i++)
            path = Mathf.Min(path, HorizontalSegmentDistance(fp, avalanchePath[i], avalanchePath[i + 1]));

        // 5: Yerel yükselti
        float sum = 0f;
        int samples = 0;
        float ringR = settings.elevationRadius * S;
        foreach (Vector3 dir in Dirs8)
        {
            if (TryTerrainPoint(fp + dir * ringR, out Vector3 p, out _))
            {
                sum += p.y;
                samples++;
            }
        }
        float relative = samples > 0 ? fp.y - sum / samples : 0f;
        float elevation = Mathf.Clamp01(0.5f + relative / (6f * S));

        // 6: Açıklık
        float clearProbe = settings.clearanceProbeDistance * S;
        int open = 0;
        foreach (Vector3 dir in Dirs8)
        {
            if (!NearestHit(waist, dir, clearProbe * 0.5f, out _)) open++;
        }

        // 7: Baş üstü
        float upLength = settings.overheadProbeDistance * S;
        float overhead = NearestHit(fp + up * (2f * S), up, upLength, out RaycastHit upHit) ? upHit.distance / upLength : 1f;

        output[offset + 0] = Mathf.Clamp01(slope);
        output[offset + 1] = tree / probe;
        output[offset + 2] = rock / probe;
        output[offset + 3] = blocked / (float)ShelterAngles.Length;
        output[offset + 4] = path / probe;
        output[offset + 5] = elevation;
        output[offset + 6] = open / (float)Dirs8.Length;
        output[offset + 7] = overhead;
    }

    private static float HorizontalSegmentDistance(Vector3 p, Vector3 a, Vector3 b)
    {
        var p2 = new Vector2(p.x, p.z);
        var a2 = new Vector2(a.x, a.z);
        var ab = new Vector2(b.x, b.z) - a2;
        float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p2 - a2, ab) / ab.sqrMagnitude) : 0f;
        return Vector2.Distance(p2, a2 + ab * t);
    }

    private bool IsConnected(Vector3 a, Vector3 b)
    {
        Vector3 delta = b - a;
        float horizontal = new Vector2(delta.x, delta.z).magnitude;
        if (Mathf.Abs(delta.y) > horizontal * Mathf.Tan(settings.maxSlope * Mathf.Deg2Rad)) return false;
        Vector3 from = a + Vector3.up * (0.6f * S);
        return !NearestHit(from, delta.normalized, delta.magnitude, out _, skipTerrain: true);
    }

    private bool NearestHit(Vector3 origin, Vector3 dir, float length, out RaycastHit best, bool skipTerrain = false)
    {
        int count = Physics.RaycastNonAlloc(origin, dir, hitBuffer, length, settings.layerMask, QueryTriggerInteraction.Ignore);
        best = default;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = hitBuffer[i];
            if (ignored.Contains(h.collider) || (skipTerrain && h.collider is TerrainCollider)) continue;
            if (!found || h.distance < best.distance) { best = h; found = true; }
        }
        return found;
    }

    private void CollectSources(List<Transform> ignoreRoots)
    {
        Bounds searchArea = area;
        searchArea.Expand(new Vector3(2f, 0f, 2f) * settings.hazardProbeDistance * S);

        foreach (MeshRenderer r in Object.FindObjectsOfType<MeshRenderer>())
        {
            if (!r.enabled || !searchArea.Intersects(r.bounds) || IsUnder(r.transform, ignoreRoots)) continue;
            if (MatchesAny(r.transform, r.sharedMaterials, settings.treeKeywords)) trees.Add(r.bounds);
            else if (MatchesAny(r.transform, r.sharedMaterials, settings.rockKeywords)) rocks.Add(r.bounds);
        }

        foreach (Collider c in Object.FindObjectsOfType<Collider>())
        {
            if (c.isTrigger || c is TerrainCollider || ignored.Contains(c)) continue;
            if (MatchesAny(c.transform, null, settings.rockKeywords)) rockColliders.Add(c);
        }
    }

    /// <summary>Objenin kendi adı, üst objelerinin adı veya materyali anahtar kelimeyi içeriyor mu.</summary>
    private static bool MatchesAny(Transform start, Material[] materials, string[] keywords)
    {
        for (Transform t = start; t != null; t = t.parent)
        {
            if (ContainsKeyword(t.name, keywords)) return true;
        }
        if (materials != null)
        {
            foreach (Material m in materials)
            {
                if (m != null && ContainsKeyword(m.name, keywords)) return true;
            }
        }
        return false;
    }

    private static bool ContainsKeyword(string text, string[] keywords)
    {
        string lower = text.ToLowerInvariant();
        foreach (string k in keywords)
        {
            if (!string.IsNullOrEmpty(k) && lower.Contains(k.ToLowerInvariant())) return true;
        }
        return false;
    }

    private static bool IsUnder(Transform t, List<Transform> roots)
    {
        foreach (Transform root in roots)
        {
            if (t.IsChildOf(root)) return true;
        }
        return false;
    }

    /// <summary>
    /// Etiket kaynakları: Tree tag'li trigger'lar (Danger) ve SafeAreaController trigger'ları (Safe).
    /// Kapalı objeler dahil edilmez (etiket, sahnenin oynanan hâlini yansıtmalı).
    /// </summary>
    public static List<GnnLabelZone> FindLabelZones()
    {
        var zones = new List<GnnLabelZone>();
        foreach (Collider c in Object.FindObjectsOfType<Collider>())
        {
            if (c.isTrigger && c.gameObject.CompareTag("Tree"))
                zones.Add(new GnnLabelZone { Collider = c, Class = GnnRiskClass.Danger });
        }
        foreach (SafeAreaController safe in Object.FindObjectsOfType<SafeAreaController>())
        {
            foreach (Collider c in safe.GetComponents<Collider>())
            {
                if (c.isTrigger) zones.Add(new GnnLabelZone { Collider = c, Class = GnnRiskClass.Safe });
            }
        }
        return zones;
    }

    /// <summary>Sığınaklardan kaç tanesinin trigger collider'ı var (eksikse etiket de eksik olur).</summary>
    public static int SafeAreasWithoutTrigger()
    {
        int missing = 0;
        foreach (SafeAreaController safe in Object.FindObjectsOfType<SafeAreaController>())
        {
            bool hasTrigger = false;
            foreach (Collider c in safe.GetComponents<Collider>()) hasTrigger |= c.isTrigger;
            if (!hasTrigger) missing++;
        }
        return missing;
    }

    /// <summary>
    /// Eğitim etiketleri: ağaç kutusunun içi Danger, sığınak kutusunun içi Safe, kalan arazi Exposed.
    /// Çakışmada tehlike önceliklidir.
    /// </summary>
    public static int[] ComputeLabels(GnnGraph graph, List<GnnLabelZone> zones, float worldScale)
    {
        var labels = new int[graph.NodeCount];
        for (int i = 0; i < graph.NodeCount; i++)
        {
            Vector3 p = graph.Positions[i] + Vector3.up * (1f * worldScale);
            int label = (int)GnnRiskClass.Exposed;
            foreach (GnnLabelZone z in zones)
            {
                if (!z.Contains(p)) continue;
                if (z.Class == GnnRiskClass.Danger) { label = (int)GnnRiskClass.Danger; break; }
                label = (int)GnnRiskClass.Safe;
            }
            labels[i] = label;
        }
        return labels;
    }
}
