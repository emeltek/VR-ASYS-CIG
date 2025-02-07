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

    private float gameTime = 0;

    private void Update()
    {
        gameTime += Time.deltaTime;

        EffectTransform();
        UpdateEffectSize();

        Debug.Log(gameTime);
    }

    private void EffectTransform() 
    {
        SmokeEffect.transform.position = targetPosition.position;
        SmokeEffect.transform.rotation = Quaternion.identity;

        SnowEffect.transform.position = targetPosition.position;
        SnowEffect.transform.rotation = Quaternion.identity;
    }

    private void UpdateEffectSize()
    {
        float normalizedTime = Mathf.Clamp01(gameTime / maxGameTime);
        float newSize = Mathf.Lerp(20f, 200f, sizeCurve.Evaluate(normalizedTime) * 4f);

        var smokeMain = SmokeEffect.main;
        smokeMain.startSize = newSize;
    }
}
