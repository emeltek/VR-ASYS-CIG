using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sınıf indeksleri. Python eğitim betiğiyle (GNN/train_gnn.py) aynı sırada olmalı.
/// </summary>
public enum GnnRiskClass
{
    Safe = 0,     // Güvenli: büyük kayanın arkası (sığınak)
    Exposed = 1,  // Açık alan: çığa karşı korunmasız
    Danger = 2    // Tehlikeli: ağaç dibi (kırılan dal / gövde, ağaç çukuru)
}

/// <summary>
/// Grafın sahneden nasıl çıkarılacağını belirleyen ayarlar. Mesafeler "gerçek metre" cinsindendir,
/// worldScale ile dünya birimine çevrilir (bu sahnede oyuncu ölçeği 6.44 olduğu için varsayılan 6.44).
/// </summary>
[Serializable]
public class GnnGraphSettings
{
    [Tooltip("Sahne ölçeği: 1 gerçek metre kaç dünya birimi (PlayerController scale = 6.44)")]
    public float worldScale = 6.44f;

    [Tooltip("Grid hücre boyu (m). Her yürünebilir hücre grafta bir düğüm olur.")]
    public float cellSize = 0.5f;

    [Tooltip("Alan sınırı: ağaç / sığınak kutularının ve oyuncunun kapsadığı alan bu kadar (m) genişletilir. Site Area atanmışsa kullanılmaz.")]
    public float siteMargin = 10f;

    [Tooltip("Ağaç / kaya / çığ yolu arama mesafesi (m)")]
    public float hazardProbeDistance = 30f;

    [Tooltip("Sığınak taraması (m): çığ yönünde bu mesafede kaya var mı")]
    public float shelterProbeDistance = 8f;

    [Tooltip("Yatay tarama mesafesi (m): çevre açıklığı")]
    public float clearanceProbeDistance = 5f;

    [Tooltip("Yukarı tarama mesafesi (m): ağaç dalı / kaya çıkıntısı")]
    public float overheadProbeDistance = 6f;

    [Tooltip("Yerel yükselti için çevre yarıçapı (m): sırt mı çukur mu")]
    public float elevationRadius = 5f;

    [Tooltip("Hücrenin boş sayılması için gövde yarıçapı (m)")]
    public float bodyRadius = 0.25f;

    [Tooltip("Yürünebilir / bağlı sayılan en dik eğim (derece)")]
    public float maxSlope = 45f;

    [Tooltip("Ağaç olarak kabul edilecek obje / materyal isim anahtar kelimeleri")]
    public string[] treeKeywords = { "tree", "spruce", "pine", "populus", "fir", "agac", "ağaç" };

    [Tooltip("Sığınak kayası olarak kabul edilecek obje / materyal isim anahtar kelimeleri")]
    public string[] rockKeywords = { "stone", "rock", "kaya" };

    public LayerMask layerMask = ~0;

    [Tooltip("Grid başlangıç kaydırması (hücre cinsinden). Eğitim verisi çoğaltmak için kullanılır.")]
    public Vector2 gridOffset = Vector2.zero;

    public GnnGraphSettings Clone() => (GnnGraphSettings)MemberwiseClone();
}

/// <summary>
/// Arazi yüzeyinden çıkarılmış graf: düğüm = yürünebilir grid hücresi,
/// kenar = engelsiz komşu hücreler. Komşuluk CSR formatında tutulur.
/// </summary>
public class GnnGraph
{
    public int FeatureCount;
    public float CellSize;
    public Vector3 GridOrigin;
    public Vector2 GridOffset;

    public readonly List<Vector3> Positions = new List<Vector3>(); // zemin noktası (dünya)
    public readonly List<Vector2Int> Cells = new List<Vector2Int>();
    public readonly Dictionary<Vector2Int, int> CellToNode = new Dictionary<Vector2Int, int>();

    public float[] Features;   // N * FeatureCount
    public int[] Labels;       // N (yalnızca eğitim verisi dışa aktarılırken dolu)

