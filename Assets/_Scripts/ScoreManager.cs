using UnityEngine;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("UI AYARLARI")]
    [SerializeField] private TextMeshProUGUI scoreText;

    [Header("PUAN AYARLARI")]
    [SerializeField] private int currentScore = 30; // Simülasyona 100 tam puanla başlıyoruz

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
        currentScore -= amount;
        
        // Puanın 0'ın altına düşmesini engelliyoruz
        currentScore = Mathf.Clamp(currentScore, MIN_SCORE, MAX_SCORE);

        UpdateScoreUI();
        Debug.Log("Puan Düştü: -" + amount + " | Güncel Puan: " + currentScore);
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
        }
        else
        {
            Debug.LogWarning("ScoreManager: 'scoreText' referansı boş! Lütfen Inspector'dan atama yapın.");
        }
    }
}