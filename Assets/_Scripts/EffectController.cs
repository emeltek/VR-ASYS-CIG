using UnityEngine;

public class EffectController : MonoBehaviour
{
    [Header("TARGET")]
    [SerializeField] private Transform targetPosition;

    [Header("EFFECTS")]
    [SerializeField] private ParticleSystem SmokeEffect;
    [SerializeField] private ParticleSystem SnowEffect;

    [Header("PARTICLE SETTINGS")]
    [SerializeField] private AnimationCurve sizeCurve;
    [SerializeField] private float maxGameTime = 40f;
    
    [Space]
    [SerializeField] private float minParticleSize = 15f;
    // Bir önceki adımdaki boşluk hatasını engellemek için max boyutu 60 yaptık
    [SerializeField] private float maxParticleSize = 60f; 

    private float gameTime = 0;

    private void Update()
    {
        // Zamanlayıcıyı sınırla (maxGameTime'ı aşınca durdur)
        if (gameTime < maxGameTime)
        {
            gameTime += Time.deltaTime;
        }

        EffectTransform();
        UpdateEffectSize();
    }

    private void EffectTransform() 
    {
        if (targetPosition == null) return;

        Vector3 targetPos = targetPosition.position;
        SmokeEffect.transform.position = targetPos;
        SmokeEffect.transform.rotation = Quaternion.identity;

        SnowEffect.transform.position = targetPos;
        SnowEffect.transform.rotation = Quaternion.identity;
    }

    private void UpdateEffectSize()
    {
        float normalizedTime = Mathf.Clamp01(gameTime / maxGameTime);
        
        // Ekranda dengeli büyüme için * 4f çarpanını kaldırdık, eğri ne derse o olacak
        float newSize = Mathf.Lerp(minParticleSize, maxParticleSize, sizeCurve.Evaluate(normalizedTime));

        // Smoke Boyut Güncellemesi
        var smokeMain = SmokeEffect.main;
        smokeMain.startSize = newSize;

        // Snow Boyut Güncellemesi (Unutulan kısım eklendi)
        var snowMain = SnowEffect.main;
        snowMain.startSize = newSize * 0.5f; // Kar taneleri dumandan biraz daha küçük olsun diye 0.5 ile çarptık
    }
}