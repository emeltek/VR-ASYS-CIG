using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleController : MonoBehaviour
{
    public AudioSource sceneFinish;
    public static bool IsAducationSceneFinished = false;
    public AudioSource avalancheVoice;
    public bool isExamScene = false;
    private void OnParticleSystemStopped()
    {
        Debug.Log("Çığ partikülü bitti! Metot tetikleniyor...");
        avalancheVoice.Stop();
        if (isExamScene == false)
        {
            sceneFinish.Play();
            

        }

        ScoreManager.Instance.GetScore();
    }
}
