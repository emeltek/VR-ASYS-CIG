using UnityEngine;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("UI AYARLARI")]
    [SerializeField] private TextMeshProUGUI scoreText;

    [Header("PUAN AYARLARI")]
    [Tooltip("Başlangıç puanı. GNN puanlayıcı (GnnRiskScorer) sahnedeyse 100'e ayarlanır ve puan cezalarla düşer.")]
    [SerializeField] private int currentScore = 30;

    [Header("Risk Etiketi (sonuçta puana göre gösterilir)")]
    [SerializeField] private int guvenliEsigi = 75;
    [SerializeField] private int ortaRiskEsigi = 50;
    [SerializeField] private int riskliEsigi = 25;

    /// <summary>Sonuç kesinleşti: bundan sonra puan değişmez, risk etiketi gösterilir.</summary>
    public bool IsFinalized { get; private set; }

    // Sınır değerlerimizi net olarak tanımlıyoruz
    private const int MAX_SCORE = 100;
    private const int MIN_SCORE = 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        UpdateScoreUI();
    }

    /// <summary>
    /// Oyuncunun puanını artırır (Maksimum 100 olabilir)
    /// </summary>
    public void AddScore(int amount)
    {
        if (IsFinalized) return;
        currentScore += amount;
        
        // Puanın 100'ü geçmesini engelliyoruz
        currentScore = Mathf.Clamp(currentScore, MIN_SCORE, MAX_SCORE);

        UpdateScoreUI();
        Debug.Log("Puan Eklendi: +" + amount + " | Güncel Puan: " + currentScore);
    }

    /// <summary>
    /// Oyuncunun puanını düşürür (Minimum 0 olabilir)
    /// </summary>
    public void DecreaseScore(int amount)
    {
        if (IsFinalized) return;
        currentScore -= amount;
        
        // Puanın 0'ın altına düşmesini engelliyoruz
        currentScore = Mathf.Clamp(currentScore, MIN_SCORE, MAX_SCORE);

        UpdateScoreUI();
        Debug.Log("Puan Düştü: -" + amount + " | Güncel Puan: " + currentScore);
    }

    /// <summary>Puanı doğrudan ayarlar (GNN puanlayıcı 100'den başlatmak için kullanır).</summary>
    public void SetScore(int value)
    {
        if (IsFinalized) return;
        currentScore = Mathf.Clamp(value, MIN_SCORE, MAX_SCORE);
        UpdateScoreUI();
    }

    /// <summary>Sonucu kesinleştirir: puan kilitlenir ve risk etiketi gösterilir.</summary>
    public void FinalizeScore()
    {
        if (IsFinalized) return;
        IsFinalized = true;
        UpdateScoreUI();
        Debug.Log($"[Puan] Sonuç: {currentScore} → {GetRiskLabel()}");
    }

    /// <summary>Puana göre risk etiketi: yüksek puan = doğru davranış = düşük risk.</summary>
    public string GetRiskLabel()
    {
        if (currentScore >= guvenliEsigi) return "Güvenli";
        if (currentScore >= ortaRiskEsigi) return "Orta Riskli";
        if (currentScore >= riskliEsigi) return "Riskli";
        return "Yüksek Riskli";
    }

    private string GetRiskColor()
    {
        if (currentScore >= guvenliEsigi) return "#3CC864";
        if (currentScore >= ortaRiskEsigi) return "#F0B43C";
        if (currentScore >= riskliEsigi) return "#F07832";
        return "#E04646";
    }

    /// <summary>
    /// Güncel puan değerini döndürür
    /// </summary>
    public int GetScore()
    {
        return currentScore;
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text = "Puan: " + currentScore;
            if (IsFinalized)
                scoreText.text += $"\n<size=75%><color={GetRiskColor()}>{GetRiskLabel()}</color></size>";
        }
        else
        {
            Debug.LogWarning("ScoreManager: 'scoreText' referansı boş! Lütfen Inspector'dan atama yapın.");
        }
    }
}