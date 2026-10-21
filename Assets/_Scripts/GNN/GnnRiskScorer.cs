using UnityEngine;

/// <summary>
/// Çığ sınavını GNN risk haritasıyla puanlar. Etkinken eski bonus / ceza script'leri (SafeAreaController +40,
/// HandsController +30, CharacterColliderTrigger −20) puan değiştirmez; kutular yalnızca eğitim etiketi olarak kalır.
///
/// Puan 100'den başlar ve üç kalemden düşer (sonuçta standart risk etiketi gösterilir):
///   1) Çığ anında konum:   ceza = açıkAlanCezası × P(Açık) + ağaçCezası × P(Tehlikeli)   (kaya arkası ≈ 0)
///   2) Korunma pozisyonu:  çığ anından itibaren pencere içinde iki el başta değilse ceza
///   3) Ağaç altında bekleme (çığ gelene kadar):
///        ceza = bütçe × [ girişPayı × w_giriş + (1 − girişPayı) × (1 − e^(−E / τ)) ]
///        w_giriş: ilk girişteki GNN tehlike olasılığı, E: tehlike olasılığıyla ağırlıklı süre (s)
/// Pencere bitince puan kesinleşir ve puanlama kapanır.
/// </summary>
public class GnnRiskScorer : MonoBehaviour
{
    private static GnnRiskScorer active;

    /// <summary>
    /// true iken eski bonus / ceza script'leri puan değiştirmez. Model / graf yüklenemediyse false kalır,
    /// böylece eski puanlama devreye girer.
    /// </summary>
    public static bool IsScoring => active != null && active.riskPredictor != null && active.riskPredictor.IsReady;

    [Header("Referanslar (boşsa otomatik bulunur)")]
    [SerializeField] private GnnRiskPredictor riskPredictor;
    [SerializeField] private SceneManager sceneManager;
    [Tooltip("Konumu puanlanan obje. Boşsa ana kamera (oyuncunun başı)")]
    [SerializeField] private Transform trackedTarget;

    [Header("Başlangıç")]
    [SerializeField] private int startingScore = 100;

    [Header("1) Çığ Anında Konum")]
    [Tooltip("Açık alanda yakalanma cezası (model tam eminken)")]
    [SerializeField] private float exposedImpactPenalty = 50f;
    [Tooltip("Ağaç dibinde yakalanma cezası (model tam eminken)")]
    [SerializeField] private float dangerImpactPenalty = 60f;

    [Header("2) Korunma Pozisyonu")]
    [Tooltip("Çığ anında iki el başta değilse kesilen puan")]
    [SerializeField] private int noPosturePenalty = 30;
    [Tooltip("Çığ anından itibaren pozisyonun kontrol edildiği süre (s); bitince puan kesinleşir")]
    [SerializeField] private float postureWindow = 3f;

    [Header("3) Ağaç Altında Bekleme (çığ gelene kadar)")]
    [SerializeField] private float treeBudget = 30f;
    [Tooltip("Bütçenin ilk girişte hemen kesilen payı")]
    [SerializeField, Range(0f, 1f)] private float entryShare = 0.4f;
    [Tooltip("Maruziyet zaman sabiti (s): bu kadar kalınca kalan payın %63'ü kesilmiş olur")]
    [SerializeField] private float exposureTimeConstant = 5f;

    [Header("Zamanlama")]
    [SerializeField] private float updateInterval = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private bool initialized;
    private float nextUpdate;
    private float lastTick = float.NegativeInfinity;
    private bool wasInDanger;

    // Ağaç maruziyeti
    private bool treeEntered;
    private float treeEntryWeight;
    private float treeExposure;
    private int treeDeducted;

    // Çığ anı
    private bool impactStarted;
    private float impactTime;
    private int impactPenalty;
    private bool postureOk;

    private void Start()
    {
        if (riskPredictor == null) riskPredictor = GnnRiskPredictor.Instance;
        if (riskPredictor == null) riskPredictor = FindObjectOfType<GnnRiskPredictor>();
        if (riskPredictor == null) riskPredictor = gameObject.AddComponent<GnnRiskPredictor>();
        if (sceneManager == null) sceneManager = FindObjectOfType<SceneManager>();
        if (trackedTarget == null && Camera.main != null) trackedTarget = Camera.main.transform;
    }