    public int[] NeighborOffsets; // N + 1
    public int[] NeighborIndices; // toplam kenar

    public int NodeCount => Positions.Count;
    public int EdgeCount => NeighborIndices?.Length ?? 0;

    public Vector2Int CellOf(Vector3 worldPosition)
    {
        return new Vector2Int(
            Mathf.FloorToInt((worldPosition.x - GridOrigin.x) / CellSize - GridOffset.x),
            Mathf.FloorToInt((worldPosition.z - GridOrigin.z) / CellSize - GridOffset.y));
    }

    /// <summary>Dışa aktarılmış JSON'dan grafı geri kurar (runtime'da sahne taranmaz).</summary>
    public static GnnGraph FromExport(GnnGraphExport e)
    {
        var g = new GnnGraph
        {
            FeatureCount = e.numFeatures,
            CellSize = e.cellSize,
            GridOrigin = new Vector3(e.gridOrigin[0], e.gridOrigin[1], e.gridOrigin[2]),
            GridOffset = new Vector2(e.gridOffset[0], e.gridOffset[1]),
            Features = e.features,
            Labels = e.labels
        };

        for (int i = 0; i < e.numNodes; i++)
        {
            g.Positions.Add(new Vector3(e.positions[i * 3], e.positions[i * 3 + 1], e.positions[i * 3 + 2]));
            var cell = new Vector2Int(e.cells[i * 2], e.cells[i * 2 + 1]);
            g.Cells.Add(cell);
            g.CellToNode[cell] = i;
        }

        // edgeSrc kaynak düğüme göre sıralı yazılır (bkz. ToExport), CSR doğrudan çıkarılır
        g.NeighborOffsets = new int[e.numNodes + 1];
        foreach (int s in e.edgeSrc) g.NeighborOffsets[s + 1]++;
        for (int i = 0; i < e.numNodes; i++) g.NeighborOffsets[i + 1] += g.NeighborOffsets[i];
        g.NeighborIndices = e.edgeDst;
        return g;
    }

    public GnnGraphExport ToExport(string sceneName, string[] featureNames, string[] classNames, float worldScale)
    {
        int n = NodeCount;
        var export = new GnnGraphExport
        {
            sceneName = sceneName,
            featureNames = featureNames,
            classNames = classNames,
            worldScale = worldScale,
            cellSize = CellSize,
            gridOrigin = new[] { GridOrigin.x, GridOrigin.y, GridOrigin.z },
            gridOffset = new[] { GridOffset.x, GridOffset.y },
            numNodes = n,
            numFeatures = FeatureCount,
            positions = new float[n * 3],
            cells = new int[n * 2],
            features = Features,
            labels = Labels ?? new int[0],
            edgeSrc = new int[EdgeCount],
            edgeDst = new int[EdgeCount]
        };

        for (int i = 0; i < n; i++)
        {
            Vector3 p = Positions[i];
            export.positions[i * 3] = p.x;
            export.positions[i * 3 + 1] = p.y;
            export.positions[i * 3 + 2] = p.z;
            export.cells[i * 2] = Cells[i].x;
            export.cells[i * 2 + 1] = Cells[i].y;

            for (int e = NeighborOffsets[i]; e < NeighborOffsets[i + 1]; e++)
            {
                export.edgeSrc[e] = i;
                export.edgeDst[e] = NeighborIndices[e];
            }
        }

        return export;
    }
}

/// <summary>
/// Dışa aktarılan graf JSON'u. Python eğitimi bunu okur; runtime da aynı dosyadan grafı yükler.
/// </summary>
[Serializable]
public class GnnGraphExport
{
    public string sceneName;
    public string[] featureNames;
    public string[] classNames;
    public float worldScale;
    public float cellSize;
    public float[] gridOrigin; // 3
    public float[] gridOffset; // 2
    public int numNodes;
    public int numFeatures;
    public float[] positions; // N * 3
    public int[] cells;       // N * 2
    public float[] features;  // N * F
    public int[] labels;      // N
    public int[] edgeSrc;
    public int[] edgeDst;
}