    private void OnEnable() => active = this;

    private void OnDisable()
    {
        if (active == this) active = null;
    }

    private void Update()
    {
        ScoreManager score = ScoreManager.Instance;
        if (!IsScoring || score == null || score.IsFinalized || trackedTarget == null) return;

        if (!initialized)
        {
            initialized = true;
            score.SetScore(startingScore);
        }

        if (impactStarted)
        {
            postureOk |= HandsController.BothHandsAtHead;
            if (Time.time - impactTime >= postureWindow) FinishImpact(score);
            return;
        }

        // Çığ ancak senaryo başladıktan sonra gelebilir (başka sahneden kalan duruma karşı ek güvence)
        bool sceneStarted = sceneManager == null || sceneManager.isSceneStart;
        if (AvanlancheTrigger.isAvalancheNear && sceneStarted)
        {
            StartImpact();
            return;
        }

        if (Time.time < nextUpdate) return;
        nextUpdate = Time.time + updateInterval;
        float dt = Mathf.Min(Time.time - lastTick, updateInterval * 2f);
        lastTick = Time.time;

        if (sceneManager != null && !sceneManager.isSceneStart) return;
        UpdateTreeExposure(score, dt);
    }

    private void UpdateTreeExposure(ScoreManager score, float dt)
    {
        GnnPrediction p = riskPredictor.GetPrediction(trackedTarget.position);
        bool inDanger = p.IsValid && p.RiskClass == GnnRiskClass.Danger;
        bool entering = inDanger && !wasInDanger;
        wasInDanger = inDanger;
        if (!inDanger) return;

        float w = p.DangerProbability;
        if (!treeEntered)
        {
            treeEntered = true;
            treeEntryWeight = w;
        }
        else if (!entering)
        {
            treeExposure += w * dt;
        }

        float target = treeBudget * (entryShare * treeEntryWeight + (1f - entryShare) * (1f - Mathf.Exp(-treeExposure / exposureTimeConstant)));
        int penalty = Mathf.RoundToInt(target) - treeDeducted;
        if (penalty <= 0) return;

        treeDeducted += penalty;
        score.DecreaseScore(penalty);
        if (showDebugLogs)
            Debug.Log($"<color=red>[GNN] AĞAÇ ALTI (p={w:F2}, maruziyet={treeExposure:F1} s): -{penalty} (toplam {treeDeducted}/{Mathf.RoundToInt(treeBudget)}) → Puan: {score.GetScore()}</color>");
    }

    private void StartImpact()
    {
        impactStarted = true;
        impactTime = Time.time;
        postureOk = HandsController.BothHandsAtHead;

        // Graf dışındaysa (ör. arazi dışı) açık alanda kabul edilir
        GnnPrediction p = riskPredictor.GetPrediction(trackedTarget.position);
        float exposed = p.IsValid ? p.ExposedProbability : 1f;
        float danger = p.IsValid ? p.DangerProbability : 0f;
        impactPenalty = Mathf.RoundToInt(exposedImpactPenalty * exposed + dangerImpactPenalty * danger);

        if (showDebugLogs)
        {
            string where = !p.IsValid ? "graf dışı" : p.RiskClass == GnnRiskClass.Safe ? "kaya arkası" : p.RiskClass == GnnRiskClass.Danger ? "ağaç dibi" : "açık alan";
            Debug.Log($"<color=cyan>[GNN] ÇIĞ ANI: {where} (P güvenli={p.SafeProbability:F2}, açık={exposed:F2}, tehlikeli={danger:F2}) → konum cezası -{impactPenalty}</color>");
        }
    }

    private void FinishImpact(ScoreManager score)
    {
        int posturePenalty = postureOk ? 0 : noPosturePenalty;
        score.DecreaseScore(impactPenalty + posturePenalty);
        if (showDebugLogs)
            Debug.Log($"<color=cyan>[GNN] Korunma pozisyonu {(postureOk ? "alındı" : "alınmadı")}: konum -{impactPenalty}, pozisyon -{posturePenalty} → Puan: {score.GetScore()}</color>");
        score.FinalizeScore();
    }
}
